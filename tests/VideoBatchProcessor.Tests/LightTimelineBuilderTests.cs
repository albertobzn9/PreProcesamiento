using VideoBatchProcessor.Core.LightDetection;

namespace VideoBatchProcessor.Tests;

public sealed class LightTimelineBuilderTests
{
    [Fact]
    public void CambioOnEstable_RegistraInicioDelPrimerFrameCandidato()
    {
        var timeline = new LightTimelineBuilder().Build(
        [
            Sample(0), Sample(1), Sample(2),
            Sample(3, foodLeft: true), Sample(4, foodLeft: true), Sample(5, foodLeft: true),
        ]);

        var transition = Assert.Single(timeline.Transitions);
        Assert.Equal(LightId.FoodLeft, transition.Light);
        Assert.False(transition.WasOn);
        Assert.True(transition.IsOn);
        Assert.Equal(3, transition.FrameIndex);
        Assert.Equal(5, transition.ConfirmedAtFrameIndex);
        Assert.Equal(0.3, transition.TimeSeconds, precision: 3);
    }

    [Fact]
    public void ArtefactoDeUnFrame_NoCreaTransicion()
    {
        var timeline = new LightTimelineBuilder().Build(
        [
            Sample(0), Sample(1), Sample(2),
            Sample(3, foodLeft: true),
            Sample(4), Sample(5), Sample(6),
        ]);

        Assert.Empty(timeline.Transitions);
    }

    [Fact]
    public void LucesIndependientes_ConservanSusPropiasTransiciones()
    {
        var timeline = new LightTimelineBuilder(new LightTimelineConfig
        {
            MinimumConsecutiveOnSamples = 2,
            MinimumConsecutiveOffSamples = 2,
        }).Build(
        [
            Sample(0), Sample(1),
            Sample(2, foodLeft: true), Sample(3, foodLeft: true),
            Sample(4, foodLeft: true, noiseLed: true), Sample(5, foodLeft: true, noiseLed: true),
            Sample(6, noiseLed: true), Sample(7, noiseLed: true),
            Sample(8), Sample(9),
        ]);

        Assert.Collection(
            timeline.Transitions,
            transition => Assert.Equal((LightId.FoodLeft, false, true, 2), (transition.Light, transition.WasOn, transition.IsOn, transition.FrameIndex)),
            transition => Assert.Equal((LightId.NoiseLed, false, true, 4), (transition.Light, transition.WasOn, transition.IsOn, transition.FrameIndex)),
            transition => Assert.Equal((LightId.FoodLeft, true, false, 6), (transition.Light, transition.WasOn, transition.IsOn, transition.FrameIndex)),
            transition => Assert.Equal((LightId.NoiseLed, true, false, 8), (transition.Light, transition.WasOn, transition.IsOn, transition.FrameIndex)));
    }

    [Fact]
    public void MuestrasFueraDeOrden_RechazaEntradaAmbigua()
    {
        var builder = new LightTimelineBuilder();

        Assert.Throws<ArgumentException>(() => builder.Build([Sample(1), Sample(1)]));
    }

    [Fact]
    public void ConfiguracionInvalida_RechazaCeros()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LightTimelineBuilder(new LightTimelineConfig
        {
            MinimumConsecutiveOnSamples = 0,
        }));
    }

    private static LightSample Sample(int frameIndex, bool foodLeft = false, bool foodRight = false, bool noiseLed = false) => new(
        frameIndex,
        frameIndex / 10.0,
        Reading(LightId.FoodLeft, foodLeft),
        Reading(LightId.FoodRight, foodRight),
        Reading(LightId.NoiseLed, noiseLed));

    private static LightReading Reading(LightId light, bool isOn) => new(light, isOn ? 200 : 0, 100);
}
