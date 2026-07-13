# SessionSetup, CameraSetup And LightMarker Manual Test

> Return to the [documentation index](../README.md).

`SessionSetup` is the first working frontend slice in
`src/VideoBatchProcessor.App`. It uses `NomenclatureParser` and
`SessionMetadataResolver` to prepare a batch, then uses `VideoReader` to show
the first readable frame of a selected source video. `CameraSetup` then sends
crop, rotation, and mirror decisions to C#, which applies them to the JPEG
preview through `VideoTransformPreviewRenderer`. `LightMarkerView` marks the
three light ROIs on that prepared preview and asks C# to validate their real
video coordinates through `FrameAnalyzer`. These screens never rename, modify,
or export source videos.

## Result

The SessionSetup, CameraSetup and LightMarker flows below were approved manually
in macOS on 12 July 2026. LightCalibration was approved manually in macOS on 13
July 2026. Repeat the relevant checks after changes to loading, naming, native
file selection, preview, camera settings, calibration, or the local Cropper.js
bundle. Windows remains pending because that platform has not been tested yet.

## Run

From the repository root:

```bash
dotnet run --project src/VideoBatchProcessor.App/VideoBatchProcessor.App.csproj
```

## What To Test

1. Use **Seleccionar carpeta** with a folder that contains subfolders of source
   videos. The app must find supported `.mp4`, `.avi`, `.mov`, `.mkv`, and
   `.m4v` files recursively.
2. Confirm that Luz-Comida (`f1`) and Condicionamiento al Miedo (`cm`/`f3`) are
   omitted and reported as skipped, while crossing sessions remain listed.
3. Confirm the naming labels: `Legacy`, `Estándar lab`, `Clip de output`, and
   `Nombre no compatible`.
4. Select a `Legacy` or `Estándar lab` row. The **PREVIEW** panel must show its
   first readable frame plus source resolution, total frames, FPS, and duration.
5. Select a `Clip de output` or `Nombre no compatible`. It must not be offered
   as a source-video preview.
6. If more than five names are not compatible, use **Quitar no compatibles**.
   Confirm they disappear only from the batch list; the source files remain on
   disk.

## CameraSetup To Test

With a compatible source session already open:

1. In **CAMERA SETUP**, choose **Girar 180°**. Confirm the preview appears
   upside down; choose it again and confirm the original orientation returns.
2. Choose **Espejo horizontal** and confirm left/right are mirrored. Choose it
   again and confirm the original orientation returns.
3. Choose **Abrir recorte**. A large modal must open over the app. Confirm that
   the complete video is visible and fixed inside the work area; only the blue
   selection frame should move or resize.
4. Drag the selection, then edit `Izquierda`, `Arriba`, `Derecha` and `Abajo`.
   Confirm that they describe the upper-left and lower-right limits in
   source-video pixels, without changing a separate width or height field.
   A valid value entered manually (for example, `50`, `1899` or `1900`) must
   remain exact after pressing Enter; it must not shift because of preview
   scaling. Every integer combination is valid when it remains inside the frame
   and preserves `izquierda < derecha` and `arriba < abajo`.
5. Choose **Video completo** inside the modal. Confirm the full image remains
   visible with a margin around it and that every side of the blue frame,
   including the right side, remains available to drag inward.
6. Choose **Aplicar recorte**. The main preview must shrink to that area and
   show its source-pixel coordinates.
7. Choose **Usar video completo**. The whole frame must return, preserving the
   chosen 180-degree rotation and mirror setting.
8. Load the session again or reset the crop. The source video must remain
   unchanged; these operations only create JPEG previews in memory.

## Light ROIs To Test

With a compatible source session already open and its camera setup finalized:

1. Click **Marcar las tres luces**.
2. Select **Comida izquierda**, click its center and drag outward to draw a
   small circle around that light. Move it and resize it from its lower-right
   handle.
3. Repeat for **Comida derecha** and **LED de ruido blanco**. Confirm each
   colored circle remains on the prepared preview and its center/radius summary is
   shown at the right.
