using VideoBatchProcessor.Core.BehavioralData;
using VideoBatchProcessor.Core.BatchProcessing;
using VideoBatchProcessor.Core.BehavioralSynchronization;
using VideoBatchProcessor.Core.Diagnostics;
using VideoBatchProcessor.Core.LightDetection;
using VideoBatchProcessor.Core.SegmentPlanning;
using VideoBatchProcessor.Core.VideoReader;

namespace VideoBatchProcessor.Tests;

public sealed class SegmentPlannerTests
{
    [Theory]
    [InlineData("f2", BehavioralResultClassificationRule.SideTransition)]
    [InlineData("f4", BehavioralResultClassificationRule.DisplacementThreshold)]
    [InlineData("f5", BehavioralResultClassificationRule.DisplacementThreshold)]
    public void ReglaPorFase_ConservaCsYComparteDesplazamientoEnCpYDis(
        string phase, BehavioralResultClassificationRule expected) =>
        Assert.Equal(expected, SegmentPlanner.GetResultClassificationRule(phase));

    [Theory]
    [InlineData(BehavioralEventType.SafeFood, 0, 0.999, PlannedBehavioralResult.NoCrossing)]
    [InlineData(BehavioralEventType.SafeFood, 1, 1, PlannedBehavioralResult.NoCrossing)]
    [InlineData(BehavioralEventType.SafeFood, 0, 1.001, PlannedBehavioralResult.Crossing)]
    [InlineData(BehavioralEventType.SafeFood, -2, 180, PlannedBehavioralResult.Timeout)]
    [InlineData(BehavioralEventType.ConflictWithFood, 0, 0.999, PlannedBehavioralResult.NoCrossing)]
    [InlineData(BehavioralEventType.ConflictWithFood, 1, 1, PlannedBehavioralResult.NoCrossing)]
    [InlineData(BehavioralEventType.ConflictWithFood, 0, 1.001, PlannedBehavioralResult.Crossing)]
    [InlineData(BehavioralEventType.ConflictWithFood, -2, 180, PlannedBehavioralResult.Timeout)]
    public void Plan_DisClasificaDesdeElPrimerEventoEnAmbosTipos(
        BehavioralEventType type, int side, double displacement, PlannedBehavioralResult expected)
    {
        var interval = Interval(1, side == 1 ? LightId.FoodLeft : LightId.FoodRight, 60, 89);
        var behavioral = Event(1, side, 0, displacement, type);
        var synchronization = new BehavioralVideoSynchronizationResult(
            BehavioralVideoSynchronizationStatus.Warning, "/tmp/dis.mat",
            new LightTimelineDiagnosticComparison(2,
                [new(interval, behavioral, 0, DiagnosticComparisonStatus.Matched, "matched")]), []);

        var result = new SegmentPlanner().Plan(Input([interval], [behavioral], synchronization) with
        {
            ResultClassificationRule = SegmentPlanner.GetResultClassificationRule("f5"),
        });

        var segment = Assert.Single(result.Segments, item => item.Kind == PlannedSegmentKind.Event);
        Assert.Equal(expected, segment.Result);
    }

