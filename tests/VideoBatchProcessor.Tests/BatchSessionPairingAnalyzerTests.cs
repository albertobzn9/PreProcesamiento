using VideoBatchProcessor.Core.BehavioralData;
using VideoBatchProcessor.Core.FrameAnalyzer;
using VideoBatchProcessor.Core.LightDetection;
using VideoBatchProcessor.Core.SessionPairing;
using VideoBatchProcessor.Core.VideoTransform;

namespace VideoBatchProcessor.Tests;

public sealed class BatchSessionPairingAnalyzerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"vbp-pairing-{Guid.NewGuid():N}");

    [Fact]
    public void Analyze_PrefiereMp4UsaMkvComoRespaldoYLeeCadaEntradaUnaVez()
    {
        var videoReader = new FakeVideoReader(new Dictionary<string, VideoPairingEvidence>
        {
            ["sesion_a.mp4"] = Video("sesion_a.mp4", [100, 120, 140, 160], [0, 1, 0, 1]),
            ["sesion_b.mkv"] = Video("sesion_b.mkv", [100, 133, 171, 212], [1, 0, 1, 0]),
        });
        var behavioralReader = new FakeBehavioralReader(new Dictionary<string, BehavioralPairingEvidence>
        {
            ["sesion_a.mat"] = Behavioral("sesion_a.mat", [100, 120, 140, 160], [0, 1, 0, 1]),
            ["sesion_b.mat"] = Behavioral("sesion_b.mat", [100, 133, 171, 212], [1, 0, 1, 0]),
        });
        var analyzer = new BatchSessionPairingAnalyzer(videoReader, behavioralReader);

        var analysis = analyzer.Analyze(
            ["/tmp/sesion_a.mkv", "/tmp/sesion_a.mp4", "/tmp/sesion_b.mkv"],
            ["/tmp/sesion_a.mat", "/tmp/sesion_b.mat", "/tmp/sesion_a_palanqueos.csv"],
            new VideoTransformConfig(),
            LightConfig());

        Assert.Equal(2, analysis.Report.ConfirmedCount);
        Assert.Equal(["sesion_a.mp4", "sesion_b.mkv"], videoReader.ReadFileNames);
        Assert.Equal(["sesion_a.mat", "sesion_b.mat"], behavioralReader.ReadFileNames);
        Assert.Single(analysis.SelectedVideos.Single(item => item.VideoPath.EndsWith("sesion_a.mp4")).AlternateVideoPaths);
        Assert.Empty(analysis.InputIssues);
    }

    [Fact]
    public void Analyze_ConservaErroresDeLecturaSinPerderLasDemasSesiones()
    {
        var videoReader = new FakeVideoReader(new Dictionary<string, VideoPairingEvidence>
        {
            ["bueno.mp4"] = Video("bueno.mp4", [100, 120, 140, 160], [0, 1, 0, 1]),
        });
        var behavioralReader = new FakeBehavioralReader(new Dictionary<string, BehavioralPairingEvidence>
        {
            ["bueno.mat"] = Behavioral("bueno.mat", [100, 120, 140, 160], [0, 1, 0, 1]),
        });
        var analyzer = new BatchSessionPairingAnalyzer(videoReader, behavioralReader);

        var analysis = analyzer.Analyze(
            ["/tmp/bueno.mp4", "/tmp/dañado.mp4"],
            ["/tmp/bueno.mat", "/tmp/dañado.mat"],
            new VideoTransformConfig(),
            LightConfig());

        Assert.Equal(1, analysis.Report.ConfirmedCount);
        Assert.Equal(2, analysis.InputIssues.Count);
        Assert.Contains(analysis.InputIssues, item => item.Path.EndsWith("dañado.mp4") && item.Kind == SessionPairingInputKind.Video);
        Assert.Contains(analysis.InputIssues, item => item.Path.EndsWith("dañado.mat") && item.Kind == SessionPairingInputKind.BehavioralSource);
    }

    [Fact]
    public void Analyze_ElProgresoDaElPesoPrincipalAlVideo()
    {
        var videoReader = new FakeVideoReader(new Dictionary<string, VideoPairingEvidence>
        {
            ["sesion.mp4"] = Video("sesion.mp4", [100, 120, 140, 160], [0, 1, 0, 1]),
        });
        var behavioralReader = new FakeBehavioralReader(new Dictionary<string, BehavioralPairingEvidence>
        {
            ["sesion.mat"] = Behavioral("sesion.mat", [100, 120, 140, 160], [0, 1, 0, 1]),
            ["extra_a.mat"] = Behavioral("extra_a.mat", [300, 320, 340, 360], [0, 1, 0, 1]),
            ["extra_b.mat"] = Behavioral("extra_b.mat", [500, 520, 540, 560], [0, 1, 0, 1]),
        });
        var updates = new List<BatchSessionPairingProgress>();

        new BatchSessionPairingAnalyzer(videoReader, behavioralReader).Analyze(
            ["/tmp/sesion.mp4"],
            ["/tmp/sesion.mat", "/tmp/extra_a.mat", "/tmp/extra_b.mat"],
            new VideoTransformConfig(),
            LightConfig(),
            new CollectingProgress<BatchSessionPairingProgress>(updates));

        Assert.Contains(updates, item => item.Stage == "Analizando luces del video" && item.CurrentInputPercent == 0 && item.Percent == 5);
        Assert.Contains(updates, item => item.Stage == "Analizando luces del video" && item.CurrentInputPercent == 50 && item.Percent == 50);
        Assert.Equal(100, updates[^1].Percent);
    }

    [Fact]
    public void AnalyzeDirectory_OmiteClipsYReportesGeneradosPorLaAplicacion()
    {
        Directory.CreateDirectory(_directory);
        Create("exp_0126_cs_d1r1.mp4");
        Create("exp_0126_cs_d1r1.mkv");
        Create("abs_2601_f2_d1r1_m_cr1_s_cr_stx.mp4");
        Create("exp_0126_cs_d1r1.mat");
        Create("exp_0126_cs_d1r1_palanqueos.csv");
        Create("clips_exportados.csv");
        Create("emparejamiento_sesiones.csv");
        var videoReader = new FakeVideoReader(new Dictionary<string, VideoPairingEvidence>
        {
            ["exp_0126_cs_d1r1.mp4"] = Video("exp_0126_cs_d1r1.mp4", [100, 120, 140, 160], [0, 1, 0, 1]),
        });
        var behavioralReader = new FakeBehavioralReader(new Dictionary<string, BehavioralPairingEvidence>
        {
            ["exp_0126_cs_d1r1.mat"] = Behavioral("exp_0126_cs_d1r1.mat", [100, 120, 140, 160], [0, 1, 0, 1]),
        });

        var analysis = new BatchSessionPairingAnalyzer(videoReader, behavioralReader).AnalyzeDirectory(
            _directory,
            new VideoTransformConfig(),
            LightConfig());

        Assert.Equal(1, analysis.Report.ConfirmedCount);
        Assert.Equal(["exp_0126_cs_d1r1.mp4"], videoReader.ReadFileNames);
        Assert.Equal(["exp_0126_cs_d1r1.mat"], behavioralReader.ReadFileNames);
        Assert.Empty(analysis.InputIssues);
    }

    [Fact]
    public void ReportWriter_DejaConfirmacionesProblemasYTablasSinVideoEnUnSoloInventario()
    {
        Directory.CreateDirectory(_directory);
        var video = Video("video.mp4", [100, 120, 140, 160], [0, 1, 0, 1]);
        var source = Behavioral("datos.mat", [100, 120, 140, 160], [0, 1, 0, 1]);
        var extra = Behavioral("sin_video.mat", [300, 330, 360, 390], [1, 0, 1, 0]);
        var report = new SessionPairingResolver().Resolve([video], [source, extra]);
        var analysis = new BatchSessionPairingAnalysis(
            report,
            [],
            [video],
            [source, extra],
            [new SessionPairingInputIssue("/tmp/dañado.mat", SessionPairingInputKind.BehavioralSource, "No se pudo leer")]);
        var output = Path.Combine(_directory, "emparejamiento.csv");

        SessionPairingReportCsvWriter.Write(output, analysis);

        var text = File.ReadAllText(output);
        Assert.Contains("Confirmado", text);
        Assert.Contains("Error de lectura", text);
        Assert.Contains("Tabla sin video", text);
        Assert.Contains("video.mp4", text);
        Assert.Contains("datos.mat", text);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private void Create(string fileName) =>
        File.WriteAllBytes(Path.Combine(_directory, fileName), []);

    private static LightDetectionConfig LightConfig() => new(
        new LightRoi(LightId.FoodLeft, 0, 0, 2, 2, shape: RoiShape.Circle),
        new LightRoi(LightId.FoodRight, 2, 0, 2, 2, shape: RoiShape.Circle),
        new LightRoi(LightId.NoiseLed, 4, 0, 2, 2, shape: RoiShape.Circle));

    private static VideoPairingEvidence Video(
        string fileName,
        IReadOnlyList<double> starts,
        IReadOnlyList<int> sides)
    {
        var path = Path.Combine("/tmp", fileName);
        var intervals = starts.Select((start, index) => new LightEventInterval(
            index + 1,
            sides[index] == 1 ? LightId.FoodLeft : LightId.FoodRight,
            (int)((start + 2.4) * 30),
            start + 2.4,
            (int)((start + 7.4) * 30),
            start + 7.4)).ToArray();
        return new VideoPairingEvidence(path, intervals, new LightTimelineScanRange(0, 20_000), 30);
    }

    private static BehavioralPairingEvidence Behavioral(
        string fileName,
        IReadOnlyList<double> starts,
        IReadOnlyList<int> sides) =>
        new(Path.Combine("/tmp", fileName), starts.Select((start, index) => new BehavioralEvent(
            index + 1,
            sides[index],
            1,
            5,
            start + 5,
            0,
            0,
            0.2,
            BehavioralEventType.ConflictWithFood,
            [])).ToArray());

    private sealed class FakeVideoReader(IReadOnlyDictionary<string, VideoPairingEvidence> evidence)
        : IVideoPairingEvidenceReader
    {
        public List<string> ReadFileNames { get; } = [];

        public VideoPairingEvidence Read(
            string videoPath,
            VideoTransformConfig transform,
            LightDetectionConfig lightConfig,
            IProgress<LightTimelineScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var name = Path.GetFileName(videoPath);
            ReadFileNames.Add(name);
            progress?.Report(new LightTimelineScanProgress(50, 100));
            return evidence.TryGetValue(name, out var result)
                ? result with { VideoPath = Path.GetFullPath(videoPath) }
                : throw new InvalidOperationException("Video de prueba dañado");
        }
    }

    private sealed class FakeBehavioralReader(IReadOnlyDictionary<string, BehavioralPairingEvidence> evidence)
        : IBehavioralPairingEvidenceReader
    {
        public List<string> ReadFileNames { get; } = [];

        public BehavioralPairingEvidence Read(string sourcePath)
        {
            var name = Path.GetFileName(sourcePath);
            ReadFileNames.Add(name);
            return evidence.TryGetValue(name, out var result)
                ? result with { SourcePath = Path.GetFullPath(sourcePath) }
                : throw new InvalidOperationException("Tabla de prueba dañada");
        }
    }

    private sealed class CollectingProgress<T>(ICollection<T> updates) : IProgress<T>
    {
        public void Report(T value) => updates.Add(value);
    }
}
