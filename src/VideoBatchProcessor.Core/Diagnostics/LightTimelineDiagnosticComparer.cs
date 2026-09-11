using VideoBatchProcessor.Core.BehavioralData;
using VideoBatchProcessor.Core.LightDetection;

namespace VideoBatchProcessor.Core.Diagnostics;

/// <summary>
/// Empata evidencia visual con filas conductuales para diagnóstico. Estima un
/// desfase por sesión a partir de coincidencias repetidas de lado y tiempo; no
/// decide segmentos ni sustituye la revisión del investigador.
/// </summary>
public sealed class LightTimelineDiagnosticComparer
{
    private const double OffsetClusterToleranceSeconds = 0.75d;
    private const double PairingToleranceSeconds = 0.75d;

    public LightTimelineDiagnosticComparison Compare(
        IReadOnlyList<LightEventInterval> intervals,
        IReadOnlyList<BehavioralEvent> events,
        double? scanStartSeconds = null,
        double? scanEndSeconds = null)
    {
        ArgumentNullException.ThrowIfNull(intervals);
        ArgumentNullException.ThrowIfNull(events);

        var visual = intervals
            .Where(IsFoodLight)
            .OrderBy(interval => interval.OnTimeSeconds)
            .ToArray();
        var behavioral = events
            .OrderBy(item => item.AbsoluteTimeSeconds)
            .ToArray();
        var offset = EstimateOffset(visual, behavioral);
        if (offset is null)
            return WithoutReliableAlignment(visual, behavioral);

        var behavioralInScanRange = behavioral
            .Where(item => IsInsideVisualScanRange(
                EstimatedMatlabStart(item) + offset.Value,
                scanStartSeconds,
                scanEndSeconds))
            .ToArray();

        var pairedBehavioralIndexes = new HashSet<int>();
        var rows = new List<LightTimelineDiagnosticComparisonRow>();
        var lastMatchedBehavioralIndex = -1;

        foreach (var interval in visual)
        {
            var matchedIndex = FindBestCompatibleBehavioral(
                interval,
                behavioralInScanRange,
                pairedBehavioralIndexes,
                lastMatchedBehavioralIndex,
                offset.Value);

            if (matchedIndex is null)
            {
                rows.Add(new LightTimelineDiagnosticComparisonRow(
                    interval,
                    null,
                    null,
                    DiagnosticComparisonStatus.VisualWithoutBehavioral,
                    "Señal visual sin fila MAT compatible; revisar habituación, ROI o umbral."));
                continue;
            }

            var behavioralIndex = matchedIndex.Value;
            var behavioralEvent = behavioralInScanRange[behavioralIndex];
            var rawStartDifference = interval.OnTimeSeconds - EstimatedMatlabStart(behavioralEvent);
            rows.Add(new LightTimelineDiagnosticComparisonRow(
                interval,
                behavioralEvent,
                rawStartDifference - offset.Value,
                DiagnosticComparisonStatus.Matched,
                "Empatado por lado y patrón temporal; diagnóstico, no sincronización final."));
            pairedBehavioralIndexes.Add(behavioralIndex);
            lastMatchedBehavioralIndex = behavioralIndex;
        }

        for (var index = 0; index < behavioralInScanRange.Length; index++)
        {
            if (pairedBehavioralIndexes.Contains(index))
                continue;

            rows.Add(new LightTimelineDiagnosticComparisonRow(
                null,
                behavioralInScanRange[index],
                null,
                DiagnosticComparisonStatus.BehavioralWithoutVisual,
                "Fila MAT sin señal visual compatible dentro del intervalo analizado."));
        }

        return new LightTimelineDiagnosticComparison(offset, rows);
    }

    private static LightTimelineDiagnosticComparison WithoutReliableAlignment(
        IReadOnlyList<LightEventInterval> visual,
        IReadOnlyList<BehavioralEvent> behavioral)
    {
        var rows = visual
            .Select(interval => new LightTimelineDiagnosticComparisonRow(
                interval,
                null,
                null,
                DiagnosticComparisonStatus.VisualWithoutBehavioral,
                "No hubo evidencia suficiente para estimar un desfase de esta sesión."))
            .Concat(behavioral.Select(item => new LightTimelineDiagnosticComparisonRow(
                null,
                item,
                null,
                DiagnosticComparisonStatus.BehavioralWithoutVisual,
                "No hubo evidencia suficiente para estimar un desfase de esta sesión.")))
            .ToArray();
        return new LightTimelineDiagnosticComparison(null, rows);
    }

