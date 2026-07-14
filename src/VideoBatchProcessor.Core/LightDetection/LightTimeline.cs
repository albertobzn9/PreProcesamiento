namespace VideoBatchProcessor.Core.LightDetection;

/// <summary>
/// Resultado de estabilizar la lectura de luces de un video. No interpreta
/// ensayos, ITIs, habituación ni eventos conductuales.
/// </summary>
public sealed record LightTimeline(
    int SamplesAnalyzed,
    IReadOnlyList<LightTransition> Transitions);
