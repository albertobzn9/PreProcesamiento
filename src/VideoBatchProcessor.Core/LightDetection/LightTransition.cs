namespace VideoBatchProcessor.Core.LightDetection;

/// <summary>
/// Cambio ON/OFF confirmado por muestras consecutivas. El frame y tiempo
/// indican dónde empezó el cambio candidato, no el último frame que lo
/// confirmó; así la transición conserva la mejor estimación visual disponible.
/// </summary>
public sealed record LightTransition(
    LightId Light,
    bool WasOn,
    bool IsOn,
    int FrameIndex,
    double TimeSeconds,
    int ConfirmedAtFrameIndex);
