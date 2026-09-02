using VideoBatchProcessor.Core.BehavioralData;
using VideoBatchProcessor.Core.BehavioralSynchronization;
using VideoBatchProcessor.Core.Diagnostics;
using VideoBatchProcessor.Core.LightDetection;
using VideoBatchProcessor.Core.VideoReader;

namespace VideoBatchProcessor.Core.SegmentPlanning;

/// <summary>
/// Construye segmentos desde las filas conductuales ya validadas contra video.
/// Las luces aportan anclas y límites visuales; por sí solas nunca crean un
/// ensayo nuevo.
/// </summary>
public sealed class SegmentPlanner
{
    public SegmentPlanningResult Plan(SegmentPlanningInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Video.TotalFrames is <= 0 or > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(input), "El video no tiene un rango de frames compatible con el planner.");

        input.ScanRange.Validate((int)input.Video.TotalFrames);
        var warnings = new List<SegmentPlanningWarning>();
        if (input.Synchronization.Status == BehavioralVideoSynchronizationStatus.Blocked)
        {
            warnings.Add(new(
                SegmentPlanningWarningKind.SynchronizationBlocked,
                "La sincronización está bloqueada; no se crearán clips hasta revisar la fuente conductual o la detección de luces.",
                null));
            return new SegmentPlanningResult([], warnings);
        }

        var previousSides = PreviousKnownSides(input.BehavioralEvents);
        var matchedEvents = input.Synchronization.Comparison.Rows
            .Where(row => row.Status == DiagnosticComparisonStatus.Matched &&
                          row.VisualInterval is not null &&
                          row.BehavioralEvent is not null)
            .Select(row => new MatchedEvent(row.VisualInterval!, row.BehavioralEvent!, row.StartResidualSeconds))
            .OrderBy(item => item.Food.OnTimeSeconds)
            .ToArray();
        var eventSegments = new List<PlannedVideoSegment>();
        var priorFoodEndSeconds = input.ScanRange.StartFrame / input.Video.Fps;

        foreach (var matched in matchedEvents)
        {
            if (matched.Event.EventType == BehavioralEventType.SoundOnly)
            {
                warnings.Add(new(
                    SegmentPlanningWarningKind.SoundOnlyNotPlanned,
                    $"El evento {matched.Event.EventNumber} es solo sonido; todavía requiere un ancla LED validada.",
                    matched.Event.EventNumber));
                continue;
            }

            if (!matched.Food.IsComplete)
            {
                warnings.Add(new(
                    SegmentPlanningWarningKind.FoodLightEndMissing,
                    $"El evento {matched.Event.EventNumber} no tiene apagado de comida dentro del rango analizado.",
                    matched.Event.EventNumber));
                continue;
            }

            var warning = FindWarningInterval(input.VisualIntervals, matched, priorFoodEndSeconds);
            var start = matched.Food;
            if (matched.Event.EventType == BehavioralEventType.ConflictWithFood)
            {
                if (warning is null)
                {
                    warnings.Add(new(
                        SegmentPlanningWarningKind.WarningLightMissing,
                        $"El evento de riesgo {matched.Event.EventNumber} no tiene LED de ruido compatible antes de la comida.",
                        matched.Event.EventNumber));
                }
                else
                {
                    start = warning;
                }
            }

            var classification = ClassifyResult(matched.Event, previousSides.GetValueOrDefault(matched.Event.EventNumber));

            var offset = input.Synchronization.Comparison.EstimatedStartOffsetSeconds;
            var mappedBehavioralStart = offset is null
                ? (double?)null
                : LightTimelineDiagnosticComparer.EstimatedMatlabStart(matched.Event) + offset.Value;
            var mappedPress = offset is null ? (double?)null : matched.Event.AbsoluteTimeSeconds + offset.Value;
            var endFrame = matched.Food.OffFrameIndex!.Value;
            var endSeconds = matched.Food.OffTimeSeconds!.Value;
            eventSegments.Add(new PlannedVideoSegment(
                PlannedSegmentKind.Event,
                eventSegments.Count + 1,
                start.OnFrameIndex,
                endFrame,
                start.OnTimeSeconds,
                endSeconds,
                ToTrialType(matched.Event.EventType),
                classification,
                matched.Event.EventNumber,
                matched.Event.Side,
                previousSides.GetValueOrDefault(matched.Event.EventNumber),
                matched.Event.LeverLatencySeconds,
                matched.Event.CrossingLatencySeconds,
                warning?.OnFrameIndex,
                matched.Food.OnFrameIndex,
                matched.Food.OffFrameIndex,
                mappedBehavioralStart,
                mappedPress,
                matched.ResidualSeconds,
                mappedPress is null ? null : endSeconds - mappedPress.Value));
            priorFoodEndSeconds = endSeconds;
        }

