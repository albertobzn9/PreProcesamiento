using VideoBatchProcessor.Core.FrameAnalyzer;
using VideoBatchProcessor.Core.LightDetection;
using Xunit;

namespace VideoBatchProcessor.Tests;

public class LightDetectorTests
{
    private static LightDetectionConfig DefaultConfig(double threshold = 180.0) => new(
        new LightRoi(LightId.FoodLeft,  10,  10, 30, 30, threshold),
        new LightRoi(LightId.FoodRight, 280, 10, 30, 30, threshold),
        new LightRoi(LightId.NoiseLed,  145, 10, 30, 30, threshold)
    );

    private static LightDetector DefaultDetector(double threshold = 180.0) =>
        new(DefaultConfig(threshold));

    private sealed class FakeFrame : IFrameBrightnessSource
    {
        private readonly Dictionary<LightId, double> _values;
        public FakeFrame(double left, double right, double noise) =>
            _values = new() { [LightId.FoodLeft]=left, [LightId.FoodRight]=right, [LightId.NoiseLed]=noise };
        public double GetMeanBrightness(LightRoi roi) => _values[roi.Light];
    }

    [Fact]
    public void TodoApagado_TodosOff()
    {
        var s = DefaultDetector().Analyze(new FakeFrame(10,10,10), 0, 0.0);
        Assert.False(s.IsFoodLeftOn); Assert.False(s.IsFoodRightOn); Assert.False(s.IsNoiseLedOn);
    }

    [Fact]
    public void EnsayoSeguro_SoloFoodLeftOn()
    {
        var s = DefaultDetector().Analyze(new FakeFrame(220,10,10), 0, 0.0);
        Assert.True(s.IsFoodLeftOn); Assert.False(s.IsFoodRightOn); Assert.False(s.IsNoiseLedOn);
    }

    [Fact]
    public void EnsayoSeguro_SoloFoodRightOn()
    {
        var s = DefaultDetector().Analyze(new FakeFrame(10,220,10), 0, 0.0);
        Assert.False(s.IsFoodLeftOn); Assert.True(s.IsFoodRightOn); Assert.False(s.IsNoiseLedOn);
    }

    [Fact]
    public void EnsayoPeligroso_FoodLeftYNoiseOn()
    {
        var s = DefaultDetector().Analyze(new FakeFrame(220,10,220), 0, 0.0);
        Assert.True(s.IsFoodLeftOn); Assert.False(s.IsFoodRightOn); Assert.True(s.IsNoiseLedOn);
    }

    [Fact]
    public void EnsayoPeligroso_FoodRightYNoiseOn()
    {
        var s = DefaultDetector().Analyze(new FakeFrame(10,220,220), 0, 0.0);
        Assert.False(s.IsFoodLeftOn); Assert.True(s.IsFoodRightOn); Assert.True(s.IsNoiseLedOn);
    }

    [Fact]
    public void BrilloExactoEnUmbral_EsOff()
    {
        var s = DefaultDetector(180.0).Analyze(new FakeFrame(180.0,10,10), 0, 0.0);
        Assert.False(s.IsFoodLeftOn);
    }

    [Fact]
    public void BrilloSobreUmbral_EsOn()
    {
        var s = DefaultDetector(180.0).Analyze(new FakeFrame(180.1,10,10), 0, 0.0);
        Assert.True(s.IsFoodLeftOn);
    }

    [Fact]
    public void UmbralPersonalizado_CadaRoiTieneSuUmbral()
    {
        var config = new LightDetectionConfig(
            new LightRoi(LightId.FoodLeft,  10,  10, 30, 30, threshold: 100.0),
            new LightRoi(LightId.FoodRight, 280, 10, 30, 30, threshold: 200.0),
            new LightRoi(LightId.NoiseLed,  145, 10, 30, 30, threshold: 150.0)
        );
        var s = new LightDetector(config).Analyze(new FakeFrame(150,150,150), 0, 0.0);
        Assert.True(s.IsFoodLeftOn); Assert.False(s.IsFoodRightOn); Assert.False(s.IsNoiseLedOn);
    }

    [Fact]
    public void FrameIndex_SeConserva()
    {
        var s = DefaultDetector().Analyze(new FakeFrame(10,10,10), frameIndex: 42, timeSeconds: 0.0);
        Assert.Equal(42, s.FrameIndex);
    }

    [Fact]
    public void TimeSeconds_SeConserva()
    {
        var s = DefaultDetector().Analyze(new FakeFrame(10,10,10), frameIndex: 0, timeSeconds: 3.567);
        Assert.Equal(3.567, s.TimeSeconds);
    }

    [Fact]
    public void LightReading_BrilloSeConserva()
    {
        var s = DefaultDetector().Analyze(new FakeFrame(210,15,8), 0, 0.0);
        Assert.Equal(210, s.FoodLeft.Brightness);
        Assert.Equal(15,  s.FoodRight.Brightness);
        Assert.Equal(8,   s.NoiseLed.Brightness);
    }

    [Fact]
    public void LightReading_LightIdCorrecto()
    {
        var s = DefaultDetector().Analyze(new FakeFrame(10,10,10), 0, 0.0);
        Assert.Equal(LightId.FoodLeft,  s.FoodLeft.Light);
        Assert.Equal(LightId.FoodRight, s.FoodRight.Light);
        Assert.Equal(LightId.NoiseLed,  s.NoiseLed.Light);
    }

    [Fact]
    public void FrameNulo_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => DefaultDetector().Analyze(null!, 0, 0.0));
    }

    [Fact]
    public void FrameIndexNegativo_LanzaArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DefaultDetector().Analyze(new FakeFrame(10,10,10), -1, 0.0));
    }

    [Fact]
    public void TimeSecondsNegativo_LanzaArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DefaultDetector().Analyze(new FakeFrame(10,10,10), 0, -1.0));
    }

    [Fact]
    public void ConfigNula_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new LightDetector(null!));
    }

    [Fact]
    public void Config_RoiConLightIdIncorrecto_LanzaArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new LightDetectionConfig(
            new LightRoi(LightId.FoodRight, 10,  10, 30, 30),
            new LightRoi(LightId.FoodRight, 280, 10, 30, 30),
            new LightRoi(LightId.NoiseLed,  145, 10, 30, 30)
        ));
    }

    [Fact]
    public void LightRoi_XNegativo_LanzaExcepcion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LightRoi(LightId.FoodLeft, x: -1, y: 0, width: 30, height: 30));
    }

    [Fact]
    public void LightRoi_WidthCero_LanzaExcepcion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LightRoi(LightId.FoodLeft, x: 0, y: 0, width: 0, height: 30));
    }

    [Fact]
    public void LightRoi_UmbralNegativo_LanzaExcepcion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LightRoi(LightId.FoodLeft, x: 0, y: 0, width: 30, height: 30, threshold: -1));
    }

    [Fact]
    public void LightRoi_Circular_ConservaSuForma()
    {
        var roi = new LightRoi(
            LightId.NoiseLed,
            x: 10,
            y: 10,
            width: 24,
            height: 24,
            shape: RoiShape.Circle);

        Assert.Equal(RoiShape.Circle, roi.Shape);
    }

    [Fact]
    public void LightRoi_CircularConRectangulo_LanzaExcepcion()
    {
        Assert.Throws<ArgumentException>(() =>
            new LightRoi(
                LightId.NoiseLed,
                x: 10,
                y: 10,
                width: 24,
                height: 18,
                shape: RoiShape.Circle));
    }
}
