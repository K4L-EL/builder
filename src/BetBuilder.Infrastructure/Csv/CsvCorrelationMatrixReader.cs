using System.Globalization;

namespace BetBuilder.Infrastructure.Csv;

public sealed class CorrelationMatrixData
{
    public IReadOnlyList<string> Legs { get; init; } = default!;
    public double?[,] Matrix { get; init; } = default!;
}

public static class CsvCorrelationMatrixReader
{
    public static CorrelationMatrixData Read(string filePath) =>
        Parse(File.ReadAllLines(filePath), filePath);

    public static CorrelationMatrixData ParseFromContent(string csvContent) =>
        Parse(csvContent.Split('\n'), "uploaded content");

    /// <summary>
    /// Creates an empty correlation matrix (all nulls) for when no correlation data is provided.
    /// </summary>
    public static CorrelationMatrixData Empty(IReadOnlyList<string> legs) =>
        new()
        {
            Legs = legs,
            Matrix = new double?[legs.Count, legs.Count]
        };

    private static CorrelationMatrixData Parse(string[] lines, string source)
    {
        if (lines.Length < 2)
            throw new InvalidOperationException($"Correlation matrix ({source}) has no data rows.");

        var headerParts = lines[0].Trim().Split(',');
        var legs = new string[headerParts.Length - 1];
        for (var i = 1; i < headerParts.Length; i++)
            legs[i - 1] = headerParts[i].Trim();

        var size = legs.Length;
        var matrix = new double?[size, size];

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split(',');
            var rowIndex = i - 1;

            for (var j = 1; j < parts.Length && (j - 1) < size; j++)
            {
                var cell = parts[j].Trim();
                if (string.IsNullOrEmpty(cell) ||
                    cell.Equals("nan", StringComparison.OrdinalIgnoreCase) ||
                    cell.Equals("NaN", StringComparison.Ordinal))
                {
                    matrix[rowIndex, j - 1] = null;
                }
                else
                {
                    matrix[rowIndex, j - 1] = double.Parse(cell, CultureInfo.InvariantCulture);
                }
            }
        }

        return new CorrelationMatrixData
        {
            Legs = legs,
            Matrix = matrix
        };
    }

    public static double?[,] AlignToIndex(
        CorrelationMatrixData data,
        IReadOnlyList<string> legs,
        IReadOnlyDictionary<string, int> legIndexMap)
    {
        var size = legs.Count;
        var aligned = new double?[size, size];

        var sourceIndexMap = new Dictionary<string, int>(data.Legs.Count);
        for (var i = 0; i < data.Legs.Count; i++)
            sourceIndexMap[data.Legs[i]] = i;

        for (var i = 0; i < size; i++)
        {
            for (var j = 0; j < size; j++)
            {
                if (sourceIndexMap.TryGetValue(legs[i], out var srcI) &&
                    sourceIndexMap.TryGetValue(legs[j], out var srcJ))
                {
                    aligned[i, j] = data.Matrix[srcI, srcJ];
                }
            }
        }

        return aligned;
    }
}
