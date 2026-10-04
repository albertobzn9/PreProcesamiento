using VideoBatchProcessor.Core.BatchProcessing;
using VideoBatchProcessor.Core.LightDetection;
using VideoBatchProcessor.Core.SegmentPlanning;
using VideoBatchProcessor.Core.VideoReader;

namespace VideoBatchProcessor.Tests;

public sealed class VisualOnlySegmentPlannerTests
{
    private static readonly VideoMetadata Video = new() { Fps = 30, TotalFrames = 300 };

    [Fact]
    public void Plan_GroupsOverlappingLedAndFoodWithoutInventingBehavioralRows()
    {
        var result = new VisualOnlySegmentPlanner().Plan(Video, new(0, 299),
            [Signal(LightId.NoiseLed, 30, 100), Signal(LightId.FoodRight, 50, 100), Signal(LightId.FoodLeft, 150, 200)]);

        Assert.Equal(5, result.Segments.Count);
        Assert.Equal([0, 30, 101, 150, 201], result.Segments.Select(s => s.StartFrameIndex));
        Assert.Equal([29, 100, 149, 200, 299], result.Segments.Select(s => s.EndFrameIndex));
        var events = result.Segments.Where(s => s.Kind == PlannedSegmentKind.Event).ToArray();
        Assert.Equal(PlannedTrialType.ConflictWithFood, events[0].TrialType);
        Assert.Equal(PlannedTrialType.SafeFood, events[1].TrialType);
        Assert.All(result.Segments, s => { Assert.True(s.IsVideoOnly); Assert.Null(s.BehavioralEventNumber); Assert.Equal(PlannedBehavioralResult.NotApplicable, s.Result); });
        var names = OutputSegmentCodePlanner.Create(result.Segments);
        Assert.Equal("e01", names[events[0]]);
        Assert.Equal("iti01", names[result.Segments[2]]);
        Assert.Equal("e02", names[events[1]]);
        Assert.Contains(result.Warnings, w => w.Kind == SegmentPlanningWarningKind.VideoOnlyEstimate);
    }

    [Fact]
    public void Plan_DarkVideoDoesNotInventEventsOrHabituation()
    {
        Assert.Empty(new VisualOnlySegmentPlanner().Plan(Video, new(0, 299), []).Segments);
    }

    [Fact]
    public void Plan_IncompleteLedIsUnknownAndClampedToRange()
    {
        var result = new VisualOnlySegmentPlanner().Plan(Video, new(60, 199), [Signal(LightId.NoiseLed, 30, null)]);
        var segment = Assert.Single(result.Segments);
        Assert.Equal(60, segment.StartFrameIndex);
        Assert.Equal(199, segment.EndFrameIndex);
        Assert.Null(segment.TrialType);
        Assert.Contains(result.Warnings, w => w.Kind == SegmentPlanningWarningKind.FoodLightEndMissing);
    }

    private static LightEventInterval Signal(LightId light, int start, int? end) =>
        new(1, light, start, start / 30d, end, end / 30d);
}
