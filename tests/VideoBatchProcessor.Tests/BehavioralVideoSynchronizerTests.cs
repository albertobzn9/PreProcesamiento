using VideoBatchProcessor.Core.BehavioralData;
using VideoBatchProcessor.Core.BehavioralSynchronization;
using VideoBatchProcessor.Core.Diagnostics;
using VideoBatchProcessor.Core.LightDetection;

namespace VideoBatchProcessor.Tests;

public sealed class BehavioralVideoSynchronizerTests
{
    [Fact]
    public void Synchronize_CuandoTresEventosCoincidenMarcaListo()
    {
        var result = Synchronize(
            [
                Interval(1, LightId.FoodRight, 102.4),
                Interval(2, LightId.FoodLeft, 122.4),
                Interval(3, LightId.FoodRight, 142.4),
            ],
            [
                Event(1, 0, 100),
                Event(2, 1, 120),
                Event(3, 0, 140),
            ]);

        Assert.Equal(BehavioralVideoSynchronizationStatus.Ready, result.Status);
        Assert.Equal(3, result.Comparison.MatchedCount);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Synchronize_CuandoHayUnaSenalExtraMarcaRevisionPeroMantieneCoincidencias()
    {
        var result = Synchronize(
            [
                Interval(1, LightId.FoodRight, 50),
                Interval(2, LightId.FoodRight, 102.4),
                Interval(3, LightId.FoodLeft, 122.4),
                Interval(4, LightId.FoodRight, 142.4),
            ],
            [
                Event(1, 0, 100),
                Event(2, 1, 120),
                Event(3, 0, 140),
            ]);

        Assert.Equal(BehavioralVideoSynchronizationStatus.Warning, result.Status);
        Assert.Equal(3, result.Comparison.MatchedCount);
        Assert.Equal(1, result.Comparison.VisualWithoutBehavioralCount);
        Assert.Contains(result.Findings, item =>
            item.Kind == BehavioralVideoSynchronizationFindingKind.VisualWithoutBehavioral);
    }

    [Fact]
    public void Synchronize_CuandoNoHayPatronCompatibleBloqueaYMarcaPosibleDesajuste()
    {
        var result = Synchronize(
            [
                Interval(1, LightId.FoodLeft, 100),
                Interval(2, LightId.FoodRight, 120),
            ],
            [
                Event(1, 0, 100),
                Event(2, 1, 110),
            ]);

        Assert.Equal(BehavioralVideoSynchronizationStatus.Blocked, result.Status);
        Assert.Contains(result.Findings, item =>
            item.Kind == BehavioralVideoSynchronizationFindingKind.PossibleSourceMismatch);
    }

    [Fact]
    public void Synchronize_UsaLadosConocidosComoAnclaYEmpataTimeoutPorTiempo()
    {
        var result = Synchronize(
            [
                Interval(1, LightId.FoodRight, 102.4),
                Interval(2, LightId.FoodRight, 122.4),
                Interval(3, LightId.FoodLeft, 142.4),
            ],
            [
                Event(1, 0, 100),
                Event(2, -2, 120),
                Event(3, 1, 140),
            ]);

        Assert.Equal(BehavioralVideoSynchronizationStatus.Ready, result.Status);
        Assert.Equal(3, result.Comparison.MatchedCount);
        Assert.Contains(result.Comparison.Rows, row =>
            row.Status == DiagnosticComparisonStatus.Matched &&
            row.BehavioralEvent?.Side == -2);
    }

    private static BehavioralVideoSynchronizationResult Synchronize(
        IReadOnlyList<LightEventInterval> intervals,
        IReadOnlyList<BehavioralEvent> events) =>
        new BehavioralVideoSynchronizer().Synchronize(
            intervals,
            events,
            new LightTimelineScanRange(0, 9_000),
            videoFps: 30,
            behavioralSourcePath: "/tmp/session.mat");

    private static LightEventInterval Interval(int sequence, LightId light, double on) =>
        new(sequence, light, (int)(on * 30), on, (int)((on + 5) * 30), on + 5);

    private static BehavioralEvent Event(int number, int side, double estimatedStart) =>
        new(number, side, 0, 5, estimatedStart + 5, 0, 0, 0, BehavioralEventType.SafeFood, []);
}