    private static double? EstimateOffset(
        IReadOnlyList<LightEventInterval> visual,
        IReadOnlyList<BehavioralEvent> behavioral)
    {
        var candidates = (
            from interval in visual
            from item in behavioral
            where HasKnownSide(item.Side) && IsCompatibleSide(interval.Light, item.Side)
            select interval.OnTimeSeconds - EstimatedMatlabStart(item))
            .OrderBy(value => value)
            .ToArray();

        if (candidates.Length < 2)
            return null;

        double[]? strongestCluster = null;
        foreach (var candidate in candidates)
        {
            var cluster = candidates
                .Where(value => Math.Abs(value - candidate) <= OffsetClusterToleranceSeconds)
                .ToArray();
            if (strongestCluster is null || cluster.Length > strongestCluster.Length)
                strongestCluster = cluster;
        }

        return strongestCluster is { Length: >= 2 }
            ? Median(strongestCluster)
            : null;
    }

    private static int? FindBestCompatibleBehavioral(
        LightEventInterval interval,
        IReadOnlyList<BehavioralEvent> behavioral,
        ISet<int> pairedIndexes,
        int lastMatchedIndex,
        double offset)
    {
        return Enumerable.Range(lastMatchedIndex + 1, behavioral.Count - lastMatchedIndex - 1)
            .Where(index => !pairedIndexes.Contains(index) && IsCompatibleForPairing(interval.Light, behavioral[index].Side))
            .Select(index => new
            {
                Index = index,
                Residual = Math.Abs(interval.OnTimeSeconds - EstimatedMatlabStart(behavioral[index]) - offset),
            })
            .Where(item => item.Residual <= PairingToleranceSeconds)
            .OrderBy(item => item.Residual)
            .Select(item => (int?)item.Index)
            .FirstOrDefault();
    }

    private static bool IsFoodLight(LightEventInterval interval) =>
        interval.Light is LightId.FoodLeft or LightId.FoodRight;

    private static bool IsCompatibleSide(LightId light, int side) =>
        (light == LightId.FoodLeft && side == 1) ||
        (light == LightId.FoodRight && side == 0);

    private static bool IsCompatibleForPairing(LightId light, int side) =>
        side == -2 || IsCompatibleSide(light, side);

    private static bool HasKnownSide(int side) => side is 0 or 1;

    private static bool IsInsideVisualScanRange(
        double estimatedVisualStartSeconds,
        double? scanStartSeconds,
        double? scanEndSeconds)
    {
        if (scanStartSeconds is null || scanEndSeconds is null)
            return true;

        return estimatedVisualStartSeconds >= scanStartSeconds.Value &&
               estimatedVisualStartSeconds <= scanEndSeconds.Value;
    }

    public static double EstimatedMatlabStart(BehavioralEvent item) =>
        item.AbsoluteTimeSeconds - item.LeverLatencySeconds;

    private static double Median(IReadOnlyList<double> values)
    {
        var middle = values.Count / 2;
        return values.Count % 2 == 0
            ? (values[middle - 1] + values[middle]) / 2d
            : values[middle];
    }
}

public enum DiagnosticComparisonStatus
{
    Matched,
    VisualWithoutBehavioral,
    BehavioralWithoutVisual,
}

public sealed record LightTimelineDiagnosticComparison(
    double? EstimatedStartOffsetSeconds,
    IReadOnlyList<LightTimelineDiagnosticComparisonRow> Rows)
{
    public int MatchedCount => Rows.Count(row => row.Status == DiagnosticComparisonStatus.Matched);
    public int VisualWithoutBehavioralCount => Rows.Count(row => row.Status == DiagnosticComparisonStatus.VisualWithoutBehavioral);
    public int BehavioralWithoutVisualCount => Rows.Count(row => row.Status == DiagnosticComparisonStatus.BehavioralWithoutVisual);
}

public sealed record LightTimelineDiagnosticComparisonRow(
    LightEventInterval? VisualInterval,
    BehavioralEvent? BehavioralEvent,
    double? StartResidualSeconds,
    DiagnosticComparisonStatus Status,
    string Note);
