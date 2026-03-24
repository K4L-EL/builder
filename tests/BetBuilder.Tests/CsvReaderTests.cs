using BetBuilder.Infrastructure.Csv;

namespace BetBuilder.Tests;

public class CsvOutcomeMatrixReaderTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }

    private string WriteTempCsv(string content)
    {
        var path = TestHelpers.CreateTempCsvFile(content);
        _tempFiles.Add(path);
        return path;
    }

    [Fact]
    public void Read_ParsesHeaderAndRows()
    {
        var csv = "bb_red_win,bb_blue_win,bb_draw\n1,0,0\n0,1,0\n0,0,1\n";
        var path = WriteTempCsv(csv);

        var result = CsvOutcomeMatrixReader.Read(path);

        Assert.Equal(3, result.Legs.Count);
        Assert.Equal("bb_red_win", result.Legs[0]);
        Assert.Equal("bb_blue_win", result.Legs[1]);
        Assert.Equal("bb_draw", result.Legs[2]);
        Assert.Equal(3, result.Rows.Length);
        Assert.Equal(1, result.Rows[0][0]);
        Assert.Equal(0, result.Rows[0][1]);
    }

    [Fact]
    public void Read_DetectsUnavailableLegs()
    {
        var csv = "bb_red_win,bb_blue_win,bb_never\n1,0,0\n0,1,0\n1,0,0\n";
        var path = WriteTempCsv(csv);

        var result = CsvOutcomeMatrixReader.Read(path);

        Assert.Single(result.UnavailableLegs);
        Assert.Contains("bb_never", result.UnavailableLegs);
    }

    [Fact]
    public void Read_SkipsEmptyLines()
    {
        var csv = "bb_red_win,bb_blue_win\n1,0\n\n0,1\n\n";
        var path = WriteTempCsv(csv);

        var result = CsvOutcomeMatrixReader.Read(path);

        Assert.Equal(2, result.Rows.Length);
    }

    [Fact]
    public void Read_ThrowsOnColumnCountMismatch()
    {
        var csv = "bb_red_win,bb_blue_win\n1,0\n0\n";
        var path = WriteTempCsv(csv);

        Assert.Throws<InvalidOperationException>(() => CsvOutcomeMatrixReader.Read(path));
    }
}

public class CsvLegProbReaderTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }

    private string WriteTempCsv(string content)
    {
        var path = TestHelpers.CreateTempCsvFile(content);
        _tempFiles.Add(path);
        return path;
    }

    [Fact]
    public void Read_ParsesProbabilities()
    {
        var csv = "leg,probability\nbb_red_win,0.65\nbb_blue_win,0.30\nbb_draw,0.05\n";
        var path = WriteTempCsv(csv);

        var result = CsvLegProbReader.Read(path);

        Assert.Equal(3, result.Probabilities.Count);
        Assert.Equal(0.65, result.Probabilities["bb_red_win"]);
        Assert.Equal(0.30, result.Probabilities["bb_blue_win"]);
        Assert.Equal(0.05, result.Probabilities["bb_draw"]);
    }

    [Fact]
    public void AlignToIndex_MapsCorrectly()
    {
        var csv = "leg,probability\nbb_red_win,0.65\nbb_blue_win,0.30\n";
        var path = WriteTempCsv(csv);

        var data = CsvLegProbReader.Read(path);
        var legs = new[] { "bb_blue_win", "bb_red_win" };
        var indexMap = new Dictionary<string, int> { ["bb_blue_win"] = 0, ["bb_red_win"] = 1 };

        var aligned = CsvLegProbReader.AlignToIndex(data, legs, indexMap);

        Assert.Equal(0.30, aligned[0]);
        Assert.Equal(0.65, aligned[1]);
    }
}

public class CsvCorrelationMatrixReaderTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }

    private string WriteTempCsv(string content)
    {
        var path = TestHelpers.CreateTempCsvFile(content);
        _tempFiles.Add(path);
        return path;
    }

    [Fact]
    public void Read_ParsesSquareMatrix()
    {
        var csv = ",bb_a,bb_b\nbb_a,1.0,0.5\nbb_b,0.5,1.0\n";
        var path = WriteTempCsv(csv);

        var result = CsvCorrelationMatrixReader.Read(path);

        Assert.Equal(2, result.Legs.Count);
        Assert.Equal(1.0, result.Matrix[0, 0]);
        Assert.Equal(0.5, result.Matrix[0, 1]);
        Assert.Equal(0.5, result.Matrix[1, 0]);
        Assert.Equal(1.0, result.Matrix[1, 1]);
    }

    [Fact]
    public void Read_HandlesEmptyCellsAsNull()
    {
        var csv = ",bb_a,bb_b\nbb_a,1.0,\nbb_b,,1.0\n";
        var path = WriteTempCsv(csv);

        var result = CsvCorrelationMatrixReader.Read(path);

        Assert.Null(result.Matrix[0, 1]);
        Assert.Null(result.Matrix[1, 0]);
        Assert.Equal(1.0, result.Matrix[0, 0]);
    }
}
