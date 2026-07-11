# Operational Terms And Decision Rules

> Return to the [documentation index](../README.md).

This is the shared vocabulary for Video Batch Processor. Use it when a term is
needed by more than one area of the project: protocol interpretation, `.mat`
parsing, video segmentation, output naming, tests, or UI review.

It does not replace the [CMC Protocol](../protocol/cmc-protocol.md), the
[MAT Format](mat-format.md), or the [Architecture](../project/architecture.md).
Those documents own experimental detail, column definitions, and module design.

## Core Terms

| Term | Operational meaning |
|------|---------------------|
| **Session** | Complete video from one rat, day, and protocol phase. It can contain initial habituation, events, ITIs, and final habituation. |
| **Event** | Behavioral record in the `.mat`. A row is an event, not automatically a successful crossing. |
| **Trial** | Visually detectable period defined by the relevant food light or noise LED. |
| **Output clip** | Video fragment exported by this program. It can represent an event, ITI, or habituation. |
| **`MatEvent`** | Data object created by `MatParser` from one `.mat` row. It carries behavioral information such as latencies, result, and optional `TipoEvento`. |
| **`LightTimeline`** | Data object built from video frames. It records stable ON/OFF transitions of `FoodLeft`, `FoodRight`, and `NoiseLed`. |
| **`VideoSegment`** | Proposed interval of the source video, with start/end frames and an operational label, ready for review or export. |
| **Same-side event** | The food light appears on the side where the rat already is. It can include lever pressing without a crossing. |
| **Crossing** | Complete displacement from one end to the opposite side. The normal automatic case requires a `Lado` change and `Desplaz > 1 s`. |
| **No crossing** | Lever pressing on the same known side with `Desplaz <= 1 s`. It is different from a timeout. |
| **Short side-change discrepancy** | `Lado` changes but `Desplaz <= 1 s`. This can happen when the rat was already in the middle of the alley. It is not automatically a complete crossing and requires video review. |
| **`InterEventCrossing`** | `Lado` remains the same but `Desplaz > 1 s`. The rat may have crossed during the short ITI and was back on the same side when the next event was recorded. It is a review finding, not an automatic final decision to count the event as a crossing. |
| **Behavioral review finding** | Exception detected from `.mat` and video evidence that needs the investigator's decision. The program reports the pattern and preserves its evidence; it does not decide the experimental inclusion criterion. |
| **Timeout** | Event where the rat does not complete the required behavior before the phase limit. Historical `.mat` data usually has `Lado = -2` and values near that limit. |
| **ITI** | Interval between events, with no relevant food light or noise LED active. It may be short in CS/DIS and long in CP. |
| **Initial habituation** | Beginning of the session without relevant signals. The original protocol uses five minutes. |
| **Final habituation** | End of the session without relevant signals. Its real duration can vary because the end is manually determined. |

## Event Categories

| Category | Visible signals | Behavioral meaning |
|----------|-----------------|--------------------|
| **Safe** | Food light, no noise LED | Food event without conflict. |
| **Conflict** | Food light plus noise LED | Risk/conflict event with food. The LED can begin before the food light. |
| **Sound-only** | Noise LED, aversive sound, and grid; no food light | Control event without food or reward. In new `.mat` files, `TipoEvento = 2`. |

The first food event of a session is always safe. This is useful context and a
sanity check, but it must never replace actual light detection or `.mat`
reading.

## Timing Terms

| Term | Meaning |
|------|---------|
| **Warning period** | In a conflict event, the interval from noise LED/sound onset to food-light onset. The output clip can include it. |
| **MATLAB event start** | Internal instant from which MATLAB measures lever latency. It is compared with the food-light onset seen in video; the two references can differ. |
| **Absolute session time** | `TiempoAbs`: total seconds since habituation was started. For a lever event, it records the MATLAB time of that event. |
| **Lever latency** | `Latencia`: duration from MATLAB event start to lever press, not an absolute timestamp. |
| **Crossing latency** | `Desplaz`: latency associated with the displacement sensor. Together with the consecutive `Lado` values, it identifies normal crossings and behavioral review findings. |
| **Video-to-MAT association** | Reviewable pairing between a visual event and a `.mat` event. It measures start gap and post-press light tail because video and MATLAB do not share a guaranteed clock. |

## Decision Rules

1. The video is the source of truth for visible signal timing and clip frame boundaries.
2. The `.mat` is the source of truth for behavioral labels, latencies, and
   `TipoEvento` when it exists.
3. Excel is an auxiliary review format, never the required parser input.
4. Do not modify or rename source `.mat` files.
5. `eN` in an output filename means the event number associated with the `.mat`;
   it does not mean a successful crossing.
6. Use `na` for output fields that do not apply to ITI or habituation.
7. For conflict clips, keep separate the visual warning start (`NoiseLed`),
   the food-light onset observed in video, and the MATLAB event-start estimate.
8. For `TipoEvento = 2`, use the noise LED as the visible anchor. Do not label
   the event as safe or conflict-with-food, and do not automatically apply
   normal crossing/no-crossing/timeout rules.
9. When video and `.mat` disagree, preserve the discrepancy as a warning for
   review instead of silently forcing a match.
10. A normal automatic crossing requires `Lado` to change (`0 -> 1` or
    `1 -> 0`) and `Desplaz > 1 s`. If `Lado` stays the same with
    `Desplaz > 1 s`, emit an `InterEventCrossing` review finding. If `Lado`
    changes but `Desplaz <= 1 s`, emit a `ShortSideChange` review finding; the
    rat may have started from the middle of the alley. The program must not
    decide whether either exception counts in a particular analysis. A short
    or long lever latency never replaces these rules or other `.mat` fields.
11. A timeout or a missing/unknown previous side breaks the side sequence. Do
    not infer a complete crossing from the next event without review.
