using VideoBatchProcessor.Core.VideoReader;

namespace VideoBatchProcessor.Core.LightDetection;

/// <summary>
/// Evidencia mínima de un escaneo completo de luces sobre un solo video.
/// </summary>
public sealed record LightTimelineScanResult(
    VideoMetadata Metadata,
    LightTimeline Timeline);
