namespace VideoBatchProcessor.Core.LightDetection;

public interface IFrameBrightnessSource
{
    double GetMeanBrightness(LightRoi roi);
}
