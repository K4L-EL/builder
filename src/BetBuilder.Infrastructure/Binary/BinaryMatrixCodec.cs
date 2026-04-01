using BetBuilder.Infrastructure.Csv;

namespace BetBuilder.Infrastructure.Binary;

/// <summary>
/// Packs/unpacks an outcome matrix as a bitfield.
/// Each scenario row is stored as ceil(legCount/8) bytes where each bit
/// represents one leg (1 = hit, 0 = miss). Bit 0 of byte 0 is leg 0, etc.
///
/// Wire format:
///   [legNames (string[])] + [packedRows (byte[])] where packedRows is
///   scenarioCount * bytesPerRow contiguous bytes.
/// </summary>
public static class BinaryMatrixCodec
{
    public static int BytesPerRow(int legCount) => (legCount + 7) / 8;

    public static byte[] Pack(byte[][] rows, int legCount)
    {
        var bpr = BytesPerRow(legCount);
        var packed = new byte[rows.Length * bpr];

        for (var r = 0; r < rows.Length; r++)
        {
            var offset = r * bpr;
            var row = rows[r];
            for (var leg = 0; leg < legCount; leg++)
            {
                if (row[leg] != 0)
                    packed[offset + (leg >> 3)] |= (byte)(1 << (leg & 7));
            }
        }

        return packed;
    }

    public static OutcomeMatrixData Unpack(
        IReadOnlyList<string> legs,
        byte[] packedRows,
        int scenarioCount)
    {
        var legCount = legs.Count;
        var bpr = BytesPerRow(legCount);

        if (packedRows.Length != scenarioCount * bpr)
            throw new InvalidOperationException(
                $"Expected {scenarioCount * bpr} bytes for {scenarioCount} scenarios × {legCount} legs, got {packedRows.Length}.");

        var rows = new byte[scenarioCount][];

        for (var r = 0; r < scenarioCount; r++)
        {
            var offset = r * bpr;
            var row = new byte[legCount];
            for (var leg = 0; leg < legCount; leg++)
            {
                row[leg] = (byte)((packedRows[offset + (leg >> 3)] >> (leg & 7)) & 1);
            }
            rows[r] = row;
        }

        var unavailable = DetectUnavailableLegs(legs, rows);

        return new OutcomeMatrixData
        {
            Legs = legs,
            Rows = rows,
            UnavailableLegs = unavailable
        };
    }

    private static IReadOnlySet<string> DetectUnavailableLegs(
        IReadOnlyList<string> legs, byte[][] rows)
    {
        var unavailable = new HashSet<string>();
        for (var col = 0; col < legs.Count; col++)
        {
            var allZero = true;
            for (var row = 0; row < rows.Length; row++)
            {
                if (rows[row][col] != 0) { allZero = false; break; }
            }
            if (allZero) unavailable.Add(legs[col]);
        }
        return unavailable;
    }
}
