# CajaValentia Session Capture Integration

[Volver al indice de documentacion](../README.md)

## Purpose

This document defines the future handoff from CajaValentia to Video Batch
Processor. It is a design contract, not current runtime behavior.

The desired workflow is one action in CajaValentia that starts an OBS recording
and a behavioral session in a known order. Once the training ends, it produces
one completed session package that this application can process without manual
file matching or speculative video-to-table offsets.

The matching decision in CajaValentia lives in its companion document:

```text
/Users/ab/Documents/GitHub/CajaValentia/docs/decisions/2026-07-13-obs-session-clock-and-preprocessing-handoff.md
```

## Shared Session Clock Contract

The word "shared" does not mean that OBS and MATLAB magically use the same
frame clock. OBS recording is asynchronous. The contract is a controlled
causal sequence:

1. CajaValentia creates `session_id`, `session_stem` and a manifest.
2. CajaValentia requests OBS recording and waits for a positive confirmation.
3. Only after that confirmation does CajaValentia create behavioral `R0` and
   begin habituation/task execution.
4. Behavioral rows continue to use `toc(R0)` as their raw, authoritative
   duration/time reference.
5. The manifest records UTC confirmation and `R0`-start timestamps, the video
   path and the behavioral output paths.

This eliminates ambiguity about which video belongs to which task and measures
the start-order relationship. It does not erase camera/OBS latency. The
existing synchronization analysis still compares detected light changes against
behavioral events to quantify any residual delay.

## Session Package V1

CajaValentia will create a flat UTF-8 `session_manifest_v1.csv` using two
columns, `key,value`. This avoids a new JSON dependency in MATLAB R2011a while
remaining simple for .NET to parse. The manifest is a first-class behavioral
source locator, not a naming hint.

Required keys:

| Key | Use in Video Batch Processor |
|---|---|
| `schema_version` | Enables a controlled future format change. |
| `session_id` | Stable identity across video, behavioral data and report. |
| `status` | Reject incomplete/failed sessions from automatic processing. |
| `session_stem` | Human-readable base name; never the sole identity. |
| `obs.recording_confirmed_utc` | Records that OBS was actively recording before `R0`. |
| `behavior.clock_name` | Expected value: `R0`. |
| `behavior.clock_started_utc` | Wall-clock counterpart of behavioral zero. |
| `behavior.result_csv` | Explicit path to the main event CSV. |
| `behavior.presses_csv` | Optional path to the presses CSV. |
| `video.recording_path` | Explicit path to the completed OBS video. |
| `behavior.task_finished_utc` | Enables end-of-session audit. |
| `obs.recording_stopped_utc` | Confirms video finalization. |

## Expected Processing Flow

When the package has `status=completed`, the future headless processor will:

1. Read the manifest from an explicit path.
2. Validate that the video and main behavioral source exist and belong to the
   same `session_id`.
3. Load the camera profile, behavioral events and optional presses evidence.
4. Scan the video, detect light transitions and retain timing evidence.
5. Compare visual events with `toc(R0)`-based rows, preserving raw values and
   calculating residual gaps for review.
6. Generate clips, reports and warnings into a session-specific output folder.

The desktop UI remains useful for inspection and configuration. The automatic
post-session action will require a separate headless orchestration entry point;
it is not implemented yet and must not be represented as working today.

## Visual Synchronization Anchor

The manifest plus start order solves identity and logical session timing. To
validate frame-level capture latency, a later lab-approved test may add a brief
visual marker that is both visible to the camera and logged by CajaValentia.

It must first be tested without an animal. It must not reuse, add or alter a
task stimulus in a way that changes the behavioral protocol. Its purpose is to
measure residual camera/recording delay, not to replace `R0`.

## Current Compatibility Gate

CajaValentia now exports a validated main CSV with **10 columns**, where column
10 is `ensayo_cruce`. The current `CsvBehavioralSessionReader` in this project
still validates an earlier 9-column CSV V1 contract. Therefore automatic
handoff is blocked until this project accepts the 10-column current contract or
adds an explicit compatible version adapter.

The valid solutions are:

- support current CSV V2/10-column files while preserving `ensayo_cruce`; or
- introduce a versioned reader selected by the manifest and keep historical
  9-column fixtures supported.

Do not silently discard the new column and do not ask CajaValentia to regress a
validated export. This is a backend compatibility task before an automated run,
not an operator workaround.

## Current Boundaries

- CajaValentia owns OBS control, the behavioral task, hardware and final
  behavioral files.
- Video Batch Processor owns video analysis, visual calibration, matching,
  clipping and reporting.
- Neither application should modify the other application's experimental rules.
- Existing historical MAT and independent videos remain supported through the
  current reconstruction workflow.

## Implementation Order

1. CajaValentia: implement and validate manifest + OBS confirmation + `R0`
   ordering with no animal.
2. Video Batch Processor: add versioned 10-column CSV compatibility and
   manifest reader tests.
3. Video Batch Processor: complete the sequential light timeline and behavioral
   matching pipeline.
4. Both projects: run short supervised capture tests; compare visual onset
   against `R0`-derived event timing and record the residual gap.
5. Only then: enable automatic post-session preprocessing.

## Related Documentation

- [Video-Behavior Synchronization](sincronizacion-video-mat-cajavalentia.md):
  current reconstruction and residual-gap measurements.
- [CajaValentia CSV Backend Handoff](handoff-cajavalentia-csv-backend.md):
  previous CSV/MAT transition notes. Its 9-column CSV contract is now
  superseded for current CajaValentia sessions by the compatibility gate above.
- [Future Integration Context](future-integration-context.md): project-level
  relationship and long-term boundaries.
