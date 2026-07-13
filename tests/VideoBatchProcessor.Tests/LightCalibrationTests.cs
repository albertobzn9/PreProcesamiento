using VideoBatchProcessor.Core.LightDetection;

namespace VideoBatchProcessor.Tests;

public class LightCalibrationTests
{
    [Fact]
    public void ReferenciasMultiples_UsaMedianasYProponePuntoMedio()
    {
        var calibration = new LightCalibration(
            LightId.NoiseLed,
            [Reference(0, 10), Reference(1, 30), Reference(2, 20)],
            [Reference(10, 210), Reference(11, 230), Reference(12, 220)]);

        Assert.Equal(20, calibration.OffMedianBrightness);
        Assert.Equal(220, calibration.OnMedianBrightness);
        Assert.Equal(120, calibration.SuggestedThreshold);
        Assert.Equal(120, calibration.AcceptedThreshold);
    }

    [Fact]
    public void ReferenciaOn_NoMasBrillanteQueOff_LanzaExcepcion()
    {
        var exception = Assert.Throws<ArgumentException>(() => new LightCalibration(
            LightId.FoodLeft,
            [Reference(0, 100)],
            [Reference(1, 100)]));

        Assert.Contains("más brillante", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReferenciaCompartidaDeComida_ConservaLaMarcaProvisional()
    {
        var calibration = new LightCalibration(
            LightId.FoodRight,
            [Reference(0, 15)],
            [Reference(30, 220)],
            usesSharedFoodReference: true);

        Assert.True(calibration.UsesSharedFoodReference);
    }

    private static LightCalibrationReference Reference(long frameIndex, double brightness) =>
        new(frameIndex, TimeSpan.FromSeconds(frameIndex / 30.0), brightness);
}
