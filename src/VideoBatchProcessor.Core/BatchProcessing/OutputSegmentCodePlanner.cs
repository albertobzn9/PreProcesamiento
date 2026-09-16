using VideoBatchProcessor.Core.SegmentPlanning;
using System.Globalization;

namespace VideoBatchProcessor.Core.BatchProcessing;

/// <summary>
/// Asigna el código breve que aparece en el nombre de cada clip exportado.
/// Todos los eventos conservan el número cronológico de su fuente como
/// <c>eNN</c>; el resultado (<c>cr</c>, <c>nc</c> o <c>to</c>) vive en su
/// propio campo. Los ITIs se nombran <c>itiNN</c> según el evento anterior.
/// </summary>
public static class OutputSegmentCodePlanner
{
    public static IReadOnlyDictionary<PlannedVideoSegment, string> Create(
        IEnumerable<PlannedVideoSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var codes = new Dictionary<PlannedVideoSegment, string>();
        foreach (var segment in segments.OrderBy(item => item.Sequence))
        {
            var code = segment.Kind switch
            {
                PlannedSegmentKind.InitialHabituation => "habini",
                PlannedSegmentKind.FinalHabituation => "habfin",
                PlannedSegmentKind.InterTrialInterval => $"iti{FormatEventNumber(segment)}",
                PlannedSegmentKind.Event => $"e{FormatEventNumber(segment)}",
                _ => throw new ArgumentOutOfRangeException(nameof(segments)),
            };

            codes.Add(segment, code);
        }

        return codes;
    }

    private static string FormatEventNumber(PlannedVideoSegment segment) =>
        (segment.BehavioralEventNumber ?? segment.Sequence)
            .ToString("D2", CultureInfo.InvariantCulture);
}
