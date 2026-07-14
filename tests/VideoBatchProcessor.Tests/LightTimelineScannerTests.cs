using OpenCvSharp;
using VideoBatchProcessor.Core.FrameAnalyzer;
using VideoBatchProcessor.Core.LightDetection;
using VideoBatchProcessor.Core.VideoTransform;

namespace VideoBatchProcessor.Tests;

public sealed class LightTimelineScannerTests : IDisposable
{
    private const int Width = 160;
    private const int Height = 100;
    private const int Fps = 10;
    private readonly string _videoPath = Path.Combine(Path.GetTempPath(), $"vbp_timeline_{Guid.NewGuid():N}.avi");

    [Fact]
    public void Scan_RecorreVideoRealYDetectaCambiosEstables()
    {
        CreateVideo();
        var scanner = new LightTimelineScanner();

        var result = scanner.Scan(
            _videoPath,
            new VideoTransformConfig(),
            CreateConfig(),
            new LightTimelineConfig
            {
                MinimumConsecutiveOnSamples = 2,
                MinimumConsecutiveOffSamples = 2,
            });

        Assert.Equal(12, result.Timeline.SamplesAnalyzed);
        Assert.Collection(
            result.Timeline.Transitions.Where(transition => transition.Light == LightId.FoodLeft),
            transition => Assert.Equal((false, true, 3), (transition.WasOn, transition.IsOn, transition.FrameIndex)),
            transition => Assert.Equal((true, false, 8), (transition.WasOn, transition.IsOn, transition.FrameIndex)));
        Assert.Collection(
            result.Timeline.Transitions.Where(transition => transition.Light == LightId.NoiseLed),
            transition => Assert.Equal((false, true, 3), (transition.WasOn, transition.IsOn, transition.FrameIndex)),
            transition => Assert.Equal((true, false, 8), (transition.WasOn, transition.IsOn, transition.FrameIndex)));
        Assert.DoesNotContain(result.Timeline.Transitions, transition => transition.Light == LightId.FoodRight);
    }

    [Fact]
    public void Scan_AplicaCropYEspejoAntesDeMedirLasRois()
    {
        CreateVideo();
        var scanner = new LightTimelineScanner();

        var result = scanner.Scan(
            _videoPath,
            new VideoTransformConfig
            {
                Crop = new VideoCropRect(0, 0, 120, Height),
                MirrorHorizontally = true,
            },
            new LightDetectionConfig(
                new LightRoi(LightId.FoodLeft, 5, 13, 24, 24, 120, RoiShape.Circle),
                new LightRoi(LightId.FoodRight, 82, 13, 24, 24, 120, RoiShape.Circle),
                new LightRoi(LightId.NoiseLed, 27, 13, 24, 24, 120, RoiShape.Circle)),
            new LightTimelineConfig
            {
                MinimumConsecutiveOnSamples = 2,
                MinimumConsecutiveOffSamples = 2,
            });

        Assert.Collection(
            result.Timeline.Transitions.Where(transition => transition.Light == LightId.FoodRight),
            transition => Assert.Equal((false, true, 3), (transition.WasOn, transition.IsOn, transition.FrameIndex)),
            transition => Assert.Equal((true, false, 8), (transition.WasOn, transition.IsOn, transition.FrameIndex)));
        Assert.Collection(
            result.Timeline.Transitions.Where(transition => transition.Light == LightId.NoiseLed),
            transition => Assert.Equal((false, true, 3), (transition.WasOn, transition.IsOn, transition.FrameIndex)),
            transition => Assert.Equal((true, false, 8), (transition.WasOn, transition.IsOn, transition.FrameIndex)));
        Assert.DoesNotContain(result.Timeline.Transitions, transition => transition.Light == LightId.FoodLeft);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_videoPath))
                File.Delete(_videoPath);
        }
        catch
        {
            // El archivo temporal se elimina al terminar la sesión si OpenCV aún lo retiene.
        }
    }

    private void CreateVideo()
    {
        using var writer = new VideoWriter(_videoPath, FourCC.MJPG, Fps, new Size(Width, Height));
        Assert.True(writer.IsOpened());

        for (var index = 0; index < 12; index++)
        {
            var isOn = index is >= 3 and <= 7;
            using var frame = new Mat(Height, Width, MatType.CV_8UC3, Scalar.Black);
            if (isOn)
            {
                Cv2.Circle(frame, new Point(25, 25), 12, Scalar.White, -1);
                Cv2.Circle(frame, new Point(80, 25), 12, Scalar.White, -1);
            }

            writer.Write(frame);
        }
    }

    private static LightDetectionConfig CreateConfig() => new(
        new LightRoi(LightId.FoodLeft, 13, 13, 24, 24, 120, RoiShape.Circle),
        new LightRoi(LightId.FoodRight, 123, 13, 24, 24, 120, RoiShape.Circle),
        new LightRoi(LightId.NoiseLed, 68, 13, 24, 24, 120, RoiShape.Circle));
}
