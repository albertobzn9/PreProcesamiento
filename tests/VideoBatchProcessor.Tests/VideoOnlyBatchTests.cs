using ClosedXML.Excel;
using VideoBatchProcessor.Core.BehavioralData;
using VideoBatchProcessor.Core.BatchProcessing;
using VideoBatchProcessor.Core.ClipExport;
using VideoBatchProcessor.Core.FrameAnalyzer;
using VideoBatchProcessor.Core.LightDetection;
using VideoBatchProcessor.Core.SessionPairing;
using VideoBatchProcessor.Core.SessionResolver;
using VideoBatchProcessor.Core.VideoReader;
using VideoBatchProcessor.Core.VideoTransform;

namespace VideoBatchProcessor.Tests;

public sealed class VideoOnlyBatchTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"vbp-visual-{Guid.NewGuid():N}");

    [Fact]
    public async Task Run_PastedTableUsesNormalSynchronizationAndExport()
    {
        var request = Request();
        var video = request.SourceVideoPaths![0];
        var path = PastedBehavioralTable.Save(Path.Combine(_directory, "tables"), video,
            "1\t1\t0\t2\t3\t1\t0\t2\n2\t0\t0\t2\t6\t1\t1\t2\n3\t1\t0\t2\t9\t2\t1\t2", false);
        request = request with { Manifest = request.Manifest with { Overrides = new Dictionary<string, FileOverride>
        {
            [Path.GetFileNameWithoutExtension(video)] = new() { BehavioralSourcePath = path },
        } } };
        var reader = new FakeVideoReader { ThreeEvents = true };
        var orchestrator = Orchestrator(reader, new FakeRunner());
        Assert.Empty(orchestrator.FindMissingBehavioralSources(request));
        var session = Assert.Single((await orchestrator.RunAsync(request)).Sessions);
        Assert.True(session.Status is BatchSessionStatus.Exported or BatchSessionStatus.ExportedWithWarnings, session.Message);
        Assert.Equal(path, session.MatPath);
        Assert.Equal(3, session.Synchronization!.Comparison.MatchedCount);
        Assert.All(session.Clips, clip => Assert.False(clip.Segment.IsVideoOnly));
        Assert.Equal(7, session.Clips.Count);
    }

    [Fact]
    public async Task Run_WithoutConsentDoesNotScanOrExport()
    {
        var request = Request();
        var reader = new FakeVideoReader();
        var runner = new FakeRunner();
        var report = await Orchestrator(reader, runner).RunAsync(request);
        Assert.Equal(0, reader.ReadCount);
        Assert.Equal(0, runner.CallCount);
        Assert.Equal(BatchSessionStatus.Blocked, Assert.Single(report.Sessions).Status);
    }

    [Fact]
    public async Task Run_ConsentExportsApproximateClipsAndMarksExcelAndIndex()
    {
        var request = Request();
        request = request with { VideoOnlySourcePaths = request.SourceVideoPaths };
        var reader = new FakeVideoReader();
        var runner = new FakeRunner();
        var session = Assert.Single((await Orchestrator(reader, runner).RunAsync(request)).Sessions);
        Assert.Equal(BatchSessionStatus.ExportedWithWarnings, session.Status);
        Assert.Equal(1, reader.ReadCount);
        Assert.Equal(3, runner.CallCount);
        Assert.All(session.Clips, c => { Assert.True(c.Export.Succeeded); Assert.True(c.Segment.IsVideoOnly); Assert.Null(c.Segment.BehavioralEventNumber); });
        Assert.Null(session.Synchronization);
        Assert.Null(session.MatPath);
        Assert.Contains(session.Clips, c => c.Export.OutputPath.EndsWith("_e01_s_na_stx.mp4"));
        var folder = Path.GetDirectoryName(session.DiagnosticPath)!;
        Assert.Contains("VIDEO ONLY", File.ReadAllText(Path.Combine(folder, "processing_mode.txt")));
        Assert.Contains("video_only_approximate", File.ReadAllText(Path.Combine(folder, "clips_exportados.csv")));
        using var workbook = new XLWorkbook(session.DiagnosticPath!);
        Assert.Contains(workbook.Worksheet("Resumen").CellsUsed(), c => c.GetString().Contains("NO SINCRONIZADO"));
        Assert.Contains(workbook.Worksheet("Segmentos planeados").CellsUsed(), c => c.GetString().Contains("Desconocido (sin MAT/CSV)"));
    }

    [Theory]
    [InlineData(".mat")]
    [InlineData(".csv")]
    public void PresentButInvalidTableIsNotTreatedAsMissing(string extension)
    {
        var request = Request();
        File.WriteAllText(Path.ChangeExtension(request.SourceVideoPaths![0], extension), "broken");
        Assert.Empty(new BatchOrchestrator().FindMissingBehavioralSources(request));
    }

    [Fact]
    public async Task Run_InvalidMatCannotBeBypassedEvenWithConsent()
    {
        var request = Request();
        File.WriteAllText(Path.ChangeExtension(request.SourceVideoPaths![0], ".mat"), "broken");
        request = request with { VideoOnlySourcePaths = request.SourceVideoPaths };
        var runner = new FakeRunner();
        var session = Assert.Single((await Orchestrator(new(), runner).RunAsync(request)).Sessions);
        Assert.Equal(BatchSessionStatus.Blocked, session.Status);
        Assert.Equal(0, runner.CallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("SYNCHRONIZED - old.mat")]
    public async Task Run_ResumeDoesNotMixUnknownOrSynchronizedOutputs(string? mode)
    {
        var request = Request();
        request = request with { VideoOnlySourcePaths = request.SourceVideoPaths, ExistingOutputPolicy = ExistingOutputPolicy.ResumeIncomplete };
        var folder = Path.Combine(_directory, "exp_0526_dis_d1r1_recortes");
        Directory.CreateDirectory(folder);
        if (mode is not null) File.WriteAllText(Path.Combine(folder, "processing_mode.txt"), mode);
        var runner = new FakeRunner();
        var session = Assert.Single((await Orchestrator(new(), runner).RunAsync(request)).Sessions);
        Assert.Equal(BatchSessionStatus.Blocked, session.Status);
        Assert.Contains("processing mode", session.Message);
        Assert.Equal(0, runner.CallCount);
    }

    [Fact]
    public void PressesCsvIsNotAMainTable()
    {
        var request = Request();
        var video = request.SourceVideoPaths![0];
        File.WriteAllText(Path.Combine(_directory, Path.GetFileNameWithoutExtension(video) + "_palanqueos.csv"), "data");
        Assert.Equal(video, Assert.Single(new BatchOrchestrator().FindMissingBehavioralSources(request)));
    }

    [Fact]
    public void MissingExplicitOverrideCannotBeBypassed()
    {
        var request = Request();
        request = request with { Manifest = request.Manifest with { Overrides = new Dictionary<string, FileOverride>
        {
            [Path.GetFileNameWithoutExtension(request.SourceVideoPaths![0])] = new() { MatPath = Path.Combine(_directory, "missing.mat") },
        } } };
        Assert.Empty(new BatchOrchestrator().FindMissingBehavioralSources(request));
    }

    private BatchProcessingRequest Request()
    {
        Directory.CreateDirectory(_directory);
        var video = Path.Combine(_directory, "exp_0526_dis_d1r1.mp4");
        File.WriteAllBytes(video, []);
        return new(_directory, _directory, new(), new(
            new LightRoi(LightId.FoodLeft, 0, 0, 2, 2, shape: RoiShape.Circle),
            new LightRoi(LightId.FoodRight, 2, 0, 2, 2, shape: RoiShape.Circle),
            new LightRoi(LightId.NoiseLed, 4, 0, 2, 2, shape: RoiShape.Circle)),
            new BatchManifest { Iniciales = "abs", Sexo = "m", Tratamiento = "stx" }, new(),
            BatchExportMode.AllSegments, [video]);
    }

    private static BatchOrchestrator Orchestrator(FakeVideoReader reader, FakeRunner runner) =>
        new(clipExporter: new ClipExporter(runner), pairingAnalyzer: new BatchSessionPairingAnalyzer(reader));

    private sealed class FakeVideoReader : IVideoPairingEvidenceReader
    {
        public bool ThreeEvents { get; init; }
        public int ReadCount { get; private set; }
        public VideoPairingEvidence Read(string path, VideoTransformConfig transform, LightDetectionConfig config,
            IProgress<LightTimelineScanProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            ReadCount++;
            var metadata = new VideoMetadata { FilePath = path, Fps = 30, TotalFrames = 300, Width = 100, Height = 100, Duration = TimeSpan.FromSeconds(10) };
            LightEventInterval[] intervals = ThreeEvents
                ? [new(1, LightId.FoodLeft, 30, 1, 90, 3), new(2, LightId.FoodRight, 120, 4, 180, 6), new(3, LightId.FoodLeft, 210, 7, 270, 9)]
                : [new(1, LightId.FoodLeft, 30, 1, 90, 3)];
            return new(path, intervals, new(0, 299), 30,
                new(metadata, new(300, [])));
        }
    }

    private sealed class FakeRunner : IFfmpegRunner
    {
        public int CallCount { get; private set; }
        public Task<FfmpegExecutionResult> RunAsync(string executablePath, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            CallCount++;
            File.WriteAllBytes(arguments[^1], [1]);
            return Task.FromResult(FfmpegExecutionResult.Success());
        }
    }

    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
