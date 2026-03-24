using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BetBuilder.Infrastructure.Snapshots;

public sealed class DataSettings
{
    public const string SectionName = "Data";
    public string DataDirectory { get; set; } = "data";
    public string DefaultSnapshot { get; set; } = "ts0";
}

public sealed class LocalSnapshotSource : ISnapshotSource
{
    private static readonly Regex FilePattern = new(
        @"^(outcome_matrix|leg_probs|correlation_matrix)_(ts\d+)\.csv$",
        RegexOptions.Compiled);

    private readonly DataSettings _settings;
    private readonly ILogger<LocalSnapshotSource> _logger;

    public LocalSnapshotSource(IOptions<DataSettings> settings, ILogger<LocalSnapshotSource> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public IReadOnlyList<SnapshotFileGroup> DiscoverSnapshots()
    {
        var dir = _settings.DataDirectory;
        if (!Directory.Exists(dir))
        {
            _logger.LogWarning("Data directory not found: {Directory}, returning empty set", dir);
            return Array.Empty<SnapshotFileGroup>();
        }

        var files = Directory.GetFiles(dir, "*.csv");
        var groups = new Dictionary<string, (string? outcome, string? prob, string? corr)>();

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var match = FilePattern.Match(fileName);
            if (!match.Success)
                continue;

            var fileType = match.Groups[1].Value;
            var snapshotId = match.Groups[2].Value;

            if (!groups.TryGetValue(snapshotId, out var group))
                group = (null, null, null);

            group = fileType switch
            {
                "outcome_matrix" => (file, group.prob, group.corr),
                "leg_probs" => (group.outcome, file, group.corr),
                "correlation_matrix" => (group.outcome, group.prob, file),
                _ => group
            };

            groups[snapshotId] = group;
        }

        var result = new List<SnapshotFileGroup>();
        foreach (var (snapshotId, (outcome, prob, corr)) in groups.OrderBy(g => g.Key))
        {
            if (outcome == null || prob == null || corr == null)
            {
                _logger.LogWarning("Snapshot {SnapshotId} is incomplete, skipping", snapshotId);
                continue;
            }

            result.Add(new SnapshotFileGroup
            {
                SnapshotId = snapshotId,
                OutcomeMatrixPath = outcome,
                LegProbsPath = prob,
                CorrelationMatrixPath = corr
            });
        }

        _logger.LogInformation("Discovered {Count} complete snapshot groups in {Directory}", result.Count, dir);
        return result;
    }
}
