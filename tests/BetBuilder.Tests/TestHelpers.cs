using BetBuilder.Domain;

namespace BetBuilder.Tests;

public static class TestHelpers
{
    public static PricingSnapshot CreateSnapshot(
        string snapshotId = "test",
        string[]? legs = null,
        byte[][]? outcomeRows = null,
        double[]? probabilities = null,
        HashSet<string>? unavailable = null)
    {
        legs ??= new[] { "bb_red_win", "bb_blue_win", "bb_draw", "bb_red_ko", "bb_red_decision" };

        outcomeRows ??= new[]
        {
            new byte[] { 1, 0, 0, 1, 0 },
            new byte[] { 1, 0, 0, 1, 0 },
            new byte[] { 1, 0, 0, 0, 1 },
            new byte[] { 0, 1, 0, 0, 0 },
            new byte[] { 0, 0, 1, 0, 0 },
            new byte[] { 1, 0, 0, 1, 0 },
            new byte[] { 1, 0, 0, 0, 1 },
            new byte[] { 1, 0, 0, 0, 1 },
            new byte[] { 1, 0, 0, 1, 0 },
            new byte[] { 0, 1, 0, 0, 0 },
        };

        var legIndexMap = new Dictionary<string, int>();
        for (var i = 0; i < legs.Length; i++)
            legIndexMap[legs[i]] = i;

        probabilities ??= new double[legs.Length];
        if (probabilities.Length == legs.Length && probabilities.All(p => p == 0))
        {
            for (var col = 0; col < legs.Length; col++)
            {
                var sum = 0;
                for (var row = 0; row < outcomeRows.Length; row++)
                    sum += outcomeRows[row][col];
                probabilities[col] = (double)sum / outcomeRows.Length;
            }
        }

        unavailable ??= new HashSet<string>();

        for (var col = 0; col < legs.Length; col++)
        {
            var allZero = true;
            for (var row = 0; row < outcomeRows.Length; row++)
            {
                if (outcomeRows[row][col] != 0) { allZero = false; break; }
            }
            if (allZero) unavailable.Add(legs[col]);
        }

        return new PricingSnapshot
        {
            SnapshotId = snapshotId,
            EventId = "test_event",
            ModelVersion = "1.0",
            GeneratedAtUtc = DateTime.UtcNow,
            Legs = legs,
            LegIndexMap = legIndexMap,
            LegProbabilities = probabilities,
            CorrelationMatrix = new double?[legs.Length, legs.Length],
            OutcomeMatrix = outcomeRows,
            UnavailableLegs = unavailable
        };
    }

    public static string CreateTempCsvFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"betbuilder_test_{Guid.NewGuid()}.csv");
        File.WriteAllText(path, content);
        return path;
    }
}
