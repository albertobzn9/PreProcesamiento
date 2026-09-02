using VideoBatchProcessor.Core.LightDetection;

namespace VideoBatchProcessor.Tests;

public sealed class LightEventIntervalBuilderTests
{
    [Fact]
    public void Build_EmparejaOnConOffYPreservaUnOnSinCierre()
    {
        var timeline = new LightTimeline(
            SamplesAnalyzed: 42,
            Transitions:
            [
                new LightTransition(LightId.FoodRight, false, true, 100, 3.333, 102),
                new LightTransition(LightId.NoiseLed, false, true, 140, 4.667, 142),
                new LightTransition(LightId.FoodRight, true, false, 250, 8.333, 252),
            ]);

        var intervals = LightEventIntervalBuilder.Build(timeline);

        Assert.Collection(
            intervals,
            food =>
            {
                Assert.Equal(LightId.FoodRight, food.Light);
                Assert.Equal(100, food.OnFrameIndex);
                Assert.Equal(250, food.OffFrameIndex);
                Assert.True(food.IsComplete);
                Assert.Equal(5, food.DurationSeconds!.Value, precision: 3);
            },
            led =>
            {
                Assert.Equal(LightId.NoiseLed, led.Light);
                Assert.Equal(140, led.OnFrameIndex);
                Assert.False(led.IsComplete);
                Assert.Null(led.OffFrameIndex);
            });
    }
}
