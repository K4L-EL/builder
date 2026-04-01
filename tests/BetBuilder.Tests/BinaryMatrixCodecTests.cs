using BetBuilder.Infrastructure.Binary;

namespace BetBuilder.Tests;

public class BinaryMatrixCodecTests
{
    [Fact]
    public void PackAndUnpack_RoundTrips()
    {
        var legs = new[] { "a", "b", "c", "d", "e" };
        var rows = new[]
        {
            new byte[] { 1, 0, 0, 1, 0 },
            new byte[] { 0, 1, 1, 0, 1 },
            new byte[] { 1, 1, 1, 1, 1 },
            new byte[] { 0, 0, 0, 0, 0 },
        };

        var packed = BinaryMatrixCodec.Pack(rows, legs.Length);
        var result = BinaryMatrixCodec.Unpack(legs, packed, rows.Length);

        Assert.Equal(legs, result.Legs);
        Assert.Equal(rows.Length, result.Rows.Length);
        for (var r = 0; r < rows.Length; r++)
            Assert.Equal(rows[r], result.Rows[r]);
    }

    [Fact]
    public void Pack_5Legs_Uses1BytePerRow()
    {
        Assert.Equal(1, BinaryMatrixCodec.BytesPerRow(5));

        var rows = new[] { new byte[] { 1, 0, 1, 0, 1 } };
        var packed = BinaryMatrixCodec.Pack(rows, 5);

        Assert.Single(packed);
        Assert.Equal(0b10101, packed[0]);
    }

    [Fact]
    public void Pack_9Legs_Uses2BytesPerRow()
    {
        Assert.Equal(2, BinaryMatrixCodec.BytesPerRow(9));

        var row = new byte[] { 1, 1, 1, 1, 1, 1, 1, 1, 1 };
        var packed = BinaryMatrixCodec.Pack(new[] { row }, 9);

        Assert.Equal(2, packed.Length);
        Assert.Equal(0xFF, packed[0]);
        Assert.Equal(0x01, packed[1]);
    }

    [Fact]
    public void Pack_46Legs_Uses6BytesPerRow()
    {
        Assert.Equal(6, BinaryMatrixCodec.BytesPerRow(46));
    }

    [Fact]
    public void Unpack_DetectsUnavailableLegs()
    {
        var legs = new[] { "a", "b", "c" };
        var rows = new[]
        {
            new byte[] { 1, 0, 0 },
            new byte[] { 1, 0, 0 },
        };

        var packed = BinaryMatrixCodec.Pack(rows, 3);
        var result = BinaryMatrixCodec.Unpack(legs, packed, 2);

        Assert.Contains("b", result.UnavailableLegs);
        Assert.Contains("c", result.UnavailableLegs);
        Assert.DoesNotContain("a", result.UnavailableLegs);
    }

    [Fact]
    public void Unpack_ThrowsOnWrongByteCount()
    {
        var legs = new[] { "a", "b", "c" };
        var badPacked = new byte[] { 0xFF };

        Assert.Throws<InvalidOperationException>(() =>
            BinaryMatrixCodec.Unpack(legs, badPacked, 5));
    }

    [Fact]
    public void Pack_LargeMatrix_5000x46()
    {
        var legCount = 46;
        var scenarioCount = 5000;
        var rng = new Random(42);
        var legs = Enumerable.Range(0, legCount).Select(i => $"leg_{i}").ToArray();

        var rows = new byte[scenarioCount][];
        for (var r = 0; r < scenarioCount; r++)
        {
            rows[r] = new byte[legCount];
            for (var c = 0; c < legCount; c++)
                rows[r][c] = (byte)(rng.NextDouble() < 0.5 ? 1 : 0);
        }

        var packed = BinaryMatrixCodec.Pack(rows, legCount);

        Assert.Equal(scenarioCount * 6, packed.Length);

        var result = BinaryMatrixCodec.Unpack(legs, packed, scenarioCount);
        for (var r = 0; r < scenarioCount; r++)
            Assert.Equal(rows[r], result.Rows[r]);
    }
}
