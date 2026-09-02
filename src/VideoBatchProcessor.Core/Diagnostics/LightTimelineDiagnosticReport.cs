using VideoBatchProcessor.Core.BehavioralData;
using VideoBatchProcessor.Core.LightDetection;
using VideoBatchProcessor.Core.VideoReader;
using VideoBatchProcessor.Core.VideoTransform;

namespace VideoBatchProcessor.Core.Diagnostics;

/// <summary>
/// Evidencia exportable para revisar una detección de luces contra la fuente
/// conductual, sin afirmar aún que ambos relojes están sincronizados.
/// </summary>
public sealed record LightTimelineDiagnosticReport(
    VideoMetadata Video,
    LightTimelineScanRange ScanRange,
    VideoTransformConfig Transform,
    LightTimeline Timeline,
    IReadOnlyList<LightEventInterval> VisualIntervals,
    LightDetectionConfig LightConfig,
    IReadOnlyList<BehavioralEvent> BehavioralEvents,
    string? BehavioralSourcePath,
    string? BehavioralReadError);
