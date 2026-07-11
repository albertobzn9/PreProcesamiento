# MAT And Video Synchronization Guide

## Why The `.mat` Is Part Of This Project

Video Batch Processor does not use the `.mat` file to cut the video directly.
It uses it to understand the behavioral meaning of the visual events found in
the video.

The two sources answer different questions:

| Source | What it tells us |
|--------|------------------|
| Video | When food lights and the noise LED visibly turn ON/OFF; the exact frames that can become clip boundaries. |
| `.mat` | Which behavioral event MATLAB registered, side, stimulation, lever latency, crossing latency, and behavioral result. |

Neither source replaces the other. The video gives the visual timeline; the
`.mat` gives the behavioral record.

## The Intended Result

For each relevant event, the program should be able to say:

```text
This clip begins in video frame X because the relevant visual signal began.
It corresponds to .mat event N and has this behavioral label.
```

For example, a risk event can produce a clip that starts when the noise LED
turns on, while its MATLAB lever latency starts a few seconds later when the
food light turns on. Both times are valid; they describe different parts of the
same event.

## What A `.mat` Row Means

A row is an event recorded by MATLAB, not automatically a successful crossing.
It can describe:

- a crossing;
- a lever press on the same side without crossing;
- a timeout;
- in new sessions, a sound-only control event.

Historical files have eight columns. New CP or DIS files with sound-only events
have a ninth column, `TipoEvento`:

```text
0 = safe event with food
1 = conflict/risk event with food
2 = sound-only event: LED + noise + grid, without food light or reward
```

The parser must read this information; it must never modify or rename the
source `.mat`.

## Why Synchronization Is Approximate

The video and MATLAB do not start from a guaranteed shared clock. There can be
recording delay, MATLAB timing delay, and manually determined habituation
boundaries.

Therefore the goal is **not** to force every `.mat` timestamp onto one exact
video frame. The goal is to find the best supported association and make any
uncertainty visible to the reviewer.

```text
Video:  Noise LED ON -------- Food light ON -------- Food light OFF
MATLAB:                         event/latency begins
```

For a normal risk event, the output clip may start at `Noise LED ON`. The food
light ON/OFF frames remain visual references that are compared with MATLAB's
event start estimate and lever-press time; they are not assumed identical.

The detailed measurement, offset-estimation, and validation rules are defined
in [CajaValentia Video-MAT Synchronization](../project/sincronizacion-video-mat-cajavalentia.md).
In particular, it derives the MATLAB event-start estimate as `TiempoAbs -
Latencia`, measures both the start gap and the post-press light tail, and
estimates their session patterns from multiple matched events rather than
assuming a fixed delay.

## Future Processing Flow

```text
VideoReader -> FrameAnalyzer -> BrightnessAdapter -> LightDetection -> LightTimelineBuilder
                                                                    |
.mat file -> MatParser -> MatEvent[] ------------------------------+-> SegmentPlanner -> VideoSegment
```

The responsibilities are deliberately separate:

- `LightTimeline` says what the camera observed and when.
- `MatEvent` says what MATLAB recorded and how the rat responded.
- `SegmentPlanner` pairs them, proposes clip boundaries, and produces warnings
  when the association is doubtful.

## Practical Matching Rules

When this functionality is implemented, use these rules:

1. Confirm that the video and `.mat` belong to the same session using their
   metadata and file relationship.
2. Detect stable food-light and noise-LED transitions from the video.
3. Read `.mat` rows in recorded order.
4. Match visual events to `.mat` events in order, using repeated light events
   and timestamps as evidence rather than assuming perfect equality.
5. Keep the video frame boundaries as the visual source of truth.
6. Keep `.mat` labels for crossing, no crossing, timeout, latencies, and
   `TipoEvento` as the behavioral source of truth.
7. Report unpaired events, implausible timing differences, or missing rows for
   user review instead of silently guessing.

## Sound-Only Events

For `TipoEvento = 2`, there is no food light. The visible anchor is the noise
LED, so the proposed clip begins with that LED transition. The program must not
label this event as safe or as conflict-with-food, and it must not automatically
apply normal crossing/no-crossing/timeout logic to it.

## Excel Is Not The Source

Excel files are useful for a person to inspect data, but the parser must work
from `.mat` files. If Excel is present later, it can be compared as an extra
check. A disagreement between Excel, `.mat`, and video is a warning to report;
the video remains the visual evidence and the `.mat` remains the behavioral
record.

## What Eric Should Do With This Now

This guide explains the reason for the future `MatParser` and `SegmentPlanner`.
They are not part of the active July block yet. For the current work, keep the
frame-to-light path clean so it can later provide the visual timeline needed by
this synchronization.

When `MatParser` begins, its minimum acceptance criteria are:

- accept `N x 8` historical and `N x 9` sound-only `.mat` matrices;
- preserve source precision internally;
- expose `MatEvent` values without changing the source file;
- test normal, no-crossing, timeout, and `TipoEvento = 2` cases;
- produce warnings for unsupported matrix shapes.

`SegmentPlanner`, not `MatParser`, is responsible for warnings about uncertain
video-to-event associations.

## Related Documentation

- [MAT Format](../reference/mat-format.md)
- [Operational Terms](../reference/operational-terms.md)
- [Architecture](../project/architecture.md)
- [CajaValentia Video-MAT Synchronization](../project/sincronizacion-video-mat-cajavalentia.md)
- [Eric Workplan](eric-workplan.md)
