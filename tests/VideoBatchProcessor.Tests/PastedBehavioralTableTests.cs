using VideoBatchProcessor.Core.BehavioralData;
using VideoBatchProcessor.Core.LightDetection;
using VideoBatchProcessor.Core.SessionPairing;

namespace VideoBatchProcessor.Tests;

public sealed class PastedBehavioralTableTests
{
    private const string Row = "1\t0\t1\t5.25\t105.25\t0\t1\t2.5";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Parse_MatchesMatEightColumnsWithOrWithoutHeaders(bool header)
    {
        var text = (header ? string.Join('\t', PastedBehavioralTable.Columns) + "\r\n" : "") + Row + "\r\n";
        var actual = Assert.Single(PastedBehavioralTable.Parse(text));
        var expected = Assert.Single(LegacyMatEventMapper.MapRows([new double[] { 1, 0, 1, 5.25, 105.25, 0, 1, 2.5 }]));
        Assert.Equal(expected with { RawValues = actual.RawValues }, actual);
        Assert.Equal(Row.Split('\t'), actual.RawValues);
    }

    [Fact]
    public void Parse_CommaDecimalsRequireExplicitSelectionAndKeepRawCells()
    {
        var text = Row.Replace('.', ',');
        Assert.Throws<BehavioralDataFormatException>(() => PastedBehavioralTable.Parse(text));
        var result = Assert.Single(PastedBehavioralTable.Parse(text, true));
        Assert.Equal(5.25, result.LeverLatencySeconds);
        Assert.Equal("5,25", result.RawValues[3]);
    }

    [Theory]
    [InlineData("", "Row 1")]
    [InlineData("1\t0\t1", "8 columns")]
    [InlineData("1\t0\t1\t5\t105\t0\t1\t2\t1", "8 columns")]
    [InlineData("1\t0\t1\t\t105\t0\t1\t2", "column 4")]
    [InlineData("1\t3\t1\t5\t105\t0\t1\t2", "column 2")]
    [InlineData("1\t0\t2\t5\t105\t0\t1\t2", "column 3")]
    [InlineData("1\t0\t1\tNaN\t105\t0\t1\t2", "column 4")]
    [InlineData("1\t0\t1\t5\t105\t0.5\t1\t2", "column 6")]
    [InlineData("1\t0\t1\t-5\t105\t0\t1\t2", "column 4")]
    [InlineData("1\t0\t1\t1,234.5\t105\t0\t1\t2", "column 4")]
    [InlineData("Ensayo\tLado\tEstim\tTiempoAbs\tLatencia\tPalancasIzq\tPalancasDer\tDesplaz", "Row 1")]
    public void Parse_ReportsBadCellsWithoutGuessing(string text, string error)
    {
        Assert.Contains(error, Assert.Throws<BehavioralDataFormatException>(() => PastedBehavioralTable.Parse(text)).Message);
    }

    [Fact]
    public void Parse_DoesNotDropBlankRowsOrReorderDuplicates()
    {
        Assert.Contains("Row 2", Assert.Throws<BehavioralDataFormatException>(() => PastedBehavioralTable.Parse(Row + "\n\n" + Row)).Message);
        Assert.Contains("duplicates", Assert.Throws<BehavioralDataFormatException>(() => PastedBehavioralTable.Parse(Row + "\n" + Row)).Message);
        Assert.Throws<BehavioralDataFormatException>(() => PastedBehavioralTable.Parse(string.Join('\t', PastedBehavioralTable.Columns)));
    }

    [Fact]
    public void Parse_PreservesTimeoutAndAllowsPartialSessionNumbering()
    {
        var item = Assert.Single(PastedBehavioralTable.Parse("21\t-2\t1\t300\t1000\t12\t15\t300"));
        Assert.Equal(21, item.EventNumber);
        Assert.Equal(-2, item.Side);
    }

    [Fact]
    public void Save_PreservesClipboardAndFeedsTheSamePairingAsMat()
    {
        var folder = Directory.CreateTempSubdirectory("vbp-paste-").FullName;
        try
        {
            var text = "1\t0\t0\t5\t105\t0\t1\t2\n2\t1\t0\t5\t125\t1\t1\t2\n3\t0\t0\t5\t145\t1\t2\t2";
            var video = Path.Combine(folder, "exp_0526_cs_d1r1.mp4");
            var path = PastedBehavioralTable.Save(folder, video, text, false);
            Assert.Equal(text, File.ReadAllText(Path.Combine(Path.GetDirectoryName(path)!, "clipboard.txt")));
            Assert.All(File.ReadAllLines(path), line => Assert.Equal(8, line.Split('\t').Length));
            Assert.NotEqual(path, PastedBehavioralTable.Save(folder, video, text, false));
            var resolution = new BehavioralSourceResolver().Resolve(video, path);
            Assert.Equal(BehavioralSourceKind.PastedTable, resolution.SourceKind);
            var source = new BehavioralPairingEvidenceReader().Read(path);
            var intervals = source.Events.Select(e => new LightEventInterval(e.EventNumber,
                e.Side == 0 ? LightId.FoodRight : LightId.FoodLeft,
                (int)((e.AbsoluteTimeSeconds - e.LeverLatencySeconds + 2.4) * 30),
                e.AbsoluteTimeSeconds - e.LeverLatencySeconds + 2.4,
                (int)((e.AbsoluteTimeSeconds + 2.4) * 30), e.AbsoluteTimeSeconds + 2.4)).ToArray();
            var pairing = new SessionPairingResolver().Resolve([new(video, intervals, new(0, 6000), 30)], [source]);
            Assert.Equal(1, pairing.ConfirmedCount);
            Assert.Equal(3, pairing.Resolutions[0].SelectedCandidate!.Synchronization.Comparison.MatchedCount);
            var originalMat = source with { SourcePath = Path.ChangeExtension(video, ".mat") };
            var videoEvidence = new VideoPairingEvidence(video, intervals, new(0, 6000), 30);
            var explicitChoice = new Dictionary<string, string> { [video] = path };
            var selected = new SessionPairingResolver().Resolve([videoEvidence], [source, originalMat], explicitChoice);
            Assert.Equal(1, selected.ConfirmedCount);
            Assert.Equal(path, selected.Resolutions[0].BehavioralSourcePath);
            var wrongTable = source with { Events = source.Events.Select(item => item with { Side = 1 - item.Side }).ToArray() };
            var rejected = new SessionPairingResolver().Resolve([videoEvidence], [wrongTable, originalMat], explicitChoice);
            Assert.Equal(0, rejected.ConfirmedCount);
        }
        finally { Directory.Delete(folder, true); }
    }
}