        foreach (var row in input.Synchronization.Comparison.Rows.Where(row => row.Status == DiagnosticComparisonStatus.BehavioralWithoutVisual))
        {
            warnings.Add(new(
                SegmentPlanningWarningKind.BehavioralEventWithoutVisualAnchor,
                $"El evento conductual {row.BehavioralEvent!.EventNumber} no tiene ancla visual compatible; no se planeó un clip automático.",
                row.BehavioralEvent.EventNumber));
        }

        foreach (var row in input.Synchronization.Comparison.Rows.Where(row => row.Status == DiagnosticComparisonStatus.VisualWithoutBehavioral))
        {
            warnings.Add(new(
                SegmentPlanningWarningKind.VisualSignalWithoutBehavioralEvent,
                "Se detectó una señal visual sin fila conductual compatible; queda como evidencia, no como clip.",
                null));
        }

        var segments = new List<PlannedVideoSegment>();
        AddInitialHabituation(input, eventSegments, segments, warnings);
        AddEventsAndItis(input, eventSegments, segments, warnings);
        AddFinalHabituation(input, eventSegments, segments, warnings);
        return new SegmentPlanningResult(segments, warnings);
    }

    private static void AddInitialHabituation(
        SegmentPlanningInput input,
        IReadOnlyList<PlannedVideoSegment> events,
        ICollection<PlannedVideoSegment> segments,
        ICollection<SegmentPlanningWarning> warnings)
    {
        if (input.ScanRange.StartFrame != 0)
        {
            warnings.Add(new(
                SegmentPlanningWarningKind.InitialHabituationNotCovered,
                "El rango analizado no inicia en frame 0; no se puede planear habituación inicial.",
                null));
            return;
        }

        if (events.Count == 0)
            return;

        var first = events[0];
        if (first.StartFrameIndex <= 0)
            return;

        segments.Add(CreateContextSegment(
            PlannedSegmentKind.InitialHabituation,
            1,
            0,
            first.StartFrameIndex - 1,
            0d,
            FrameTime(input.Video.Fps, first.StartFrameIndex - 1)));
    }

    private static void AddEventsAndItis(
        SegmentPlanningInput input,
        IReadOnlyList<PlannedVideoSegment> events,
        ICollection<PlannedVideoSegment> segments,
        ICollection<SegmentPlanningWarning> warnings)
    {
        for (var index = 0; index < events.Count; index++)
        {
            var current = events[index];
            segments.Add(current with { Sequence = segments.Count + 1 });
            if (index == events.Count - 1)
                continue;

            var next = events[index + 1];
            var itiStart = current.EndFrameIndex + 1;
            var itiEnd = next.StartFrameIndex - 1;
            if (itiStart > itiEnd)
            {
                warnings.Add(new(
                    SegmentPlanningWarningKind.EventIntervalsOverlap,
                    $"Los eventos {current.BehavioralEventNumber} y {next.BehavioralEventNumber} no dejan un ITI visual separable.",
                    next.BehavioralEventNumber));
                continue;
            }

            segments.Add(CreateContextSegment(
                PlannedSegmentKind.InterTrialInterval,
                segments.Count + 1,
                itiStart,
                itiEnd,
                FrameTime(input.Video.Fps, itiStart),
                FrameTime(input.Video.Fps, itiEnd)));
        }
    }

    private static void AddFinalHabituation(
        SegmentPlanningInput input,
        IReadOnlyList<PlannedVideoSegment> events,
        ICollection<PlannedVideoSegment> segments,
        ICollection<SegmentPlanningWarning> warnings)
    {
        if (input.ScanRange.EndFrame != input.Video.TotalFrames - 1)
        {
            warnings.Add(new(
                SegmentPlanningWarningKind.FinalHabituationNotCovered,
                "El rango analizado no llega al último frame; no se puede planear habituación final.",
                null));
            return;
        }

        if (events.Count == 0)
            return;

        var last = events[^1];
        var start = last.EndFrameIndex + 1;
        var end = input.ScanRange.EndFrame;
        if (start > end)
            return;

        var unexpectedSignals = input.VisualIntervals
            .Where(interval => interval.OnFrameIndex >= start)
            .Any();
        if (unexpectedSignals)
        {
            warnings.Add(new(
                SegmentPlanningWarningKind.LightDuringFinalHabituation,
                "Hay señales de luz después del último evento validado; revisar antes de aceptar la habituación final.",
                null));
        }

        segments.Add(CreateContextSegment(
            PlannedSegmentKind.FinalHabituation,
            segments.Count + 1,
            start,
            end,
            FrameTime(input.Video.Fps, start),
            FrameTime(input.Video.Fps, end)));
    }

    private static LightEventInterval? FindWarningInterval(
        IReadOnlyList<LightEventInterval> intervals,
        MatchedEvent matched,
        double priorFoodEndSeconds) =>
        intervals
            .Where(interval => interval.Light == LightId.NoiseLed &&
                               interval.OnTimeSeconds <= matched.Food.OnTimeSeconds &&
                               interval.OnTimeSeconds >= priorFoodEndSeconds)
            .OrderByDescending(interval => interval.OnTimeSeconds)
            .FirstOrDefault();

    private static Dictionary<int, int?> PreviousKnownSides(IReadOnlyList<BehavioralEvent> events)
    {
        var result = new Dictionary<int, int?>();
        int? previous = null;
        foreach (var item in events.OrderBy(item => item.EventNumber))
        {
            result[item.EventNumber] = previous;
            if (item.Side is 0 or 1)
                previous = item.Side;
        }

        return result;
    }

    private static PlannedBehavioralResult ClassifyResult(BehavioralEvent item, int? previousSide)
    {
        if (item.Side == -2)
            return PlannedBehavioralResult.Timeout;
        if (previousSide is null)
            return PlannedBehavioralResult.NotApplicable;

        return previousSide.Value == item.Side
            ? PlannedBehavioralResult.NoCrossing
            : PlannedBehavioralResult.Crossing;
    }

    private static PlannedTrialType ToTrialType(BehavioralEventType eventType) => eventType switch
    {
        BehavioralEventType.SafeFood => PlannedTrialType.SafeFood,
        BehavioralEventType.ConflictWithFood => PlannedTrialType.ConflictWithFood,
        BehavioralEventType.SoundOnly => PlannedTrialType.SoundOnly,
        _ => throw new ArgumentOutOfRangeException(nameof(eventType)),
    };

    private static PlannedVideoSegment CreateContextSegment(
        PlannedSegmentKind kind,
        int sequence,
        int startFrame,
        int endFrame,
        double startSeconds,
        double endSeconds) =>
        new(
            kind,
            sequence,
            startFrame,
            endFrame,
            startSeconds,
            endSeconds,
            null,
            PlannedBehavioralResult.NotApplicable,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

    private static double FrameTime(double fps, int frameIndex) => frameIndex / fps;

    private sealed record MatchedEvent(
        LightEventInterval Food,
        BehavioralEvent Event,
        double? ResidualSeconds);

}

