# SessionSetup And CameraSetup Manual Test

> Return to the [documentation index](../README.md).

`SessionSetup` is the first working frontend slice in
`src/VideoBatchProcessor.App`. It uses `NomenclatureParser` and
`SessionMetadataResolver` to prepare a batch, then uses `VideoReader` to show
the first readable frame of a selected source video. `CameraSetup` then sends
crop, rotation, and mirror decisions to C#, which applies them to the JPEG
preview through `VideoTransformPreviewRenderer`. Neither module renames,
modifies, or exports source videos.

## Result

The SessionSetup and CameraSetup flows below were approved manually in macOS on
12 July 2026. Repeat the relevant checks after changes to loading, naming,
native file selection, preview, camera settings, or the local Cropper.js
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

## Expected Limit

This screen verifies source selection, a raw preview, and the first visual
camera settings. Reusable `CameraProfile`, ROIs, light calibration, `.mat`
reading, segmentation, review findings, and export belong to later slices.

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

For a filename problem, share the filename exactly as it appears. For a preview
problem, share the filename and the visible error message; no source video needs
to enter the repository.
