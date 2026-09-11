using VideoBatchProcessor.Core.ClipExport;
using VideoBatchProcessor.Core.SegmentPlanning;
using VideoBatchProcessor.Core.VideoReader;
using VideoBatchProcessor.Core.VideoTransform;

namespace VideoBatchProcessor.Tests;

public sealed class ClipExporterTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("vbp-clip-export-").FullName;

    [Fact]
    public async Task ExportAsync_UsaFramesExactosTransformacionYRenombraAlFinal()
    {
        var source = Path.Combine(_directory, "source.mp4");
        var output = Path.Combine(_directory, "clip.mp4");
        await File.WriteAllTextAsync(source, "source");
        var runner = new SuccessfulRunner();
        var request = Request(source, output) with
        {
            Transform = new VideoTransformConfig
            {
                Crop = new VideoCropRect(10, 20, 100, 80),
                Rotation = VideoRotation.UpsideDown,
                MirrorHorizontally = true,
            },
        };

        var result = await new ClipExporter(runner).ExportAsync(request);

        Assert.True(result.Succeeded);
        Assert.True(File.Exists(output));
        Assert.False(Directory.EnumerateFiles(_directory, "*.partial.mp4").Any());
        Assert.Equal("1.83333333", ValueAfter(runner.Arguments!, "-ss"));
        Assert.Equal("35", ValueAfter(runner.Arguments!, "-frames:v"));
        Assert.Contains("-shortest", runner.Arguments!);
        Assert.DoesNotContain("-t", runner.Arguments!);
        Assert.Equal("crop=100:80:10:20,hflip,vflip,hflip", ValueAfter(runner.Arguments!, "-vf"));
        Assert.Equal(55, result.StartFrameIndex);
        Assert.Equal(89, result.EndFrameIndex);
        Assert.Equal(55d / 30d, result.StartSeconds);
        Assert.Equal(3, result.EndExclusiveSeconds);
    }

    [Fact]
    public async Task ExportAsync_SiFfmpegFallaNoDejaSalidaParcial()
    {
        var source = Path.Combine(_directory, "source.mp4");
        var output = Path.Combine(_directory, "clip.mp4");
        await File.WriteAllTextAsync(source, "source");

        var result = await new ClipExporter(new FailingRunner()).ExportAsync(Request(source, output));

        Assert.False(result.Succeeded);
        Assert.False(File.Exists(output));
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ExportAsync_RechazaSegmentoFueraDelVideoSinLlamarFfmpeg()
    {
        var source = Path.Combine(_directory, "source.mp4");
        var output = Path.Combine(_directory, "clip.mp4");
        await File.WriteAllTextAsync(source, "source");
        var runner = new SuccessfulRunner();
        var invalidSegment = Segment(startFrame: 89, endFrame: 90);

        var result = await new ClipExporter(runner).ExportAsync(Request(source, output) with { Segment = invalidSegment });

        Assert.False(result.Succeeded);
        Assert.Null(runner.Arguments);
        Assert.Contains("límites", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportAsync_AgregaCincoFramesSoloAEventos()
    {
        var source = Path.Combine(_directory, "context-source.mp4");
        var output = Path.Combine(_directory, "context-clip.mp4");
        await File.WriteAllTextAsync(source, "source");
        var runner = new SuccessfulRunner();
        var request = Request(source, output) with
        {
            SourceVideo = new VideoMetadata
            {
                FilePath = source,
                Width = 320,
                Height = 240,
                Fps = 30,
                TotalFrames = 180,
                Duration = TimeSpan.FromSeconds(6),
            },
        };

        var result = await new ClipExporter(runner).ExportAsync(request);

        Assert.True(result.Succeeded);
        Assert.Equal("1.83333333", ValueAfter(runner.Arguments!, "-ss"));
        Assert.Equal("40", ValueAfter(runner.Arguments!, "-frames:v"));
        Assert.Equal(55, result.StartFrameIndex);
        Assert.Equal(94, result.EndFrameIndex);
    }

    [Fact]
    public async Task ExportAsync_NoAgregaContextoAHabituacionNiIti()
    {
        var source = Path.Combine(_directory, "iti-source.mp4");
        var output = Path.Combine(_directory, "iti-clip.mp4");
        await File.WriteAllTextAsync(source, "source");
        var runner = new SuccessfulRunner();
        var request = Request(source, output) with
        {
            Segment = Segment(60, 89) with { Kind = PlannedSegmentKind.InterTrialInterval },
        };

        var result = await new ClipExporter(runner).ExportAsync(request);

        Assert.True(result.Succeeded);
        Assert.Equal("2", ValueAfter(runner.Arguments!, "-ss"));
        Assert.Equal("30", ValueAfter(runner.Arguments!, "-frames:v"));
        Assert.Equal(60, result.StartFrameIndex);
        Assert.Equal(89, result.EndFrameIndex);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static ClipExportRequest Request(string source, string output) =>
        new(
            source,
            new VideoMetadata
            {
                FilePath = source,
                Width = 320,
                Height = 240,
                Fps = 30,
                TotalFrames = 90,
                Duration = TimeSpan.FromSeconds(3),
            },
            Segment(60, 89),
            new VideoTransformConfig(),
            output,
            new ClipExportOptions());

    private static PlannedVideoSegment Segment(int startFrame, int endFrame) =>
        new(
            PlannedSegmentKind.Event,
            1,
            startFrame,
            endFrame,
            startFrame / 30d,
            endFrame / 30d,
            PlannedTrialType.SafeFood,
            PlannedBehavioralResult.Crossing,
            2,
            1,
            0,
            null,
            null,
            null,
            startFrame,
            endFrame,
            null,
            null,
            null,
            null);

    private static string ValueAfter(IReadOnlyList<string> arguments, string option) =>
        arguments[Array.IndexOf(arguments.ToArray(), option) + 1];

    private sealed class SuccessfulRunner : IFfmpegRunner
    {
        public IReadOnlyList<string>? Arguments { get; private set; }

        public async Task<FfmpegExecutionResult> RunAsync(string executablePath, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            Arguments = arguments;
            await File.WriteAllTextAsync(arguments[^1], "clip", cancellationToken);
            return FfmpegExecutionResult.Success();
        }
    }

    private sealed class FailingRunner : IFfmpegRunner
    {
        public Task<FfmpegExecutionResult> RunAsync(string executablePath, IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
            Task.FromResult(FfmpegExecutionResult.Failure("falló para prueba"));
    }
}
