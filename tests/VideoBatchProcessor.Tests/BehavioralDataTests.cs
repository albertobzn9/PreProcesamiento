using System.Globalization;
using VideoBatchProcessor.Core.BehavioralData;

namespace VideoBatchProcessor.Tests;

public sealed class BehavioralDataTests : IDisposable
{
    private const string MainHeader = CsvV1BehavioralSessionReader.MainHeader;
    private const string PressesHeader = CsvV1BehavioralSessionReader.PressesHeader;
    private readonly string _directory = Directory.CreateTempSubdirectory("vbp-behavioral-").FullName;

    [Fact]
    public void SourceResolver_PrefersValidCsvOverMat_AndFindsOptionalPresses()
    {
        var video = Path.Combine(_directory, "exp_0126_dis_d1r1.mp4");
        var csv = Path.ChangeExtension(video, ".csv");
        var mat = Path.ChangeExtension(video, ".mat");
        var presses = Path.Combine(_directory, "exp_0126_dis_d1r1_palanqueos.csv");
        File.WriteAllText(video, string.Empty);
        File.WriteAllText(csv, MainHeader + Environment.NewLine + MainRow());
        File.WriteAllText(mat, "legacy binary placeholder");
        File.WriteAllText(presses, PressesHeader + Environment.NewLine + PressRow());

        var source = new BehavioralSourceResolver().Resolve(video);

        Assert.True(source.IsResolved);
        Assert.Equal(BehavioralSourceKind.CsvV1, source.SourceKind);
        Assert.Equal(csv, source.SourcePath);
        Assert.Equal(presses, source.PressesPath);
    }

    [Fact]
    public void SourceResolver_InvalidCsvHeader_FallsBackToMatWithWarning()
    {
        var video = Path.Combine(_directory, "exp_0126_dis_d1r2.mp4");
        var csv = Path.ChangeExtension(video, ".csv");
        var mat = Path.ChangeExtension(video, ".mat");
        File.WriteAllText(video, string.Empty);
        File.WriteAllText(csv, "wrong,header");
        File.WriteAllText(mat, "legacy binary placeholder");

        var source = new BehavioralSourceResolver().Resolve(video);

        Assert.True(source.IsResolved);
        Assert.Equal(BehavioralSourceKind.LegacyMat, source.SourceKind);
        Assert.Equal(mat, source.SourcePath);
        Assert.Contains(source.Warnings, warning => warning.Contains("se ignoró", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SourceResolver_ExplicitInvalidCsv_DoesNotSilentlyFallBack()
    {
        var video = Path.Combine(_directory, "exp_0126_dis_d1r3.mp4");
        var csv = Path.ChangeExtension(video, ".csv");
        var mat = Path.ChangeExtension(video, ".mat");
        File.WriteAllText(video, string.Empty);
        File.WriteAllText(csv, "wrong,header");
        File.WriteAllText(mat, "legacy binary placeholder");

        var source = new BehavioralSourceResolver().Resolve(video, csv);

        Assert.False(source.IsResolved);
        Assert.NotNull(source.Error);
        Assert.Null(source.SourcePath);
    }

    [Fact]
    public void SourceResolver_NeverUsesPressesCsvAsMainSource()
    {
        var video = Path.Combine(_directory, "exp_0126_dis_d1r4.mp4");
        var presses = Path.Combine(_directory, "exp_0126_dis_d1r4_palanqueos.csv");
        File.WriteAllText(video, string.Empty);
        File.WriteAllText(presses, PressesHeader + Environment.NewLine + PressRow());

        var source = new BehavioralSourceResolver().Resolve(video);

        Assert.False(source.IsResolved);
        Assert.Null(source.SourcePath);
    }

    [Fact]
    public void CsvReader_ParsesSoundOnly_PreservesRawOrder_AndKeepsNaTrial()
    {
        var video = Path.Combine(_directory, "exp_0126_dis_d2r1.mp4");
        var csv = Path.ChangeExtension(video, ".csv");
        var presses = Path.Combine(_directory, "exp_0126_dis_d2r1_palanqueos.csv");
        File.WriteAllText(video, string.Empty);
        File.WriteAllText(csv, MainHeader + Environment.NewLine + MainRow("2", "1.500000"));
        File.WriteAllText(presses, PressesHeader + Environment.NewLine + PressRow("NA"));

        var source = new BehavioralSourceResolver().Resolve(video);
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-MX");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("es-MX");

        try
        {
            var data = new CsvV1BehavioralSessionReader().Read(source);

            var behavioralEvent = Assert.Single(data.Events);
            Assert.Equal(BehavioralEventType.SoundOnly, behavioralEvent.EventType);
            Assert.Equal(1.5, behavioralEvent.LeverLatencySeconds, precision: 6);
            Assert.Equal("1.500000", behavioralEvent.RawValues[3]);
            Assert.Equal(1, behavioralEvent.Side); // 1 = izquierda, validado en CajaValentia.

            var press = Assert.Single(data.Presses);
            Assert.Null(press.TrialNumber);
            Assert.Equal("NA", press.RawValues[3]);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public void CsvReader_RejectsMalformedMainRow()
    {
        var video = Path.Combine(_directory, "exp_0126_dis_d2r2.mp4");
        var csv = Path.ChangeExtension(video, ".csv");
        File.WriteAllText(video, string.Empty);
        File.WriteAllText(csv, MainHeader + Environment.NewLine + "1,0,1,1.0,2.0,0,1,3.0");

        var source = new BehavioralSourceResolver().Resolve(video);

        var exception = Assert.Throws<BehavioralDataFormatException>(() =>
            new CsvV1BehavioralSessionReader().Read(source));
        Assert.Contains("exactamente 9 columnas", exception.Message);
    }

    [Fact]
    public void LegacyMatMapper_MapsEightColumns_AndDerivesTypeFromStimulus()
    {
        IReadOnlyList<double>[] rows =
        [
            [1, 1, 0, 5.5, 100.5, 1, 0, 2.2],
            [2, -2, 1, 30, 130, 1, 0, 30],
        ];

        var events = LegacyMatEventMapper.MapRows(rows);

        Assert.Equal(BehavioralEventType.SafeFood, events[0].EventType);
        Assert.Equal(BehavioralEventType.ConflictWithFood, events[1].EventType);
        Assert.Equal(-2, events[1].Side);
    }

    [Fact]
    public void LegacyMatMapper_MapsNineColumns_AndUsesExplicitEventType()
    {
        IReadOnlyList<double>[] rows =
        [
            [1, 0, 1, 180, 280, 2, 1, 8.2, 2],
        ];

        var behavioralEvent = Assert.Single(LegacyMatEventMapper.MapRows(rows));

        Assert.Equal(BehavioralEventType.SoundOnly, behavioralEvent.EventType);
        Assert.Equal(0, behavioralEvent.Side); // 0 = derecha, validado en CajaValentia.
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private static string MainRow(string eventType = "1", string latency = "6.250000") =>
        $"1,1,1,{latency},120.000000,2,3,4.500000,{eventType}";

    private static string PressRow(string trial = "1") =>
        $"1,120.000,dis,{trial},riesgo,1,2,7";
}
