using VideoBatchProcessor.Core.BehavioralData;
using VideoBatchProcessor.Core.Diagnostics;
using VideoBatchProcessor.Core.LightDetection;

namespace VideoBatchProcessor.Tests;

public sealed class LightTimelineDiagnosticComparerTests
{
    [Fact]
    public void Compare_DejaFalsoPositivoFueraYAlineaEventosPorLadoYTiempo()
    {
        var intervals = new[]
        {
            Interval(1, LightId.FoodRight, 211.8, 212.0),
            Interval(2, LightId.FoodRight, 303.8, 312.7),
            Interval(3, LightId.FoodLeft, 330.0, 337.9),
        };
        var events = new[]
        {
            Event(1, side: 0, estimatedStart: 301.4),
            Event(2, side: 1, estimatedStart: 327.6),
        };

        var comparison = new LightTimelineDiagnosticComparer().Compare(intervals, events);

        Assert.Equal(2.4, comparison.EstimatedStartOffsetSeconds!.Value, precision: 1);
        Assert.Equal(2, comparison.MatchedCount);
        Assert.Equal(1, comparison.VisualWithoutBehavioralCount);
        Assert.Equal(0, comparison.BehavioralWithoutVisualCount);
        Assert.Equal(DiagnosticComparisonStatus.VisualWithoutBehavioral, comparison.Rows[0].Status);
        Assert.Collection(
            comparison.Rows.Where(row => row.Status == DiagnosticComparisonStatus.Matched),
            row => Assert.Equal(1, row.BehavioralEvent!.EventNumber),
            row => Assert.Equal(2, row.BehavioralEvent!.EventNumber));
    }

    [Fact]
    public void Compare_IgnoraFilasPosterioresCuandoElAnalisisSoloCubreParteDelVideo()
    {
        var intervals = new[]
        {
            Interval(1, LightId.FoodRight, 12, 18),
            Interval(2, LightId.FoodLeft, 32, 38),
        };
        var events = new[]
        {
            Event(0, side: 0, estimatedStart: 7.5),
            Event(1, side: 0, estimatedStart: 10),
            Event(2, side: 1, estimatedStart: 30),
            Event(3, side: 0, estimatedStart: 700),
        };

        var comparison = new LightTimelineDiagnosticComparer().Compare(
            intervals,
            events,
            scanStartSeconds: 10,
            scanEndSeconds: 600);

        Assert.Equal(2, comparison.MatchedCount);
        Assert.Equal(0, comparison.BehavioralWithoutVisualCount);
    }

    private static LightEventInterval Interval(int sequence, LightId light, double on, double off) =>
        new(sequence, light, (int)(on * 30), on, (int)(off * 30), off);

    private static BehavioralEvent Event(int number, int side, double estimatedStart) =>
        new(number, side, 0, 5, estimatedStart + 5, 0, 0, 0, BehavioralEventType.SafeFood, []);
}
