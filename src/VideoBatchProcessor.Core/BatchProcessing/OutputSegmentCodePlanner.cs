using VideoBatchProcessor.Core.SegmentPlanning;

namespace VideoBatchProcessor.Core.BatchProcessing;

/// <summary>
/// Asigna el código breve que aparece en el nombre de cada clip exportado.
/// Los cruces y no cruces llevan contadores independientes para que el nombre
/// sea legible durante la revisión. El número original del evento conductual
/// permanece en <c>clips_exportados.csv</c>.
/// </summary>
public static class OutputSegmentCodePlanner
{
    public static IReadOnlyDictionary<PlannedVideoSegment, string> Create(
        IEnumerable<PlannedVideoSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var codes = new Dictionary<PlannedVideoSegment, string>();
        var crossingCount = 0;
        var noCrossingCount = 0;

        foreach (var segment in segments.OrderBy(item => item.Sequence))
        {
            var code = segment.Kind switch
            {
                PlannedSegmentKind.InitialHabituation => "habini",
                PlannedSegmentKind.FinalHabituation => "habfin",
                PlannedSegmentKind.InterTrialInterval => $"iti{segment.BehavioralEventNumber ?? segment.Sequence}",
                PlannedSegmentKind.Event when segment.Result == PlannedBehavioralResult.Crossing => $"cr{++crossingCount}",
                PlannedSegmentKind.Event when segment.Result == PlannedBehavioralResult.NoCrossing => $"nc{++noCrossingCount}",
                PlannedSegmentKind.Event => $"e{segment.BehavioralEventNumber ?? segment.Sequence}",
                _ => throw new ArgumentOutOfRangeException(nameof(segments)),
            };

            codes.Add(segment, code);
        }

        return codes;
    }
}
