# Architecture Review Log

## Purpose

This is the working record for the architecture review. It preserves the
annotations removed from [Architecture](architecture.md) so that the main
document remains a clean reference for the project.

Status meanings:

- **Applied:** reflected in the current documentation.
- **Pending:** useful requirement that still needs a design or implementation decision.
- **Requires decision:** needs an explicit agreement before code or naming can be finalized.

## Applied Decisions

| Area | Recorded decision | Status |
|------|-------------------|--------|
| Data models | The conceptual data-model section describes information exchanged between modules, not executable modules. | Applied |
| Technical names | The document distinguishes a backend module, class, method, and data model. | Applied |
| Terminology | Use `Input`, `Output`, `Dependencies`, and `Validation/Tests` as the section vocabulary. | Applied |
| Interface wording | Use “interface” for code such as `IFrameBrightnessSource`; use direct wording for data formats and rules instead of “contract”. | Applied |
| Lab source names | The current lab-standard source name identifies a complete session and does not contain `eN` or `s/p`. | Applied |
| Lab source parser | `NomenclatureParser` accepts the six-part lab source name and rejects the former intermediate name with segment/type fields. | Applied |
| Output names | `SegmentCode` and `TrialTypeCode` are assigned only when the program creates output clips. | Applied |
| Metadata fields | The main nomenclature fields are explained in the architecture document. | Applied |
| Frame analysis | Crop, rotation, flip, and light ROIs are selected in the UI. `FrameAnalyzer` mide las ROIs de luces; las transformaciones completas pertenecen a preview/exportación futuros. | Applied |
| Light detection | The conceptual explanation appears before the technical structure. | Applied |
| Light timeline | The timeline is described as filtering unstable frame-level changes into stable transitions. | Applied |
| Segment planner | The module uses the same fixed description structure as the other backend modules. | Applied |
| UI vocabulary | UI view names use the same terms as the backend documentation. | Applied |
| Light calibration | Calibrate every light with separate OFF/ON references. Do not request one frame with all three lights ON because food lights are mutually exclusive. | Applied |
| Camera changes | A `CameraProfile` groups crop, orientation, ROIs, and calibration for the sessions that share an encuadre; the user can create another profile from the session where it changes. | Applied |
| Final habituation review | Highlight only final habituations above the target or below the short-warning limit; let the user select long sessions to trim and keep short sessions as warnings. | Applied |
| MAT association | Pair video and `.mat` events with reviewable evidence and warnings, never by assuming identical clocks. | Applied |
| Video-MAT offset | Compare food-light ON/OFF with MATLAB start estimate and `TiempoAbs` separately; retain the noise LED as visual warning. Estimate session patterns from several matches and preserve raw values plus measurements. | Applied |
| Frontend starting point | Begin with session loading, frame preview, ROIs, and calibration using implemented Core modules. Do not extend `LightEventCore` as a second backend; timeline and export wait for their backend modules. | Applied |

## Pending Architecture Decisions

### Documentation And Module Presentation

- Replace the current long dependency-arrow diagram with a more readable dependency view.
- Decide whether processing progress belongs only to `ExportView`/UI state or needs a separate backend progress model.
- State the output type of each module more explicitly where useful, including parser failures and invalid source names.
- Rename `BatchManifest` to a name that clearly communicates that it supplies missing session metadata for legacy files.
- Clarify why `VideoTransformConfig` and `ClipExporter` remain separate, and when the optional `VideoCropRotate` helper is justified.

### Legacy Metadata And Source Files

- For a legacy name, the UI must request missing session metadata and allow the user to leave unavailable fields incomplete with a warning.
- Define the expected behavior for a malformed file name: clear validation message, no silent guess, and no crash.

### Segmentation And Synchronization

- Define the exact boundary between event, ITI, and habituation when light intensity changes around a threshold. A frame must not belong to two categories.
- Make the review stage explicit: the user should be able to inspect detected light transitions, proposed segment start/end frames, event labels, `.mat` matching, and warnings before export.
- Confirm the rule for crossing versus no crossing. The `.mat` remains the preferred behavioral source when present; any visual heuristic needs documented limits.
- Implement the documented `TipoEvento = 2` path in `MatParser`, `SegmentPlanner`, tests, and the output naming code. Its output code is still undecided and must not reuse `s`, `p`, or `na`.

### MAT And Spreadsheet Validation

- Keep source precision internally. Round only when displaying values; one video frame at 30 FPS is about 0.033 seconds, so rounding to centiseconds can hide useful alignment information.
- When a `.mat` is reconstructed from Excel, compare row count and event identity against the video and report missing trailing rows. The video is the primary visual evidence; Excel is secondary convenience data.
- Define acceptance thresholds for a stable session-level offset after validation with real sessions.

### Batch Processing And Calibration

- The batch should scan a protocol folder, process only supported CMC phases, and ignore fear-conditioning material without treating it as an error.
- Compare the available video, `.mat`, and optional Excel records; report discrepancies and preserve evidence rather than silently forcing a match.
- Decide later whether a fading-but-still-on frame is needed as an optional calibration example.

## Related Files

- [Architecture](architecture.md): clean current reference.
- [Naming Convention](../reference/naming-convention.md): the three file-naming schemes.
- [MAT Format](../reference/mat-format.md): source behavioral data.
- [Sound-only event design in CajaValentia](/Users/ab/Documents/GitHub/CajaValentia/docs/idea-eventos-solo-ruido.md): upstream definition of `TipoEvento = 2`.
