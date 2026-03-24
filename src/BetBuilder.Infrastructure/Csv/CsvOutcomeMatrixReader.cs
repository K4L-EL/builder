namespace BetBuilder.Infrastructure.Csv;

public sealed class OutcomeMatrixData
{
    public IReadOnlyList<string> Legs { get; init; } = default!;
    public byte[][] Rows { get; init; } = default!;
    public IReadOnlySet<string> UnavailableLegs { get; init; } = default!;
}

public static class CsvOutcomeMatrixReader
{
    public static OutcomeMatrixData Read(string filePath) =>
        Parse(File.ReadAllLines(filePath), filePath);

    public static OutcomeMatrixData ParseFromContent(string csvContent) =>
        Parse(csvContent.Split('\n'), "uploaded content");

    private static OutcomeMatrixData Parse(string[] lines, string source)
    {
        if (lines.Length < 2)
            throw new InvalidOperationException($"Outcome matrix ({source}) has no data rows.");

        var legs = lines[0].Trim().Split(',', StringSplitOptions.None);
        var legCount = legs.Length;
        var rows = new List<byte[]>(lines.Length - 1);

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Trim().Split(',', StringSplitOptions.None);
            if (parts.Length != legCount)
                throw new InvalidOperationException(
                    $"Row {i} in ({source}) has {parts.Length} columns, expected {legCount}.");

            var row = new byte[legCount];
            for (var j = 0; j < legCount; j++)
                row[j] = byte.Parse(parts[j]);

            rows.Add(row);
        }

        var unavailable = DetectUnavailableLegs(legs, rows);

        return new OutcomeMatrixData
        {
            Legs = legs,
            Rows = rows.ToArray(),
            UnavailableLegs = unavailable
        };
    }

    private static IReadOnlySet<string> DetectUnavailableLegs(
        IReadOnlyList<string> legs,
        List<byte[]> rows)
    {
        var unavailable = new HashSet<string>();

        for (var col = 0; col < legs.Count; col++)
        {
            var allZero = true;
            for (var row = 0; row < rows.Count; row++)
            {
                if (rows[row][col] != 0)
                {
                    allZero = false;
                    break;
                }
            }

            if (allZero)
                unavailable.Add(legs[col]);
        }

        return unavailable;
    }
}
