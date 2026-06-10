namespace VideoBatchProcessor.Core.LightDetection;

public sealed class LightDetector
{
    public LightDetectionConfig Config { get; }

    public LightDetector(LightDetectionConfig config)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public LightSample Analyze(IFrameBrightnessSource frame, int frameIndex, double timeSeconds)
    {
        if (frame is null)
            throw new ArgumentNullException(nameof(frame));
        if (frameIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(frameIndex), "Frame index must be greater than or equal to zero.");
        if (timeSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(timeSeconds), "Timestamp must be greater than or equal to zero.");

        return new LightSample(
            frameIndex,
            timeSeconds,
            AnalyzeRoi(frame, Config.FoodLeft),
            AnalyzeRoi(frame, Config.FoodRight),
            AnalyzeRoi(frame, Config.NoiseLed));
    }

    private static LightReading AnalyzeRoi(IFrameBrightnessSource frame, LightRoi roi)
    {
        var brightness = frame.GetMeanBrightness(roi);
        return new LightReading(roi.Light, brightness, roi.Threshold);
    }
}
