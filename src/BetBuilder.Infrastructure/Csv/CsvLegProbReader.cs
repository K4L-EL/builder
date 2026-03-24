namespace BetBuilder.Infrastructure.Csv;

public sealed class LegProbData
{
    public IReadOnlyDictionary<string, double> Probabilities { get; init; } = default!;
}

public static class CsvLegProbReader
{
    public static LegProbData Read(string filePath) =>
        Parse(File.ReadAllLines(filePath), filePath);

    public static LegProbData ParseFromContent(string csvContent) =>
        Parse(csvContent.Split('\n'), "uploaded content");

    /// <summary>
    /// Derives leg probabilities directly from an outcome matrix (column means).
    /// Used when leg_probs CSV is not provided in an upload.
    /// </summary>
    public static LegProbData DeriveFromOutcomeMatrix(OutcomeMatrixData outcomeData)
    {
        var probs = new Dictionary<string, double>(outcomeData.Legs.Count);
        var rowCount = outcomeData.Rows.Length;

        for (var col = 0; col < outcomeData.Legs.Count; col++)
        {
            var sum = 0;
            for (var row = 0; row < rowCount; row++)
                sum += outcomeData.Rows[row][col];

            probs[outcomeData.Legs[col]] = rowCount > 0 ? (double)sum / rowCount : 0;
        }

        return new LegProbData { Probabilities = probs };
    }

    private static LegProbData Parse(string[] lines, string source)
    {
        if (lines.Length < 2)
            throw new InvalidOperationException($"Leg prob ({source}) has no data rows.");

        var probs = new Dictionary<string, double>(lines.Length - 1);

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Trim().Split(',', 2);
            if (parts.Length != 2)
                throw new InvalidOperationException(
                    $"Row {i} in ({source}) does not have exactly 2 columns.");

            var leg = parts[0].Trim();
            var prob = double.Parse(parts[1].Trim(), System.Globalization.CultureInfo.InvariantCulture);
            probs[leg] = prob;
        }

        return new LegProbData { Probabilities = probs };
    }

    public static double[] AlignToIndex(
        LegProbData data,
        IReadOnlyList<string> legs,
        IReadOnlyDictionary<string, int> legIndexMap)
    {
        var result = new double[legs.Count];
        for (var i = 0; i < legs.Count; i++)
        {
            if (data.Probabilities.TryGetValue(legs[i], out var prob))
                result[i] = prob;
        }

        return result;
    }
}
