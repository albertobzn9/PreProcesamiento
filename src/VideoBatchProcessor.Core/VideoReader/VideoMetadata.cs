namespace VideoBatchProcessor.Core.VideoReader;

/// <summary>
/// Metadata estática de un archivo de video.
/// Se calcula una vez al abrir el archivo y no cambia durante la sesión.
/// </summary>
public sealed record VideoMetadata
{
    /// <summary>Frames por segundo (puede no ser entero, ej. 29.97).</summary>
    public double Fps           { get; init; }

    public int    Width         { get; init; }
    public int    Height        { get; init; }

    /// <summary>Total de frames del video según el contenedor.</summary>
    public long   TotalFrames   { get; init; }

    /// <summary>Duración total del video.</summary>
    public TimeSpan Duration    { get; init; }

    /// <summary>Códec FourCC del video (ej. "avc1", "mp4v"). Útil para diagnóstico.</summary>
    public string Codec         { get; init; } = string.Empty;

    /// <summary>Ruta absoluta del archivo de video.</summary>
    public string FilePath      { get; init; } = string.Empty;
}