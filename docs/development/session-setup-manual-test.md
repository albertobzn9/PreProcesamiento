# SessionSetup Manual Test

> Return to the [documentation index](../README.md).

`SessionSetup` is the first working frontend slice in
`src/VideoBatchProcessor.App`. It connects the UI to `NomenclatureParser` and
`SessionMetadataResolver`; it does not open, alter, rename, or export source
videos.

## Run

From the repository root:

```bash
dotnet run --project src/VideoBatchProcessor.App
```

## What To Test

1. Select **Abrir carpeta** and choose a folder containing source videos. The
   table should list supported video files only: `.mp4`, `.avi`, `.mov`, `.mkv`,
   and `.m4v`.
2. Check a recognized legacy filename such as `exp_0126_cs_d1r1.mp4`. It should
   show normalized phase `f2`, day 1, rat 1, and pending fields only for
   initials, sex, and treatment.
3. Select that row. The side panel must show only the missing fields, not a
   long form with irrelevant inputs. Enter the values and press **Aplicar datos
   pendientes**. The row should change to **Listo**.
4. Add a file with an intentionally unrecognized name. The table should show
   **Formato no reconocido** and the side panel should request all required
   metadata. Once completed, the row should change to **Completado
   manualmente**, preserving that its original filename was not recognized.
5. Confirm that the source folder has not changed: no source videos renamed,
   copied, or deleted.

## Expected Limit

This screen only verifies session identity and missing metadata. Video preview,
ROIs, light calibration, `.mat` reading, segmentation, review findings, and
export belong to later frontend/backend slices.

## Report Back

When reviewing it, note whether:

- the table has the right columns and order;
- any status or missing-field wording is unclear;
- the manual fields ask for too much or too little;
- a real filename is parsed incorrectly.

If a filename is parsed incorrectly, share the filename exactly as it appears;
that is enough to add a parser test and correct the backend safely.
