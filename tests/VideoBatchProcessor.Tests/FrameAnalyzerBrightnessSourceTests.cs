using OpenCvSharp;
using VideoBatchProcessor.Core.FrameAnalyzer;
using VideoBatchProcessor.Core.LightDetection;
using Xunit;

namespace VideoBatchProcessor.Tests;

public class FrameAnalyzerBrightnessSourceTests
{
    private static LightDetectionConfig CircularConfig(double threshold = 180) => new(
        new LightRoi(LightId.FoodLeft, 10, 10, 30, 30, threshold, RoiShape.Circle),
        new LightRoi(LightId.FoodRight, 280, 10, 30, 30, threshold, RoiShape.Circle),
        new LightRoi(LightId.NoiseLed, 145, 10, 30, 30, threshold, RoiShape.Circle));

    private static Mat MakeFrame(bool foodLeft, bool foodRight, bool noiseLed)
    {
        var frame = new Mat(240, 320, MatType.CV_8UC3, Scalar.Black);

        if (foodLeft)
            Cv2.Circle(frame, new Point(25, 25), 15, Scalar.White, -1);
        if (foodRight)
            Cv2.Circle(frame, new Point(295, 25), 15, Scalar.White, -1);
        if (noiseLed)
            Cv2.Circle(frame, new Point(160, 25), 15, Scalar.White, -1);

        return frame;
    }

    [Fact]
    public void Source_MideCadaLuzDelFrameReal()
    {
        var config = CircularConfig();
        using var frame = MakeFrame(foodLeft: true, foodRight: false, noiseLed: true);
        var source = new FrameAnalyzerBrightnessSource(frame, config);

        Assert.True(source.GetMeanBrightness(config.FoodLeft) > 240);
        Assert.True(source.GetMeanBrightness(config.FoodRight) < 10);
        Assert.True(source.GetMeanBrightness(config.NoiseLed) > 240);
    }

    [Fact]
    public void LightDetector_ConAdaptadorOpenCv_EntregaEstadosReales()
    {
        var config = CircularConfig();
        using var frame = MakeFrame(foodLeft: false, foodRight: true, noiseLed: true);
        var source = new FrameAnalyzerBrightnessSource(frame, config);

        var sample = new LightDetector(config).Analyze(source, frameIndex: 42, timeSeconds: 1.4);

        Assert.False(sample.IsFoodLeftOn);
        Assert.True(sample.IsFoodRightOn);
        Assert.True(sample.IsNoiseLedOn);
        Assert.Equal(42, sample.FrameIndex);
        Assert.Equal(1.4, sample.TimeSeconds);
    }

    [Fact]
    public void Constructor_ConfigNula_LanzaExcepcion()
    {
        using var frame = MakeFrame(foodLeft: false, foodRight: false, noiseLed: false);

        Assert.Throws<ArgumentNullException>(() =>
            new FrameAnalyzerBrightnessSource(frame, null!));
    }
}
