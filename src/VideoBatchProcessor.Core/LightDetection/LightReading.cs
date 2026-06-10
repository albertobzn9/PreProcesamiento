namespace VideoBatchProcessor.Core.LightDetection;

public sealed record LightReading(
    LightId Light,
    double Brightness,
    double Threshold)
{
    public bool IsOn => Brightness > Threshold;
}
