using VideoBatchProcessor.Core.LightDetection;
using VideoBatchProcessor.Core.VideoReader;

namespace VideoBatchProcessor.Core.SegmentPlanning;

/// <summary>Opt-in approximation. Visual groups never represent confirmed behavioral rows.</summary>
public sealed class VisualOnlySegmentPlanner
{
    public const string Notice = "VIDEO ONLY - No MAT/CSV synchronization. Clip boundaries and event counts are approximate; crossing, no crossing and timeout are unknown. Habituation and ITI labels are provisional.";

    public SegmentPlanningResult Plan(VideoMetadata video, LightTimelineScanRange range,
        IReadOnlyList<LightEventInterval> intervals)
    {
        range.Validate(checked((int)video.TotalFrames));
        var warnings = new List<SegmentPlanningWarning> { new(SegmentPlanningWarningKind.VideoOnlyEstimate, Notice, null) };
        var signals = intervals.OrderBy(item => item.OnFrameIndex).ToArray();
        var groups = new List<List<LightEventInterval>>();
        var end = -1;
        // Merge overlapping signals so an LED followed by overlapping food light is one visual group.
        foreach (var signal in signals)
        {
            if (signal.OnFrameIndex > range.EndFrame || (signal.OffFrameIndex ?? range.EndFrame) < range.StartFrame)
                continue;
            if (groups.Count == 0 || signal.OnFrameIndex > end)
                groups.Add([]);
            groups[^1].Add(signal);
            end = Math.Max(end, signal.OffFrameIndex ?? range.EndFrame);
        }

        var segments = new List<PlannedVideoSegment>();
        var cursor = range.StartFrame;
        for (var index = 0; index < groups.Count; index++)
        {
            var group = groups[index];
            var start = Math.Max(range.StartFrame, group.Min(item => item.OnFrameIndex));
            var finish = Math.Min(range.EndFrame, group.Max(item => item.OffFrameIndex ?? range.EndFrame));
            if (start > cursor)
                Add(index == 0 ? PlannedSegmentKind.InitialHabituation : PlannedSegmentKind.InterTrialInterval,
                    cursor, start - 1, index == 0 ? null : index);
            var food = group.Where(item => item.Light != LightId.NoiseLed).ToArray();
            var led = group.FirstOrDefault(item => item.Light == LightId.NoiseLed);
            var segment = Add(PlannedSegmentKind.Event, start, finish, index + 1);
            segments[^1] = segment with
            {
                TrialType = food.Length == 0 ? null : led is null ? PlannedTrialType.SafeFood : PlannedTrialType.ConflictWithFood,
                WarningStartFrameIndex = led?.OnFrameIndex,
                FoodLightStartFrameIndex = food.Length == 0 ? null : food.Min(item => item.OnFrameIndex),
                FoodLightEndFrameIndex = food.Length == 0 ? null : food.Max(item => item.OffFrameIndex),
            };
            cursor = finish + 1;
        }
        if (groups.Count > 0 && cursor <= range.EndFrame)
            Add(PlannedSegmentKind.FinalHabituation, cursor, range.EndFrame, null);
        if (signals.Any(item => !item.IsComplete))
            warnings.Add(new(SegmentPlanningWarningKind.FoodLightEndMissing,
                "An observed signal has no OFF transition; its provisional clip ends at the analysis boundary.", null));
        return new(segments, warnings);

        PlannedVideoSegment Add(PlannedSegmentKind kind, int start, int finish, int? number)
        {
            var segment = new PlannedVideoSegment(kind, segments.Count + 1, start, finish,
                start / video.Fps, finish / video.Fps, null, PlannedBehavioralResult.NotApplicable,
                null, null, null, null, null, null, null, null, null, null, null, null,
                VisualEventNumber: number, IsVideoOnly: true);
            segments.Add(segment);
            return segment;
        }
    }
}
