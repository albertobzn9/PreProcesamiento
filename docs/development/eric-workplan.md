# Eric Workplan - July 2026

## Current Block

**Start:** Monday, 13 July 2026.

This is the only active development plan for Eric. It replaces the completed
June LightDetection plan and the older handoff guides.

## Verified Starting Point

The following backend modules already exist and must be extended, not rewritten:

- `NomenclatureParser`: parses legacy sessions, current lab source sessions,
  and Video Batch Processor output names.
- `SessionMetadataResolver`: completes missing metadata and gives the UI a
  clear list of missing fields.
- `VideoReader`: opens video, exposes metadata, and reads frames.
- `FrameAnalyzer`: measures real ROI brightness from OpenCV frames.
- `LightDetection`: converts the three ROI brightness readings into ON/OFF
  states for `FoodLeft`, `FoodRight`, and `NoiseLed`.

Verification command:

```bash
./scripts/test-macos.sh
```

Baseline on 10 July 2026: 109 tests pass. Run this command before beginning
work and before each delivery.

## Goal Of This Block

Create one reviewable backend path:

```text
video frame -> FrameAnalyzer -> brightness adapter -> LightDetector -> LightSample
```

Then create `LightTimelineBuilder`, which turns many `LightSample` values into
stable light transitions. This block does not include GUI work, `.mat` parsing,
segment planning, or clip export.

## Working Rules

- Work about four hours each weekday, Monday through Friday.
- Keep changes inside `VideoBatchProcessor.Core` and its matching tests.
- Do not change names, architecture, or file conventions without first
  documenting the reason and reviewing it.
- Each delivery must compile, pass all tests, and be independently reviewable.
- After each completed module, stop for a functional review before starting the
  next one.

## Week 1: Connect Real Frames To LightDetection

**Dates:** 13-17 July 2026.

### Monday 13: Baseline And Design

- Run `./scripts/test-macos.sh` and record the result.
- Read `FrameAnalyzer`, `LightDetection`, and their tests.
- Define the smallest mapping between `LightRoi`/`LightId` and
  `RoiDefinition`/`TipoLed` without renaming either model yet.

**Reviewable output:** short implementation note in the pull/commit message and
a proposed class boundary for the adapter.

### Tuesday-Wednesday 14-15: Brightness Adapter

- Add an adapter such as `OpenCvFrameBrightnessSource` that implements
  `IFrameBrightnessSource`.
- The adapter receives a real OpenCV `Mat`, uses `FrameAnalyzer` for pixel
  measurement, and gives `LightDetector` the brightness for each `LightRoi`.
- Keep pixel analysis in `FrameAnalyzer`; do not duplicate brightness logic in
  `LightDetector`.

**Reviewable output:** unit tests with synthetic OpenCV frames showing that
known bright and dark ROIs become the expected `LightSample` values.

### Thursday 16: Integration Tests

- Add tests for the complete frame-to-light path.
- Cover all OFF, one food light ON, `NoiseLed` ON, and a ROI outside the frame.
- Verify that frame index and timestamp survive in `LightSample`.

**Reviewable output:** focused tests plus a short list of known limitations.

### Friday 17: First Real-Video Smoke Test

- Use one short, approved CMC video only for a manual smoke test.
- Confirm that the three marked ROIs produce plausible ON/OFF readings on
  selected frames.
- Do not add session data or large videos to the repository.

**Reviewable output:** command, video-independent test evidence, and any issue
found in ROI selection or thresholds.

## Week 2: LightTimelineBuilder

**Dates:** 20-24 July 2026.

### Monday-Tuesday 20-21: Data And Rules

- Create `LightTimelineBuilder`, `LightTransition`, and `LightTimeline` in the
  Core library.
- Define configuration for minimum consecutive ON/OFF frames.
- The module receives `LightSample` values and produces stable transitions; it
  must not decide trials, ITIs, habituation, or crossing.

### Wednesday 22: Unit Tests

- Test a stable OFF-to-ON transition.
- Test a single-frame bright artifact that must be ignored.
- Test a stable ON-to-OFF transition.
- Test the three lights independently.

### Thursday 23: Integration Review

- Feed samples from the Week 1 path into `LightTimelineBuilder`.
- Confirm that the timeline preserves frame index, timestamp, light identity,
  and ON/OFF direction.

### Friday 24: Delivery Review

- Run the full suite and build the solution.
- Present the new classes, tests, and one short example sequence.
- Stop after review; do not start `MatParser` without an agreed `.mat` fixture.

## Out Of Scope For This Block

- `MatParser`, including the future ninth `TipoEvento` column.
- `SegmentPlanner`, ITI/habituation rules, and crossing classification.
- Output code for the sound-only event.
- FFmpeg export, batch orchestration, and final GUI screens.

## Next Decision After Review

After the 24 July review, choose the next module based on evidence:

1. `MatParser` with real 8-column and 9-column `.mat` fixtures; or
2. a thin UI connection that lets the user select ROIs and inspect the verified
   frame-to-light path.

## References

- [Architecture](../project/architecture.md)
- [Architecture Review Log](../project/architecture-review-log.md)
- [MAT Format](../reference/mat-format.md)
- [MAT And Video Synchronization Guide](mat-video-synchronization-guide.md)
- [Naming Convention](../reference/naming-convention.md)
