using BetBuilder.Application.Interfaces;
using BetBuilder.Domain;
using BetBuilder.Infrastructure.Csv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BetBuilder.Infrastructure.Simulation;

/// <summary>
/// Loads and broadcasts live stats CSV slices. Mirrors the matrix ingestion pattern
/// (CSV reader -> domain object -> push downstream) but stats go over the broadcaster,
/// not the pricing snapshot store.
/// </summary>
public interface IStatsFeedService : IStatsFeedAccessor
{

    /// <summary>
    /// Directory on disk where stats CSVs live. Exposed so the simulator can
    /// discover files in lockstep with the matrix files.
    /// </summary>
    string StatsDirectory { get; }

    /// <summary>
    /// List of stats CSVs in <see cref="StatsDirectory"/> sorted by filename; empty
    /// array when the directory is missing or has no CSVs.
    /// </summary>
    IReadOnlyList<string> GetSortedFiles();

    /// <summary>
    /// Read the given stats CSV, cache the row as <see cref="Current"/>, and broadcast
    /// a <c>statsUpdate</c> to the fight group. If the CSV has multiple rows, the
    /// last row is used (simulator already steps per file, so we treat each file as one tick).
    /// </summary>
    Task FeedFromFile(string filePath, string fightId, CancellationToken ct = default);

    /// <summary>
    /// Parse and cache a stats row from in-memory CSV content without broadcasting.
    /// Used by the resulting service / admin tooling.
    /// </summary>
    FightStatsSnapshot IngestContent(string csvContent);

    void Reset();
}

public sealed class StatsFeedService : IStatsFeedService
{
    private readonly IFightBroadcaster _broadcaster;
    private readonly ILogger<StatsFeedService> _logger;
    private readonly string _statsDirectory;

    private volatile FightStatsSnapshot? _current;

    public StatsFeedService(
        IFightBroadcaster broadcaster,
        IConfiguration configuration,
        ILogger<StatsFeedService> logger)
    {
        _broadcaster = broadcaster;
        _logger = logger;

        _statsDirectory = configuration["Stats:MockDataDirectory"]
                          ?? configuration["Simulation:MockDataDirectory"]
                          ?? Path.Combine(AppContext.BaseDirectory, "Mock-stats");
    }

    public FightStatsSnapshot? Current => _current;
    public string StatsDirectory => _statsDirectory;

    public IReadOnlyList<string> GetSortedFiles()
    {
        if (!Directory.Exists(_statsDirectory))
        {
            _logger.LogDebug("Stats directory not found at {Dir}. Stats feed inactive.", _statsDirectory);
            return Array.Empty<string>();
        }

        return Directory.GetFiles(_statsDirectory, "*.csv")
            .OrderBy(Path.GetFileName)
            .ToArray();
    }

    public async Task FeedFromFile(string filePath, string fightId, CancellationToken ct = default)
    {
        try
        {
            var stats = CsvStatsReader.Read(filePath);
            _current = stats;
            await _broadcaster.StatsUpdate(fightId, stats);
            _logger.LogDebug("Stats fed from {File}: {MetricCount} metrics, fightId={FightId}",
                Path.GetFileName(filePath), stats.Metrics.Count, fightId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to feed stats from {File}", Path.GetFileName(filePath));
        }
    }

    public FightStatsSnapshot IngestContent(string csvContent)
    {
        var stats = CsvStatsReader.ParseFromContent(csvContent);
        _current = stats;
        return stats;
    }

    public void Reset() => _current = null;
}
