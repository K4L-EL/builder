using System.Text;
using BetBuilder.Application.Interfaces;
using BetBuilder.Infrastructure.Snapshots;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BetBuilder.Infrastructure.Simulation;

public sealed class SimulationStatus
{
    public bool IsPlaying { get; init; }
    public int CurrentIndex { get; init; }
    public int TotalFiles { get; init; }
    public double Speed { get; init; }
    public string? CurrentTimestamp { get; init; }
    public double ElapsedFightSeconds { get; init; }
}

public interface IFightSimulationService
{
    SimulationStatus GetStatus();
    SimulationStatus Start(double speed);
    SimulationStatus Stop();
}

public sealed class FightSimulationService : IFightSimulationService
{
    private readonly IPricingSnapshotFactory _factory;
    private readonly IActiveSnapshotStore _store;
    private readonly ILogger<FightSimulationService> _logger;
    private readonly string _mockDataDirectory;

    private readonly object _lock = new();
    private string[] _sortedFiles = Array.Empty<string>();
    private CancellationTokenSource? _cts;
    private Task? _runningTask;

    private volatile bool _isPlaying;
    private volatile int _currentIndex;
    private double _speed = 1.0;
    private volatile string? _currentTimestamp;

    public FightSimulationService(
        IPricingSnapshotFactory factory,
        IActiveSnapshotStore store,
        IConfiguration configuration,
        ILogger<FightSimulationService> logger)
    {
        _factory = factory;
        _store = store;
        _logger = logger;

        _mockDataDirectory = configuration["Simulation:MockDataDirectory"]
                             ?? Path.Combine(AppContext.BaseDirectory, "mock-data");

        ScanFiles();
    }

    private void ScanFiles()
    {
        if (!Directory.Exists(_mockDataDirectory))
        {
            _logger.LogWarning("Mock-data directory not found at {Dir}. Simulation unavailable.", _mockDataDirectory);
            return;
        }

        _sortedFiles = Directory.GetFiles(_mockDataDirectory, "*.csv")
            .OrderBy(f => Path.GetFileName(f))
            .ToArray();

        _logger.LogInformation("Fight simulation: found {Count} CSV files in {Dir}", _sortedFiles.Length, _mockDataDirectory);
    }

    public SimulationStatus GetStatus() => BuildStatus();

    public SimulationStatus Start(double speed)
    {
        lock (_lock)
        {
            if (_sortedFiles.Length == 0)
            {
                ScanFiles();
                if (_sortedFiles.Length == 0)
                    throw new InvalidOperationException("No mock-data CSV files found. Cannot start simulation.");
            }

            if (_isPlaying)
            {
                _speed = speed;
                return BuildStatus();
            }

            _speed = speed;
            _currentIndex = 0;
            _currentTimestamp = null;
            _cts = new CancellationTokenSource();

            var token = _cts.Token;
            _isPlaying = true;
            _runningTask = Task.Run(() => RunLoop(token), token);

            _logger.LogInformation("Fight simulation started at {Speed}x speed ({Files} files)", speed, _sortedFiles.Length);
            return BuildStatus();
        }
    }

    public SimulationStatus Stop()
    {
        lock (_lock)
        {
            if (!_isPlaying) return BuildStatus();

            _cts?.Cancel();
            _isPlaying = false;
            _logger.LogInformation("Fight simulation stopped at index {Index}/{Total}", _currentIndex, _sortedFiles.Length);
            return BuildStatus();
        }
    }

    private async Task RunLoop(CancellationToken ct)
    {
        try
        {
            for (var i = 0; i < _sortedFiles.Length && !ct.IsCancellationRequested; i++)
            {
                _currentIndex = i;
                var file = _sortedFiles[i];
                _currentTimestamp = Path.GetFileNameWithoutExtension(file).Replace(".csv", "");

                try
                {
                    FeedSnapshot(file);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to feed snapshot from {File}", Path.GetFileName(file));
                }

                var delayMs = (int)(5000.0 / _speed);
                await Task.Delay(delayMs, ct);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _isPlaying = false;
            _logger.LogInformation("Fight simulation loop ended at index {Index}/{Total}", _currentIndex, _sortedFiles.Length);
        }
    }

    private void FeedSnapshot(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        if (lines.Length < 2) return;

        var header = lines[0].Split(',');

        var bbStartIndex = -1;
        for (var c = 0; c < header.Length; c++)
        {
            if (header[c].StartsWith("bb_"))
            {
                bbStartIndex = c;
                break;
            }
        }

        if (bbStartIndex < 0)
        {
            _logger.LogWarning("No bb_ columns found in {File}", Path.GetFileName(filePath));
            return;
        }

        var firstDataRow = lines[1].Split(',');
        var snapshotId = firstDataRow.Length > 0 ? firstDataRow[0] : Path.GetFileNameWithoutExtension(filePath);
        var eventId = firstDataRow.Length > 1 ? firstDataRow[1] : "default";
        var modelVersion = firstDataRow.Length > 2 ? firstDataRow[2] : "1.0";

        var bbHeaders = header[bbStartIndex..];
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', bbHeaders));

        for (var r = 1; r < lines.Length; r++)
        {
            if (string.IsNullOrWhiteSpace(lines[r])) continue;
            var cols = lines[r].Split(',');
            if (cols.Length <= bbStartIndex) continue;
            sb.AppendLine(string.Join(',', cols[bbStartIndex..]));
        }

        var content = new SnapshotCsvContent
        {
            SnapshotId = snapshotId,
            OutcomeMatrixCsv = sb.ToString(),
            EventId = eventId,
            ModelVersion = modelVersion
        };

        var snapshot = _factory.BuildFromContent(content);
        _store.LoadSnapshot(snapshot);
        _store.SetActiveSnapshot(snapshot.SnapshotId);

        _logger.LogDebug("Simulation fed snapshot {Id}: {Legs} legs, {Scenarios} scenarios",
            snapshot.SnapshotId, snapshot.LegCount, snapshot.ScenarioCount);
    }

    private SimulationStatus BuildStatus() => new()
    {
        IsPlaying = _isPlaying,
        CurrentIndex = _currentIndex,
        TotalFiles = _sortedFiles.Length,
        Speed = _speed,
        CurrentTimestamp = _currentTimestamp,
        ElapsedFightSeconds = _currentIndex * 5.0
    };
}
