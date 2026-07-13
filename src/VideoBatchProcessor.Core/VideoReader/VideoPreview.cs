namespace VideoBatchProcessor.Core.VideoReader;

/// <summary>
/// Un frame JPEG listo para mostrar como preview, acompañado de la metadata del
/// video fuente. No modifica el video ni representa una transformación final.
/// </summary>
public sealed record VideoPreview
{
    public required VideoMetadata Metadata { get; init; }
    public required byte[] JpegBytes { get; init; }
    public int PreviewWidth { get; init; }
    public int PreviewHeight { get; init; }
    public long FrameIndex { get; init; }
    public TimeSpan Timestamp { get; init; }
}
