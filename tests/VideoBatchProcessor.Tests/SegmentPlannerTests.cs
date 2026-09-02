using VideoBatchProcessor.Core.BehavioralData;
using VideoBatchProcessor.Core.BehavioralSynchronization;
using VideoBatchProcessor.Core.Diagnostics;
using VideoBatchProcessor.Core.LightDetection;
using VideoBatchProcessor.Core.SegmentPlanning;
using VideoBatchProcessor.Core.VideoReader;

namespace VideoBatchProcessor.Tests;

public sealed class SegmentPlannerTests
{
    [Fact]
    public void Plan_ConstruyeHabituacionEventosItiYHabituacionFinalDesdeEventosEmpatados()
    {
        var first = Interval(1, LightId.FoodRight, 60, 89);
        var second = Interval(2, LightId.FoodLeft, 120, 149);
        var firstEvent = Event(1, side: 0, estimatedStart: 0, crossing: 0.2);
        var secondEvent = Event(2, side: 1, estimatedStart: 2, crossing: 2);

        var result = new SegmentPlanner().Plan(Input([first, second], [firstEvent, secondEvent], ReadySynchronization(first, firstEvent, second, secondEvent)));

        Assert.Collection(
            result.Segments,
            segment => Assert.Equal(PlannedSegmentKind.InitialHabituation, segment.Kind),
            segment =>
            {
                Assert.Equal(PlannedSegmentKind.Event, segment.Kind);
                Assert.Equal(PlannedBehavioralResult.NotApplicable, segment.Result);
                Assert.Equal(1, segment.BehavioralEventNumber);
            },
            segment =>
            {
                Assert.Equal(PlannedSegmentKind.InterTrialInterval, segment.Kind);
                Assert.Equal(1, segment.BehavioralEventNumber);
            },
            segment =>
            {
                Assert.Equal(PlannedSegmentKind.Event, segment.Kind);
                Assert.Equal(PlannedBehavioralResult.Crossing, segment.Result);
                Assert.Equal(2, segment.BehavioralEventNumber);
            },
            segment => Assert.Equal(PlannedSegmentKind.FinalHabituation, segment.Kind));
        Assert.DoesNotContain(result.Warnings, item => item.Kind == SegmentPlanningWarningKind.SynchronizationBlocked);
    }

    [Fact]
    public void Plan_NoInventaHabituacionFueraDeUnRangoParcial()
    {
        var first = Interval(1, LightId.FoodRight, 60, 89);
        var second = Interval(2, LightId.FoodLeft, 120, 149);
        var firstEvent = Event(1, side: 0, estimatedStart: 0, crossing: 0.2);
        var secondEvent = Event(2, side: 1, estimatedStart: 2, crossing: 2);
        var input = Input(
            [first, second],
            [firstEvent, secondEvent],
            ReadySynchronization(first, firstEvent, second, secondEvent),
            new LightTimelineScanRange(60, 200));

        var result = new SegmentPlanner().Plan(input);

        Assert.DoesNotContain(result.Segments, item => item.Kind is PlannedSegmentKind.InitialHabituation or PlannedSegmentKind.FinalHabituation);
        Assert.Contains(result.Warnings, item => item.Kind == SegmentPlanningWarningKind.InitialHabituationNotCovered);
        Assert.Contains(result.Warnings, item => item.Kind == SegmentPlanningWarningKind.FinalHabituationNotCovered);
    }

    [Fact]
    public void Plan_AnteSincronizacionBloqueadaNoCreaClips()
    {
        var interval = Interval(1, LightId.FoodRight, 60, 89);
        var behavioral = Event(1, side: 0, estimatedStart: 0, crossing: 0.2);
        var blocked = new BehavioralVideoSynchronizationResult(
            BehavioralVideoSynchronizationStatus.Blocked,
            "/tmp/session.mat",
            new LightTimelineDiagnosticComparison(null, []),
            [new BehavioralVideoSynchronizationFinding(BehavioralVideoSynchronizationFindingKind.NoReliableAlignment, "Sin alineación")]);

        var result = new SegmentPlanner().Plan(Input([interval], [behavioral], blocked));

        Assert.Empty(result.Segments);
        Assert.Contains(result.Warnings, item => item.Kind == SegmentPlanningWarningKind.SynchronizationBlocked);
    }

