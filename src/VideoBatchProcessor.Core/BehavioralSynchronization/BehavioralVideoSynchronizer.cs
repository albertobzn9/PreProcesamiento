using VideoBatchProcessor.Core.BehavioralData;
using VideoBatchProcessor.Core.Diagnostics;
using VideoBatchProcessor.Core.LightDetection;

namespace VideoBatchProcessor.Core.BehavioralSynchronization;

/// <summary>
/// Revisa si la evidencia de luces de un video y su fuente conductual pueden
/// usarse juntas antes de planear recortes. Lee resultados ya normalizados;
/// no abre archivos, no cambia MAT/CSV y nunca corrige asociaciones solo.
/// </summary>
public sealed class BehavioralVideoSynchronizer
{
    private const int MinimumMatchedEventsForReady = 3;

    private readonly LightTimelineDiagnosticComparer _comparer = new();

    public BehavioralVideoSynchronizationResult Synchronize(
        IReadOnlyList<LightEventInterval> visualIntervals,
        IReadOnlyList<BehavioralEvent> behavioralEvents,
        LightTimelineScanRange scanRange,
        double videoFps,
        string? behavioralSourcePath = null)
    {
        ArgumentNullException.ThrowIfNull(visualIntervals);
        ArgumentNullException.ThrowIfNull(behavioralEvents);
        ArgumentNullException.ThrowIfNull(scanRange);
        if (videoFps <= 0)
            throw new ArgumentOutOfRangeException(nameof(videoFps), "El FPS debe ser mayor que cero.");

        var comparison = _comparer.Compare(
            visualIntervals,
            behavioralEvents,
            scanRange.StartFrame / videoFps,
            scanRange.EndFrame / videoFps);
        var findings = new List<BehavioralVideoSynchronizationFinding>();

        if (behavioralEvents.Count == 0)
        {
            findings.Add(new(
                BehavioralVideoSynchronizationFindingKind.MissingBehavioralEvidence,
                "No hay filas conductuales legibles para validar este video."));
        }

        if (!visualIntervals.Any(IsFoodLight))
        {
            findings.Add(new(
                BehavioralVideoSynchronizationFindingKind.NoVisualEvidence,
                "No hay intervalos de luz de comida dentro del rango analizado."));
        }

        if (comparison.EstimatedStartOffsetSeconds is null && behavioralEvents.Count > 0 && visualIntervals.Any(IsFoodLight))
        {
            findings.Add(new(
                BehavioralVideoSynchronizationFindingKind.NoReliableAlignment,
                "No hubo evidencia suficiente para estimar un desfase visual-conductual estable."));
        }

        if (comparison.MatchedCount == 0 && behavioralEvents.Count > 0 && visualIntervals.Any(IsFoodLight))
        {
            findings.Add(new(
                BehavioralVideoSynchronizationFindingKind.PossibleSourceMismatch,
                "No hubo eventos compatibles por lado y tiempo; revisar si el video y la fuente conductual corresponden a la misma sesión."));
        }

        if (comparison.VisualWithoutBehavioralCount > 0)
        {
            findings.Add(new(
                BehavioralVideoSynchronizationFindingKind.VisualWithoutBehavioral,
                $"Hay {comparison.VisualWithoutBehavioralCount} señal(es) visual(es) sin fila conductual compatible."));
        }

        if (comparison.BehavioralWithoutVisualCount > 0)
        {
            findings.Add(new(
                BehavioralVideoSynchronizationFindingKind.BehavioralWithoutVisual,
                $"Hay {comparison.BehavioralWithoutVisualCount} fila(s) conductual(es) sin señal visual compatible en el rango analizado."));
        }

        var status = ResolveStatus(comparison, findings);
        return new BehavioralVideoSynchronizationResult(
            status,
            behavioralSourcePath,
            comparison,
            findings);
    }

    private static BehavioralVideoSynchronizationStatus ResolveStatus(
        LightTimelineDiagnosticComparison comparison,
        IReadOnlyList<BehavioralVideoSynchronizationFinding> findings)
    {
        if (findings.Any(item => item.Kind is
                BehavioralVideoSynchronizationFindingKind.MissingBehavioralEvidence or
                BehavioralVideoSynchronizationFindingKind.NoVisualEvidence or
                BehavioralVideoSynchronizationFindingKind.NoReliableAlignment or
                BehavioralVideoSynchronizationFindingKind.PossibleSourceMismatch))
        {
            return BehavioralVideoSynchronizationStatus.Blocked;
        }

        if (comparison.MatchedCount < MinimumMatchedEventsForReady ||
            comparison.VisualWithoutBehavioralCount > 0 ||
            comparison.BehavioralWithoutVisualCount > 0)
        {
            return BehavioralVideoSynchronizationStatus.Warning;
        }

        return BehavioralVideoSynchronizationStatus.Ready;
    }

    private static bool IsFoodLight(LightEventInterval interval) =>
        interval.Light is LightId.FoodLeft or LightId.FoodRight;
}

public enum BehavioralVideoSynchronizationStatus
{
    Ready,
    Warning,
    Blocked,
}

public enum BehavioralVideoSynchronizationFindingKind
{
    MissingBehavioralEvidence,
    NoVisualEvidence,
    NoReliableAlignment,
    PossibleSourceMismatch,
    VisualWithoutBehavioral,
    BehavioralWithoutVisual,
}

public sealed record BehavioralVideoSynchronizationFinding(
    BehavioralVideoSynchronizationFindingKind Kind,
    string Message);

public sealed record BehavioralVideoSynchronizationResult(
    BehavioralVideoSynchronizationStatus Status,
    string? BehavioralSourcePath,
    LightTimelineDiagnosticComparison Comparison,
    IReadOnlyList<BehavioralVideoSynchronizationFinding> Findings);
