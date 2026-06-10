namespace VideoBatchProcessor.Core.LightDetection;

public sealed record LightSample(
    int FrameIndex,
    double TimeSeconds,
    LightReading FoodLeft,
    LightReading FoodRight,
    LightReading NoiseLed)
{
    public bool IsFoodLeftOn => FoodLeft.IsOn;
    public bool IsFoodRightOn => FoodRight.IsOn;
    public bool IsNoiseLedOn => NoiseLed.IsOn;
}
