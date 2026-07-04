using VideoBatchProcessor.Core.LightDetection;
using Xunit;

namespace VideoBatchProcessor.Tests;

public class LightDetectorTests
{
    // ── Helpers ───────────────────────────────────────────────────────────

    private static LightDetectionConfig DefaultConfig(double threshold = 180.0) => new(
        new LightRoi(LightId.FoodLeft,  10,  10, 30, 30, threshold),
        new LightRoi(LightId.FoodRight, 280, 10, 30, 30, threshold),
        new LightRoi(LightId.NoiseLed,  145, 10, 30, 30, threshold)
    );

    private static LightDetector DefaultDetector(double threshold = 180.0) =>
        new(DefaultConfig(threshold));

    /// <summary>
    /// Mock de IFrameBrightnessSource — devuelve brillo fijo por LightId.
    /// Permite probar LightDetector sin OpenCV ni frames reales.
    /// </summary>
    private sealed class FakeFrame : IFrameBrightnessSource
    {
        private readonly Dictionary<LightId, double> _values;

        public FakeFrame(double left, double right, double noise) =>
            _values = new()
            {
                [LightId.FoodLeft]  = left,
                [LightId.FoodRight] = right,
                [LightId.NoiseLed]  = noise,
            };

        public double GetMeanBrightness(LightRoi roi) => _values[roi.Light];
    }

    // ── 1. Todo apagado ───────────────────────────────────────────────────

    [Fact]
    public void TodoApagado_TodosOff()
    {
        var sample = DefaultDetector().Analyze(new FakeFrame(10, 10, 10), 0, 0.0);

        Assert.False(sample.IsFoodLeftOn);
        Assert.False(sample.IsFoodRightOn);
        Assert.False(sample.IsNoiseLedOn);
    }

    // ── 2. Ensayo seguro ──────────────────────────────────────────────────

    [Fact]
    public void EnsayoSeguro_SoloFoodLeftOn()
    {
        var sample = DefaultDetector().Analyze(new FakeFrame(220, 10, 10), 0, 0.0);

        Assert.True(sample.IsFoodLeftOn);
        Assert.False(sample.IsFoodRightOn);
        Assert.False(sample.IsNoiseLedOn);
    }

    [Fact]
    public void EnsayoSeguro_SoloFoodRightOn()
    {
        var sample = DefaultDetector().Analyze(new FakeFrame(10, 220, 10), 0, 0.0);

        Assert.False(sample.IsFoodLeftOn);
        Assert.True(sample.IsFoodRightOn);
        Assert.False(sample.IsNoiseLedOn);
    }

    // ── 3. Ensayo peligroso ───────────────────────────────────────────────

    [Fact]
    public void EnsayoPeligroso_FoodLeftYNoiseOn()
    {
        var sample = DefaultDetector().Analyze(new FakeFrame(220, 10, 220), 0, 0.0);

        Assert.True(sample.IsFoodLeftOn);
        Assert.False(sample.IsFoodRightOn);
        Assert.True(sample.IsNoiseLedOn);
    }

    [Fact]
    public void EnsayoPeligroso_FoodRightYNoiseOn()
    {
        var sample = DefaultDetector().Analyze(new FakeFrame(10, 220, 220), 0, 0.0);

        Assert.False(sample.IsFoodLeftOn);
        Assert.True(sample.IsFoodRightOn);
        Assert.True(sample.IsNoiseLedOn);
    }

    // ── 4. Umbral ─────────────────────────────────────────────────────────

    [Fact]
    public void BrilloExactoEnUmbral_EsOff()
    {
        // IsOn usa >, no >= — exactamente en el umbral es OFF
        var sample = DefaultDetector(threshold: 180.0).Analyze(new FakeFrame(180.0, 10, 10), 0, 0.0);

        Assert.False(sample.IsFoodLeftOn);
    }

    [Fact]
    public void BrilloSobreUmbral_EsOn()
    {
        var sample = DefaultDetector(threshold: 180.0).Analyze(new FakeFrame(180.1, 10, 10), 0, 0.0);

        Assert.True(sample.IsFoodLeftOn);
    }

    [Fact]
    public void UmbralPersonalizado_CadaRoiTieneSuUmbral()
    {
        var config = new LightDetectionConfig(
            new LightRoi(LightId.FoodLeft,  10,  10, 30, 30, threshold: 100.0),
            new LightRoi(LightId.FoodRight, 280, 10, 30, 30, threshold: 200.0),
            new LightRoi(LightId.NoiseLed,  145, 10, 30, 30, threshold: 150.0)
        );
        var sample = new LightDetector(config).Analyze(new FakeFrame(150, 150, 150), 0, 0.0);

        Assert.True(sample.IsFoodLeftOn);    // 150 > 100
        Assert.False(sample.IsFoodRightOn);  // 150 <= 200
        Assert.False(sample.IsNoiseLedOn);   // 150 <= 150 (no es >)
    }

    // ── 5. Trazabilidad ───────────────────────────────────────────────────

    [Fact]
    public void FrameIndex_SeConserva()
    {
        var sample = DefaultDetector().Analyze(new FakeFrame(10, 10, 10), frameIndex: 42, timeSeconds: 0.0);

        Assert.Equal(42, sample.FrameIndex);
    }

    [Fact]
    public void TimeSeconds_SeConserva()
    {
        var sample = DefaultDetector().Analyze(new FakeFrame(10, 10, 10), frameIndex: 0, timeSeconds: 3.567);

        Assert.Equal(3.567, sample.TimeSeconds);
    }

    // ── 6. LightReading ───────────────────────────────────────────────────

    [Fact]
    public void LightReading_BrilloSeConserva()
    {
        var sample = DefaultDetector().Analyze(new FakeFrame(210, 15, 8), 0, 0.0);

        Assert.Equal(210, sample.FoodLeft.Brightness);
        Assert.Equal(15,  sample.FoodRight.Brightness);
        Assert.Equal(8,   sample.NoiseLed.Brightness);
    }

    [Fact]
    public void LightReading_LightIdCorrecto()
    {
        var sample = DefaultDetector().Analyze(new FakeFrame(10, 10, 10), 0, 0.0);

        Assert.Equal(LightId.FoodLeft,  sample.FoodLeft.Light);
        Assert.Equal(LightId.FoodRight, sample.FoodRight.Light);
        Assert.Equal(LightId.NoiseLed,  sample.NoiseLed.Light);
    }

    // ── 7. Validaciones de LightDetector ─────────────────────────────────

    [Fact]
    public void FrameNulo_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            DefaultDetector().Analyze(null!, 0, 0.0));
    }

    [Fact]
    public void FrameIndexNegativo_LanzaArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DefaultDetector().Analyze(new FakeFrame(10, 10, 10), -1, 0.0));
    }

    [Fact]
    public void TimeSecondsNegativo_LanzaArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DefaultDetector().Analyze(new FakeFrame(10, 10, 10), 0, -1.0));
    }

    [Fact]
    public void ConfigNula_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new LightDetector(null!));
    }

    // ── 8. Validaciones de LightDetectionConfig ───────────────────────────

    [Fact]
    public void Config_RoiConLightIdIncorrecto_LanzaArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new LightDetectionConfig(
            new LightRoi(LightId.FoodRight, 10,  10, 30, 30),  // LightId incorrecto
            new LightRoi(LightId.FoodRight, 280, 10, 30, 30),
            new LightRoi(LightId.NoiseLed,  145, 10, 30, 30)
        ));
    }

    // ── 9. Validaciones de LightRoi ───────────────────────────────────────

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
}