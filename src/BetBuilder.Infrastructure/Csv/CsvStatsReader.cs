using System.Globalization;
using BetBuilder.Domain;

namespace BetBuilder.Infrastructure.Csv;

/// <summary>
/// Schema-agnostic stats CSV reader. Mirrors <see cref="CsvOutcomeMatrixReader"/>
/// but doesn't assume any particular column layout beyond:
/// - header row, comma-separated
/// - at least one data row
/// - optional well-known columns: <c>snapshot_id</c>, <c>event_id</c>, <c>elapsed_seconds</c>
/// All other numeric columns become entries in <see cref="FightStatsSnapshot.Metrics"/>.
/// Columns matching <c>bb_&lt;leg&gt;_result</c> become <see cref="FightStatsSnapshot.LegResults"/>.
/// </summary>
public static class CsvStatsReader
{
    private const string LegResultSuffix = "_result";

    public static FightStatsSnapshot Read(string filePath) =>
        Parse(File.ReadAllLines(filePath), filePath);

    public static FightStatsSnapshot ParseFromContent(string csvContent) =>
        Parse(csvContent.Split('\n'), "uploaded content");

    public static IReadOnlyList<FightStatsSnapshot> ReadAll(string filePath) =>
        ParseAll(File.ReadAllLines(filePath), filePath);

    private static FightStatsSnapshot Parse(string[] lines, string source)
    {
        var all = ParseAll(lines, source);
        if (all.Count == 0)
            throw new InvalidOperationException($"Stats CSV ({source}) has no data rows.");
        return all[^1];
    }

    private static IReadOnlyList<FightStatsSnapshot> ParseAll(string[] lines, string source)
    {
        if (lines.Length < 2)
            return Array.Empty<FightStatsSnapshot>();

        var header = lines[0].Trim().Split(',');
        var colIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Length; i++)
            colIndex[header[i].Trim()] = i;

        int? snapshotIdCol = colIndex.TryGetValue("snapshot_id", out var si) ? si : null;
        int? eventIdCol = colIndex.TryGetValue("event_id", out var ei) ? ei : null;
        int? elapsedCol = colIndex.TryGetValue("elapsed_seconds", out var es) ? es
            : colIndex.TryGetValue("fight_time_s", out var fts) ? fts
            : null;

        var results = new List<FightStatsSnapshot>(lines.Length - 1);

        for (var r = 1; r < lines.Length; r++)
        {
            var line = lines[r];
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = line.Trim().Split(',');

            var metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var legResults = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (var c = 0; c < header.Length && c < cols.Length; c++)
            {
                var name = header[c].Trim();
                if (name.Length == 0) continue;
                var raw = cols[c].Trim();
                if (raw.Length == 0) continue;

                if (name.StartsWith("bb_", StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith(LegResultSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    var leg = name[..^LegResultSuffix.Length];
                    if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r2))
                        legResults[leg] = r2;
                    continue;
                }

                if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
                    metrics[name] = num;
            }

            results.Add(new FightStatsSnapshot
            {
                SnapshotId = snapshotIdCol.HasValue && snapshotIdCol.Value < cols.Length
                    ? cols[snapshotIdCol.Value]
                    : $"row{r}",
                EventId = eventIdCol.HasValue && eventIdCol.Value < cols.Length
                    ? cols[eventIdCol.Value]
                    : "default",
                ElapsedSeconds = elapsedCol.HasValue && elapsedCol.Value < cols.Length
                    && double.TryParse(cols[elapsedCol.Value], NumberStyles.Float, CultureInfo.InvariantCulture, out var el)
                    ? el
                    : (r - 1) * 5.0,
                CapturedAtUtc = DateTime.UtcNow,
                Metrics = metrics,
                LegResults = legResults
            });
        }

        return results;
    }
}