4. Save the three regions. Confirm the side panel reports that three ROIs are
   configured and validated.
5. Use the trackpad pinch gesture and the mouse wheel over the video. Confirm
   both zoom around the pointed area; use the visible `−`, `+`, and reset
   controls as a fallback.
6. At a high zoom level, confirm the circle outlines stay thin rather than
   becoming thick. Activate **Mano** and drag to move the image; then turn it
   off and confirm dragging returns to circle editing. Press `Space` once to
   activate **Mano**, then press it again to return to marking. The text in the
   panel must explain this. Also confirm that the middle mouse button moves the
   image without creating or changing a circle.
7. Change crop, rotation, or mirror. Confirm the ROIs are cleared, because the
   prepared coordinate system changed and they must be marked again.

## LightCalibration To Test

With a compatible CP or DIS source session open, its camera setup finalized and
the three ROIs saved:

1. Click **Calibrar luces**. Confirm that a large modal opens with the same
   prepared orientation, mirror and crop used for the ROIs.
2. Drag the time bar. It must move through real frames and show both a frame
   number and an estimated timestamp. It is expected to load only the final
   position after a short pause while dragging; this keeps seeking responsive on
   long compressed videos.
3. Find a moment where all three lights are OFF and choose **Usar frame actual
   como OFF**. Confirm the card becomes green and records frame/time.
4. Find a CP or DIS moment with one food light and the white-noise LED ON. Pick
   **Comida izquierda** or **Comida derecha** according to the visible food
   light, then choose **Usar frame actual como ON**. The same frame is the
   direct ON evidence for that food light and for `NoiseLed`.
5. Confirm that the three ROI circles are visible over the current calibration
   frame. Choose **Ajustar ROIs** and confirm that the ROI editor opens on that
   exact same frame, not on the generic preview. Saving an ROI change must keep
   the same OFF/ON frames, remeasure them with the new regions, and update the
   saved thresholds without asking the user to find the frames again.
6. Confirm that the current brightness values change while moving through the
   video and that **Guardar calibración** only enables after both references are
   recorded.
7. Save the calibration. Confirm that its modal closes and the main CameraSetup
   panel shows a green `✓` for the saved calibration. Crop and saved ROIs must
   likewise show their own green `✓` indicators. The app must explicitly say the
   opposite food light uses a provisional shared reference. The workflow must
   never request a frame with all three lights ON, because both food lights do
   not turn on together.
8. Close and reopen **Calibrar luces**. Confirm the prior OFF/ON selections and
   the selected food side remain visible, and that the timeline returns to the
   last calibration frame rather than clearing the work.
9. Reopen the calibration with another real video if possible and repeat using
   the opposite food side. Report whether both sides look equally well
   classified; this determines whether future versions keep the shared food
   reference or require independent ones.

## Expected Limit

This screen verifies source selection, a raw preview, camera settings, ROI
marking and initial ON/OFF calibration against real video frames. Reusable
`CameraProfile`, continuous light timelines, `.mat` reading, segmentation,
review findings, and export belong to later slices.

## Report Back

When reviewing it, note:

- whether the shown frame is useful to confirm the camera view;
- whether resolution, FPS, or duration look implausible;
- whether a source session fails to open;
- whether a filename has the wrong label.
- whether the 180-degree rotation, mirror, or crop differs from what is visible
  in the camera;
- whether the modal crop tool is comfortable enough to use and coordinates move
  the selection as expected;
- whether resetting the crop restores the full frame.
- whether each ROI stays over its intended light after moving or resizing it;
- whether saving the ROIs clears them correctly after changing crop, rotation,
  or mirror.
- whether the calibration bar is comfortable to navigate in a long CP/DIS
  video and each selected reference matches the visible frame;
- whether both food sides behave well with the provisional shared reference.

For a filename problem, share the filename exactly as it appears. For a preview
problem, share the filename and the visible error message; no source video needs
to enter the repository.
