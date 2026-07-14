# Eric Handoff - Light Module

> Historical handoff. This document is not an active work plan.

## Status

**Closed:** 13 July 2026.

Eric's responsibility in Video Batch Processor ended with the light-detection
module. No further backend, frontend, testing, or integration work is assigned
to him in this repository.

## Delivered Foundation

The project now has a reviewable route from a prepared video frame to the state
of the three lights:

```text
video frame -> FrameAnalyzer -> BrightnessAdapter -> LightDetection -> LightSample
```

The current repository also contains the UI integration that marks circular
ROIs, calibrates them with real OFF/ON frames, and preserves those references
when a ROI is adjusted. These later integration changes are owned by the active
project work, not by this handoff.

## Current Ownership

AB is now responsible for the project's remaining backend and frontend work:

1. `SegmentPlanner`, behavioral-source integration, and review findings.
2. Reusable `CameraProfile` persistence.
3. Review UI, export, batch orchestration, and validation with real
   CMC sessions.

## Verification Baseline

Run from the repository root:

```bash
dotnet build VideoBatchProcessor.sln
dotnet test VideoBatchProcessor.sln
```

The accepted baseline after the timeline implementation is **146 passing tests**. The manual
CameraSetup, ROI, and calibration checks are in
[SessionSetup, CameraSetup, LightMarker And Calibration Manual Test](session-setup-manual-test.md).

## Work Completed After The Handoff

`LightTimelineBuilder` and `LightTimelineScanner` are now implemented by the
active project work. They convert `LightSample` values into stable ON/OFF
transitions, ignore isolated artifacts and scan a prepared video using the same
camera configuration and ROIs as calibration. They still do not decide trials,
ITIs, habituation or export clips.

## References

- [Architecture](../project/architecture.md)
- [Current Project Status](../project/current-status.md)
- [Historical MAT Format](../reference/mat-format.md)
- [Behavioral Data And Video Synchronization Guide](mat-video-synchronization-guide.md)