    [Fact]
    public void Plan_ClasificaSoloComparandoElLadoConElEventoAnterior()
    {
        var first = Interval(1, LightId.FoodRight, 60, 89);
        var interEvent = Interval(2, LightId.FoodRight, 120, 149);
        var shortChange = Interval(3, LightId.FoodLeft, 180, 209);
        var firstEvent = Event(1, side: 0, estimatedStart: 0, crossing: 0.2);
        var interEventBehavioral = Event(2, side: 0, estimatedStart: 2, crossing: 2);
        var shortChangeBehavioral = Event(3, side: 1, estimatedStart: 4, crossing: 0.5);
        var synchronization = new BehavioralVideoSynchronizationResult(
            BehavioralVideoSynchronizationStatus.Ready,
            "/tmp/session.mat",
            new LightTimelineDiagnosticComparison(
                2,
                [
                    new LightTimelineDiagnosticComparisonRow(first, firstEvent, 0, DiagnosticComparisonStatus.Matched, "matched"),
                    new LightTimelineDiagnosticComparisonRow(interEvent, interEventBehavioral, 0, DiagnosticComparisonStatus.Matched, "matched"),
                    new LightTimelineDiagnosticComparisonRow(shortChange, shortChangeBehavioral, 0, DiagnosticComparisonStatus.Matched, "matched"),
                ]),
            []);

        var result = new SegmentPlanner().Plan(Input(
            [first, interEvent, shortChange],
            [firstEvent, interEventBehavioral, shortChangeBehavioral],
            synchronization));
        var events = result.Segments.Where(item => item.Kind == PlannedSegmentKind.Event).ToArray();

        Assert.Equal(PlannedBehavioralResult.NoCrossing, events[1].Result);
        Assert.Equal(PlannedBehavioralResult.Crossing, events[2].Result);
    }

    private static SegmentPlanningInput Input(
        IReadOnlyList<LightEventInterval> intervals,
        IReadOnlyList<BehavioralEvent> events,
        BehavioralVideoSynchronizationResult synchronization,
        LightTimelineScanRange? range = null) =>
        new(
            new VideoMetadata
            {
                FilePath = "/tmp/session.mp4",
                Width = 1_920,
                Height = 1_080,
                Fps = 30,
                TotalFrames = 300,
                Duration = TimeSpan.FromSeconds(10),
            },
            range ?? new LightTimelineScanRange(0, 299),
            intervals,
            events,
            synchronization);

    private static BehavioralVideoSynchronizationResult ReadySynchronization(
        LightEventInterval first,
        BehavioralEvent firstEvent,
        LightEventInterval second,
        BehavioralEvent secondEvent) =>
        new(
            BehavioralVideoSynchronizationStatus.Ready,
            "/tmp/session.mat",
            new LightTimelineDiagnosticComparison(
                2,
                [
                    new LightTimelineDiagnosticComparisonRow(first, firstEvent, 0, DiagnosticComparisonStatus.Matched, "matched"),
                    new LightTimelineDiagnosticComparisonRow(second, secondEvent, 0, DiagnosticComparisonStatus.Matched, "matched"),
                ]),
            []);

    private static LightEventInterval Interval(int sequence, LightId light, int onFrame, int offFrame) =>
        new(sequence, light, onFrame, onFrame / 30d, offFrame, offFrame / 30d);

    private static BehavioralEvent Event(int number, int side, double estimatedStart, double crossing) =>
        new(number, side, 0, 0.5, estimatedStart + 0.5, 0, 0, crossing, BehavioralEventType.SafeFood, []);
}
