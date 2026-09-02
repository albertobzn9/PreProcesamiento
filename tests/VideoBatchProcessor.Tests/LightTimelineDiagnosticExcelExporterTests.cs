using System.IO.Compression;
using ClosedXML.Excel;
using VideoBatchProcessor.Core.BehavioralData;
using VideoBatchProcessor.Core.Diagnostics;
using VideoBatchProcessor.Core.LightDetection;
using VideoBatchProcessor.Core.VideoReader;
using VideoBatchProcessor.Core.VideoTransform;

namespace VideoBatchProcessor.Tests;

public sealed class LightTimelineDiagnosticExcelExporterTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("vbp-light-diagnostic-").FullName;

    [Fact]
    public void Export_CreaLibroConPerfilReutilizableEIncluyeSegmentosPlaneados()
    {
        var output = Path.Combine(_directory, "diagnostico.xlsx");
        var timeline = new LightTimeline(
            SamplesAnalyzed: 300,
            Transitions:
            [
                new LightTransition(LightId.FoodLeft, false, true, 100, 3.333, 102),
                new LightTransition(LightId.FoodLeft, true, false, 200, 6.667, 202),
            ]);
        var report = new LightTimelineDiagnosticReport(
            new VideoMetadata
            {
                FilePath = "/tmp/sesion.mp4",
                Width = 1920,
                Height = 1080,
                Fps = 30,
                TotalFrames = 300,
                Duration = TimeSpan.FromSeconds(10),
            },
            new LightTimelineScanRange(0, 299),
            new VideoTransformConfig(),
            timeline,
            LightEventIntervalBuilder.Build(timeline),
            new LightDetectionConfig(
                new LightRoi(LightId.FoodLeft, 10, 10, 20, 20),
                new LightRoi(LightId.FoodRight, 40, 10, 20, 20),
                new LightRoi(LightId.NoiseLed, 70, 10, 20, 20)),
            [new BehavioralEvent(1, 0, 0, 5, 305, 0, 1, 0.1, BehavioralEventType.SafeFood, [])],
            "/tmp/sesion.mat",
            null);

        new LightTimelineDiagnosticExcelExporter().Export(report, output);

        Assert.True(File.Exists(output));
        using var archive = ZipFile.OpenRead(output);
        var workbookXml = archive.GetEntry("xl/workbook.xml");
        Assert.NotNull(workbookXml);
        using var reader = new StreamReader(workbookXml!.Open());
        var xml = reader.ReadToEnd();
        Assert.Contains("Resumen", xml);
        Assert.Contains("Perfil de camara", xml);
        Assert.Contains("Eventos video", xml);
        Assert.Contains("Cambios raw", xml);
        Assert.Contains("MAT conductual", xml);
        Assert.Contains("Comparacion", xml);
        Assert.Contains("Segmentos planeados", xml);

        using var workbook = new XLWorkbook(output);
        var segments = workbook.Worksheet("Segmentos planeados");
        Assert.Equal("Duración total (s)", segments.Cell(1, 9).GetString());
        Assert.Equal("Cruce por lado", segments.Cell(1, 12).GetString());
        Assert.Equal("Comparación de lado", segments.Cell(1, 13).GetString());
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