    [Fact]
    public void Plan_DisSincronizaMezclaSeguroRiesgoYConservaOrdenYLimites()
    {
        var intervals = new[]
        {
            Interval(1, LightId.FoodRight, 30, 59),
            Interval(2, LightId.NoiseLed, 75, 119),
            Interval(3, LightId.FoodRight, 90, 119),
            Interval(4, LightId.FoodLeft, 180, 209),
            Interval(5, LightId.NoiseLed, 225, 269),
            Interval(6, LightId.FoodLeft, 240, 269),
        };
        var events = new[]
        {
            Event(1, 0, 0, 0.2),
            Event(2, 0, 2, 1.001, BehavioralEventType.ConflictWithFood),
            Event(3, 1, 5, 1),
            Event(4, -2, 7, 180, BehavioralEventType.ConflictWithFood),
        };
        var synchronization = new BehavioralVideoSynchronizer().Synchronize(
            intervals, events, new LightTimelineScanRange(0, 299), 30, "/tmp/dis.mat");
        Assert.Equal(BehavioralVideoSynchronizationStatus.Ready, synchronization.Status);
        Assert.Equal(4, synchronization.Comparison.MatchedCount);
        Assert.Equal(1d, synchronization.Comparison.EstimatedStartOffsetSeconds);

        var plan = new SegmentPlanner().Plan(Input(intervals, events, synchronization) with
        {
            ResultClassificationRule = SegmentPlanner.GetResultClassificationRule("f5"),
        });
        var planned = plan.Segments.Where(item => item.Kind == PlannedSegmentKind.Event).ToArray();
        Assert.Equal(new[] { PlannedTrialType.SafeFood, PlannedTrialType.ConflictWithFood,
            PlannedTrialType.SafeFood, PlannedTrialType.ConflictWithFood }, planned.Select(item => item.TrialType!.Value));
        Assert.Equal(new[] { PlannedBehavioralResult.NoCrossing, PlannedBehavioralResult.Crossing,
            PlannedBehavioralResult.NoCrossing, PlannedBehavioralResult.Timeout }, planned.Select(item => item.Result));
        Assert.Equal(new[] { 30, 75, 180, 225 }, planned.Select(item => item.StartFrameIndex));
        Assert.Equal(new[] { 59, 119, 209, 269 }, planned.Select(item => item.EndFrameIndex));
        Assert.Empty(plan.Warnings);
        var codes = OutputSegmentCodePlanner.Create(plan.Segments);
        Assert.Equal(new[] { "habini", "e01", "iti01", "e02", "iti02", "e03", "iti03", "e04", "habfin" },
            plan.Segments.Select(item => codes[item]));
        Assert.Equal(0, plan.Segments[0].StartFrameIndex);
        Assert.Equal(299, plan.Segments[^1].EndFrameIndex);
        for (var i = 1; i < plan.Segments.Count; i++)
            Assert.Equal(plan.Segments[i - 1].EndFrameIndex + 1, plan.Segments[i].StartFrameIndex);
    }

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

    [Fact]
    public void Plan_EnCpClasificaCadaEventoSoloConDesplazYRespetaTimeout()
    {
        var intervals = new[]
        {
            Interval(1, LightId.FoodRight, 30, 59),
            Interval(2, LightId.FoodRight, 90, 119),
            Interval(3, LightId.FoodLeft, 150, 179),
            Interval(4, LightId.FoodLeft, 210, 239),
        };
        var events = new[]
        {
            Event(1, side: 0, estimatedStart: 0, crossing: 0.2, BehavioralEventType.ConflictWithFood),
            Event(2, side: 0, estimatedStart: 2, crossing: 2.1, BehavioralEventType.ConflictWithFood),
            Event(3, side: -2, estimatedStart: 4, crossing: 30, BehavioralEventType.ConflictWithFood),
            Event(4, side: 1, estimatedStart: 6, crossing: 1, BehavioralEventType.ConflictWithFood),
        };
        var rows = intervals.Zip(events, (interval, behavioral) =>
            new LightTimelineDiagnosticComparisonRow(interval, behavioral, 0, DiagnosticComparisonStatus.Matched, "matched")).ToArray();
        var synchronization = new BehavioralVideoSynchronizationResult(
            BehavioralVideoSynchronizationStatus.Ready,
            "/tmp/session.mat",
            new LightTimelineDiagnosticComparison(1, rows),
            []);
        var input = Input(intervals, events, synchronization) with
        {
            ResultClassificationRule = SegmentPlanner.GetResultClassificationRule("f4"),
        };

        var result = new SegmentPlanner().Plan(input);
        var plannedEvents = result.Segments.Where(item => item.Kind == PlannedSegmentKind.Event).ToArray();

        Assert.Equal(PlannedBehavioralResult.NoCrossing, plannedEvents[0].Result);
        Assert.Equal(PlannedBehavioralResult.Crossing, plannedEvents[1].Result);
        Assert.Equal(PlannedBehavioralResult.Timeout, plannedEvents[2].Result);
        Assert.Equal(PlannedBehavioralResult.NoCrossing, plannedEvents[3].Result);
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

    private static BehavioralEvent Event(
        int number,
        int side,
        double estimatedStart,
        double crossing,
        BehavioralEventType eventType = BehavioralEventType.SafeFood) =>
        new(number, side, 0, 0.5, estimatedStart + 0.5, 0, 0, crossing, eventType, []);
}
