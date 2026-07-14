namespace VideoBatchProcessor.Core.LightDetection;

/// <summary>
/// Intervalo inclusivo de frames que se analizará dentro de una sesión.
/// La interfaz puede obtenerlo por tiempo o por frame, pero el scanner solo
/// necesita esta representación única y verificable.
/// </summary>
public sealed record LightTimelineScanRange(int StartFrame, int EndFrame)
{
    public int FrameCount => EndFrame - StartFrame + 1;

    public void Validate(int totalFrames)
    {
        if (totalFrames <= 0)
            throw new ArgumentOutOfRangeException(nameof(totalFrames), "El video no contiene frames analizables.");
        if (StartFrame < 0 || StartFrame >= totalFrames)
            throw new ArgumentOutOfRangeException(nameof(StartFrame), "El frame inicial debe estar dentro del video.");
        if (EndFrame < StartFrame || EndFrame >= totalFrames)
            throw new ArgumentOutOfRangeException(nameof(EndFrame), "El frame final debe ser posterior al inicial y quedar dentro del video.");
    }
}
