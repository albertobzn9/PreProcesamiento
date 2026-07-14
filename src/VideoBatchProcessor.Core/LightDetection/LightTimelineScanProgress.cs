namespace VideoBatchProcessor.Core.LightDetection;

/// <summary>
/// Avance observable de un escaneo de luces. Se reporta en porcentajes enteros
/// para que la interfaz informe trabajo real sin recibir un mensaje por frame.
/// </summary>
public sealed record LightTimelineScanProgress(
    int FramesProcessed,
    int TotalFrames)
{
    public int Percent => TotalFrames <= 0
        ? 0
        : Math.Clamp((int)Math.Floor(FramesProcessed * 100d / TotalFrames), 0, 100);
}
