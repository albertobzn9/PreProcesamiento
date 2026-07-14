namespace VideoBatchProcessor.Core.LightDetection;

/// <summary>
/// Regla mínima de estabilidad para aceptar un cambio visual. Los valores se
/// expresan en muestras consecutivas porque el escáner puede recorrer cada
/// frame o aplicar una estrategia de muestreo futura.
/// </summary>
public sealed record LightTimelineConfig
{
    public int MinimumConsecutiveOnSamples { get; init; } = 3;
    public int MinimumConsecutiveOffSamples { get; init; } = 3;

    public void Validate()
    {
        if (MinimumConsecutiveOnSamples <= 0)
            throw new ArgumentOutOfRangeException(nameof(MinimumConsecutiveOnSamples));
        if (MinimumConsecutiveOffSamples <= 0)
            throw new ArgumentOutOfRangeException(nameof(MinimumConsecutiveOffSamples));
    }
}
