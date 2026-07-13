namespace VideoBatchProcessor.Core.VideoTransform;

/// <summary>
/// Giro que puede aplicarse a una sesión antes de marcar sus luces o exportar
/// clips. El protocolo solo requiere la imagen intacta o invertida 180 grados.
/// </summary>
public enum VideoRotation
{
    None,
    UpsideDown,
}

/// <summary>
/// Rectángulo de recorte en píxeles del video fuente, antes de rotar o espejar.
/// </summary>
public sealed record VideoCropRect(int X, int Y, int Width, int Height)
{
    public bool IsWithin(int sourceWidth, int sourceHeight) =>
        X >= 0 &&
        Y >= 0 &&
        Width > 0 &&
        Height > 0 &&
        X + Width <= sourceWidth &&
        Y + Height <= sourceHeight;
}

/// <summary>
/// Decisiones visuales de una cámara. Son datos reutilizables: no modifican el
/// archivo fuente y después podrán formar parte de un CameraProfile.
/// </summary>
public sealed record VideoTransformConfig
{
    public VideoCropRect? Crop { get; init; }
    public VideoRotation Rotation { get; init; } = VideoRotation.None;
    public bool MirrorHorizontally { get; init; }

    public bool TryValidateFor(int sourceWidth, int sourceHeight, out string? error)
    {
        error = null;

        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            error = "El video no tiene dimensiones válidas para configurar la cámara.";
            return false;
        }

        if (Crop is not null && !Crop.IsWithin(sourceWidth, sourceHeight))
        {
            error = "El recorte debe quedar completamente dentro del video fuente.";
            return false;
        }

        return true;
    }
}
