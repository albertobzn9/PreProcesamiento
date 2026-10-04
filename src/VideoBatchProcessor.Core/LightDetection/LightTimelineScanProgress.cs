namespace VideoBatchProcessor.Core.LightDetection;

/// <summary>
/// Avance observable de un escaneo de luces. Se reporta en porcentajes enteros
/// y conteos de frames. El scanner avisa al cambiar de porcentaje o, si avanza
/// más despacio, una vez por segundo mientras recibe frames.
/// </summary>
public sealed record LightTimelineScanProgress(
    int FramesProcessed,
    int TotalFrames)
{
    public int Percent => TotalFrames <= 0
        ? 0
        : Math.Clamp((int)Math.Floor(FramesProcessed * 100d / TotalFrames), 0, 100);
}