public sealed record SegmentPlanningInput(
    VideoMetadata Video,
    LightTimelineScanRange ScanRange,
    IReadOnlyList<LightEventInterval> VisualIntervals,
    IReadOnlyList<BehavioralEvent> BehavioralEvents,
    BehavioralVideoSynchronizationResult Synchronization);

public sealed record SegmentPlanningResult(
    IReadOnlyList<PlannedVideoSegment> Segments,
    IReadOnlyList<SegmentPlanningWarning> Warnings);

public enum PlannedSegmentKind
{
    InitialHabituation,
    Event,
    InterTrialInterval,
    FinalHabituation,
}

public enum PlannedTrialType
{
    SafeFood,
    ConflictWithFood,
    SoundOnly,
}

public enum PlannedBehavioralResult
{
    Crossing,
    NoCrossing,
    Timeout,
    NotApplicable,
}

public enum SegmentPlanningWarningKind
{
    SynchronizationBlocked,
    BehavioralEventWithoutVisualAnchor,
    VisualSignalWithoutBehavioralEvent,
    FoodLightEndMissing,
    WarningLightMissing,
    SoundOnlyNotPlanned,
    InitialHabituationNotCovered,
    FinalHabituationNotCovered,
    LightDuringFinalHabituation,
    EventIntervalsOverlap,
}

public sealed record SegmentPlanningWarning(
    SegmentPlanningWarningKind Kind,
    string Message,
    int? BehavioralEventNumber);

public sealed record PlannedVideoSegment(
    PlannedSegmentKind Kind,
    int Sequence,
    int StartFrameIndex,
    int EndFrameIndex,
    double StartTimeSeconds,
    double EndTimeSeconds,
    PlannedTrialType? TrialType,
    PlannedBehavioralResult Result,
    int? BehavioralEventNumber,
    int? CurrentSide,
    int? PreviousKnownSide,
    double? LeverLatencySeconds,
    double? CrossingLatencySeconds,
    int? WarningStartFrameIndex,
    int? FoodLightStartFrameIndex,
    int? FoodLightEndFrameIndex,
    double? MappedBehavioralStartSeconds,
    double? MappedBehavioralPressSeconds,
    double? VisualStartResidualSeconds,
    double? PostPressLightTailSeconds)
{
    public double DurationSeconds => EndTimeSeconds - StartTimeSeconds;
}
