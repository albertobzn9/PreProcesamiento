# Future Integration Context: Behavior, Video, And Data

## Purpose

This document records the one-to-two-year context shared by CajaValentia and
Video Batch Processor. It guides present design decisions, but it is **not** a
request to integrate both applications during the current semester.

The source planning document is maintained in:

```text
/Users/ab/Library/CloudStorage/GoogleDrive-jasjabs19@gmail.com/My Drive/workspace/02_gestion/03_planeacion/18_BIG_PICTURE_INTEGRACION_CONDUCTA_VIDEO_DATOS.md
```

## Current Scope

This semester, both projects advance separately:

- **CajaValentia** controls the behavioral task, its GUIs, and hardware.
- **Video Batch Processor** reads video, metadata, and `.mat` files to plan and
  export reviewable video segments.

Neither project should start, control, or depend on the other at runtime yet.
The immediate goal for this repository is a reviewable processing pipeline, not
a unified application.

## Future Outcome

In a later semester, a session orchestrator may validate the session, create a
unique `sessionId`, coordinate behavior and video recording, and keep a
structured record of events, timestamps, files, camera profile, and final state.

That future system should let Video Batch Processor associate the video, `.mat`
file, and session metadata without reconstructing identity from manual file
names or risking swapped files.

## Design Rules For This Repository

The backend should preserve these capabilities while it is built:

- Accept explicit session metadata or a session manifest; filename parsing is a
  useful fallback, not the only source of truth.
- Keep a stable session identity such as `sessionId` whenever it is available.
- Preserve traceable timestamps and distinguish at least: video start,
  habituation start, MATLAB event start, LED or food-light changes, and session
  end.
- Preserve raw behavioral times and any estimated video-MAT offset separately;
  an estimate must never overwrite the source `.mat` values.
- Preserve which `CameraProfile` was used for each session or contiguous group
  of sessions. Crop, orientation, ROIs, calibration references, and accepted
  thresholds are part of the evidence needed to reproduce visual processing.
- Keep the behavioral protocol, hardware control, video recording, storage,
  and clip analysis as separate responsibilities.
- Produce reviewable evidence of how a video segment was associated with its
  metadata and `.mat` events.

## What Is Deliberately Out Of Scope Now

- A runtime dependency on CajaValentia.
- A production database or a shared live service.
- A new hardware controller or a replacement of the current MATLAB system.
- Premature assumptions about the final orchestrator technology.

## Near-Term Definition Of Success

At the end of this semester, the useful shared foundation is:

1. CajaValentia has a safely validated version and records its behavior.
2. Video Batch Processor can process a real session through a reviewable flow.
3. Both projects can describe the same session identity, metadata, timestamps,
   and associated files in compatible terms.

## Related Documentation

- [Architecture](architecture.md): current modules and data flow in this
  repository.
- [Product Requirements](product-requirements.md): what the application must do
  now.
- [MAT Format](../reference/mat-format.md): current behavioral data available
  to the processor.
- [CajaValentia Video-MAT Synchronization](sincronizacion-video-mat-cajavalentia.md): current rule for matching visual and MATLAB time references.
- [Operational Terms](../reference/operational-terms.md): shared operational
  meanings used by parsers, segmenters, and exporters.
