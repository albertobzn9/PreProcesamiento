using ClosedXML.Excel;
using VideoBatchProcessor.Core.BehavioralData;
using VideoBatchProcessor.Core.BehavioralSynchronization;
using VideoBatchProcessor.Core.LightDetection;
using VideoBatchProcessor.Core.SegmentPlanning;
using VideoBatchProcessor.Core.VideoReader;
using VideoBatchProcessor.Core.VideoTransform;

namespace VideoBatchProcessor.Core.Diagnostics;

/// <summary>
/// Exporta evidencia de detección en un libro XLSX para que el investigador
/// pueda revisar transiciones visuales y filas conductuales sin modificar sus
/// archivos fuente.
/// </summary>
public sealed class LightTimelineDiagnosticExcelExporter
{
    public void Export(LightTimelineDiagnosticReport report, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        using var workbook = new XLWorkbook();
        var synchronization = new BehavioralVideoSynchronizer().Synchronize(
            report.VisualIntervals,
            report.BehavioralEvents,
            report.ScanRange,
            report.Video.Fps,
            report.BehavioralSourcePath);
        var planning = new SegmentPlanner().Plan(new SegmentPlanningInput(
            report.Video,
            report.ScanRange,
            report.VisualIntervals,
            report.BehavioralEvents,
            synchronization));
        WriteSummary(workbook.Worksheets.Add("Resumen"), report, synchronization, planning);
        WriteCameraProfile(workbook.Worksheets.Add("Perfil de camara"), report);
        WriteVisualIntervals(workbook.Worksheets.Add("Eventos video"), report.VisualIntervals);
        WriteTransitions(workbook.Worksheets.Add("Cambios raw"), report.Timeline.Transitions);
        WriteBehavioralEvents(workbook.Worksheets.Add("MAT conductual"), report.BehavioralEvents, report.BehavioralReadError);
        WriteProvisionalComparison(workbook.Worksheets.Add("Comparacion"), synchronization.Comparison);
        WritePlannedSegments(workbook.Worksheets.Add("Segmentos planeados"), planning);

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        workbook.SaveAs(outputPath);
    }

    private static void WriteSummary(
        IXLWorksheet sheet,
        LightTimelineDiagnosticReport report,
        BehavioralVideoSynchronizationResult synchronization,
        SegmentPlanningResult planning)
    {
        var comparison = synchronization.Comparison;
        var rows = new (string Label, object? Value)[]
        {
            ("Archivo de video", report.Video.FilePath),
            ("Resolucion", $"{report.Video.Width} x {report.Video.Height}"),
            ("FPS", report.Video.Fps),
            ("Frames totales", report.Video.TotalFrames),
            ("Duracion total (s)", report.Video.Duration.TotalSeconds),
            ("Duracion total", FormatTime(report.Video.Duration.TotalSeconds)),
            ("Frame inicial analizado", report.ScanRange.StartFrame),
            ("Frame final analizado", report.ScanRange.EndFrame),
            ("Muestras analizadas", report.Timeline.SamplesAnalyzed),
            ("Cambios visuales confirmados", report.Timeline.Transitions.Count),
            ("Intervalos visuales", report.VisualIntervals.Count),
            ("Transformacion aplicada", DescribeTransform(report.Transform)),
            ("ROI comida izquierda (video fuente)", DescribeSourceRoi(report.LightConfig.FoodLeft, report.Transform, report.Video)),
            ("ROI comida derecha (video fuente)", DescribeSourceRoi(report.LightConfig.FoodRight, report.Transform, report.Video)),
            ("ROI LED de ruido (video fuente)", DescribeSourceRoi(report.LightConfig.NoiseLed, report.Transform, report.Video)),
            ("ROI comida izquierda (video preparado)", DescribeRoi(report.LightConfig.FoodLeft)),
            ("ROI comida derecha (video preparado)", DescribeRoi(report.LightConfig.FoodRight)),
            ("ROI LED de ruido (video preparado)", DescribeRoi(report.LightConfig.NoiseLed)),
            ("Fuente conductual", report.BehavioralSourcePath ?? "No disponible"),
            ("Filas conductuales leidas", report.BehavioralEvents.Count),
            ("Aviso de lectura", report.BehavioralReadError ?? "Sin errores"),
            ("Estado de sincronizacion", DescribeSynchronizationStatus(synchronization.Status)),
            ("Hallazgos", synchronization.Findings.Count == 0
                ? "Sin hallazgos"
                : string.Join(" | ", synchronization.Findings.Select(item => item.Message))),
            ("Desfase visual-MAT estimado (s)", comparison.EstimatedStartOffsetSeconds?.ToString("0.000") ?? "No estimable"),
            ("Eventos empatados", comparison.MatchedCount),
            ("Visuales sin MAT compatible", comparison.VisualWithoutBehavioralCount),
            ("MAT sin señal visual compatible", comparison.BehavioralWithoutVisualCount),
            ("Segmentos planeados", planning.Segments.Count),
            ("Eventos planeados", planning.Segments.Count(item => item.Kind == PlannedSegmentKind.Event)),
            ("ITIs planeados", planning.Segments.Count(item => item.Kind == PlannedSegmentKind.InterTrialInterval)),
            ("Avisos del planner", planning.Warnings.Count == 0
                ? "Sin avisos"
                : string.Join(" | ", planning.Warnings.Select(item => item.Message))),
            ("Nota", "La hoja Comparacion usa lado y patrón temporal. El resultado bloquea o avisa, pero la decisión experimental sigue siendo del investigador."),
        };

        sheet.Cell(1, 1).Value = "Diagnostico de timeline de luces";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;

        for (var index = 0; index < rows.Length; index++)
        {
            var row = index + 3;
            sheet.Cell(row, 1).Value = rows[index].Label;
            sheet.Cell(row, 2).Value = rows[index].Value?.ToString() ?? string.Empty;
            sheet.Cell(row, 1).Style.Font.Bold = true;
        }

        sheet.Columns().AdjustToContents();
        sheet.Column(2).Width = Math.Min(100, Math.Max(sheet.Column(2).Width, 28));
    }

    private static void WriteVisualIntervals(IXLWorksheet sheet, IReadOnlyList<LightEventInterval> intervals)
    {
        WriteHeaders(sheet, "Indice", "Senal", "Frame ON", "Tiempo ON (s)", "Tiempo ON", "Frame OFF", "Tiempo OFF (s)", "Tiempo OFF", "Duracion (s)", "Estado");
        var row = 2;
        foreach (var interval in intervals)
        {
            sheet.Cell(row, 1).Value = interval.Sequence;
            sheet.Cell(row, 2).Value = DescribeLight(interval.Light);
            sheet.Cell(row, 3).Value = interval.OnFrameIndex;
            sheet.Cell(row, 4).Value = interval.OnTimeSeconds;
            sheet.Cell(row, 5).Value = FormatTime(interval.OnTimeSeconds);
            if (interval.OffFrameIndex is not null) sheet.Cell(row, 6).Value = interval.OffFrameIndex.Value;
            if (interval.OffTimeSeconds is not null)
            {
                sheet.Cell(row, 7).Value = interval.OffTimeSeconds.Value;
                sheet.Cell(row, 8).Value = FormatTime(interval.OffTimeSeconds.Value);
            }
            if (interval.DurationSeconds is not null) sheet.Cell(row, 9).Value = interval.DurationSeconds.Value;
            sheet.Cell(row, 10).Value = interval.IsComplete ? "ON -> OFF" : "ON sin OFF dentro del rango";
            row++;
        }

        FormatTable(sheet, row - 1, 10);
    }

    private static void WriteCameraProfile(IXLWorksheet sheet, LightTimelineDiagnosticReport report)
    {
        sheet.Cell(1, 1).Value = "Perfil de camara reutilizable";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;

        var settings = new (string Key, object? Value)[]
        {
            ("Formato de perfil", "VideoBatchProcessor.CameraProfile.v1"),
            ("Sistema de coordenadas", "pixeles del video fuente"),
            ("Video de referencia", report.Video.FilePath),
            ("Ancho fuente", report.Video.Width),
            ("Alto fuente", report.Video.Height),
            ("FPS de referencia", report.Video.Fps),
            ("Crop X", report.Transform.Crop?.X ?? 0),
            ("Crop Y", report.Transform.Crop?.Y ?? 0),
            ("Crop ancho", report.Transform.Crop?.Width ?? report.Video.Width),
            ("Crop alto", report.Transform.Crop?.Height ?? report.Video.Height),
            ("Giro", report.Transform.Rotation == VideoRotation.UpsideDown ? "180" : "0"),
            ("Espejo horizontal", report.Transform.MirrorHorizontally),
        };

        for (var index = 0; index < settings.Length; index++)
        {
            var row = index + 3;
            sheet.Cell(row, 1).Value = settings[index].Key;
            sheet.Cell(row, 2).Value = settings[index].Value?.ToString() ?? string.Empty;
            sheet.Cell(row, 1).Style.Font.Bold = true;
        }

        const int roiHeaderRow = 17;
        var headers = new[] { "Luz", "X fuente", "Y fuente", "Ancho", "Alto", "Forma", "Umbral", "Nota de importacion" };
        for (var column = 0; column < headers.Length; column++)
            sheet.Cell(roiHeaderRow, column + 1).Value = headers[column];
        sheet.Row(roiHeaderRow).Style.Font.Bold = true;
        sheet.Row(roiHeaderRow).Style.Fill.BackgroundColor = XLColor.FromHtml("DCE6F1");

        var rois = new[]
        {
            report.LightConfig.FoodLeft,
            report.LightConfig.FoodRight,
            report.LightConfig.NoiseLed,
        };
        var rowIndex = roiHeaderRow + 1;
        foreach (var preparedRoi in rois)
        {
            if (!TryMapSourceRoi(preparedRoi, report.Transform, report.Video, out var sourceRoi, out var error))
            {
                sheet.Cell(rowIndex, 1).Value = DescribeLight(preparedRoi.Light);
                sheet.Cell(rowIndex, 8).Value = error ?? "No se pudo convertir la ROI al video fuente.";
                rowIndex++;
                continue;
            }

            sheet.Cell(rowIndex, 1).Value = DescribeLight(sourceRoi!.Light);
            sheet.Cell(rowIndex, 2).Value = sourceRoi.X;
            sheet.Cell(rowIndex, 3).Value = sourceRoi.Y;
            sheet.Cell(rowIndex, 4).Value = sourceRoi.Width;
            sheet.Cell(rowIndex, 5).Value = sourceRoi.Height;
            sheet.Cell(rowIndex, 6).Value = sourceRoi.Shape.ToString();
            sheet.Cell(rowIndex, 7).Value = sourceRoi.Threshold;
            sheet.Cell(rowIndex, 8).Value = "Importar solo si cámara, crop y orientación equivalen al video de referencia.";
            rowIndex++;
        }

        sheet.Range(roiHeaderRow, 1, rowIndex - 1, 8).CreateTable();
        sheet.Columns().AdjustToContents();
        sheet.Column(8).Width = Math.Min(80, Math.Max(sheet.Column(8).Width, 40));
    }

    private static void WriteTransitions(IXLWorksheet sheet, IReadOnlyList<LightTransition> transitions)
    {
        WriteHeaders(sheet, "Senal", "Cambio", "Frame candidato", "Tiempo candidato (s)", "Tiempo candidato", "Frame confirmado");
        var row = 2;
        foreach (var transition in transitions)
        {
            sheet.Cell(row, 1).Value = DescribeLight(transition.Light);
            sheet.Cell(row, 2).Value = transition.IsOn ? "OFF -> ON" : "ON -> OFF";
            sheet.Cell(row, 3).Value = transition.FrameIndex;
            sheet.Cell(row, 4).Value = transition.TimeSeconds;
            sheet.Cell(row, 5).Value = FormatTime(transition.TimeSeconds);
            sheet.Cell(row, 6).Value = transition.ConfirmedAtFrameIndex;
            row++;
        }

        FormatTable(sheet, row - 1, 6);
    }

    private static void WritePlannedSegments(IXLWorksheet sheet, SegmentPlanningResult planning)
    {
        WriteHeaders(sheet,
            "Secuencia",
            "Tipo",
            "Frame inicio",
            "Tiempo inicio (s)",
            "Tiempo inicio",
            "Frame fin",
            "Tiempo fin (s)",
            "Tiempo fin",
            "Duración total (s)",
            "Duración total",
            "Tipo de ensayo",
            "Cruce por lado",
            "Comparación de lado",
            "Evento conductual",
            "Lado actual",
            "Lado previo",
            "Inicio LED",
            "Inicio comida",
            "Fin comida",
            "Inicio MAT mapeado (s)",
            "Palanqueo MAT mapeado (s)",
            "Residuo inicio (s)",
            "Cola luz tras palanqueo (s)");

        var row = 2;
        foreach (var segment in planning.Segments)
        {
            sheet.Cell(row, 1).Value = segment.Sequence;
            sheet.Cell(row, 2).Value = DescribeSegmentKind(segment.Kind);
            sheet.Cell(row, 3).Value = segment.StartFrameIndex;
            sheet.Cell(row, 4).Value = segment.StartTimeSeconds;
            sheet.Cell(row, 5).Value = FormatTime(segment.StartTimeSeconds);
            sheet.Cell(row, 6).Value = segment.EndFrameIndex;
            sheet.Cell(row, 7).Value = segment.EndTimeSeconds;
            sheet.Cell(row, 8).Value = FormatTime(segment.EndTimeSeconds);
            sheet.Cell(row, 9).Value = segment.DurationSeconds;
            sheet.Cell(row, 10).Value = FormatTime(segment.DurationSeconds);
            sheet.Cell(row, 11).Value = segment.TrialType is null ? string.Empty : DescribeTrialType(segment.TrialType.Value);
            sheet.Cell(row, 12).Value = DescribePlannedResult(segment.Result);
            sheet.Cell(row, 13).Value = DescribeSideComparison(segment);
            if (segment.BehavioralEventNumber is not null) sheet.Cell(row, 14).Value = segment.BehavioralEventNumber.Value;
            if (segment.CurrentSide is not null) sheet.Cell(row, 15).Value = DescribeSide(segment.CurrentSide.Value);
            if (segment.PreviousKnownSide is not null) sheet.Cell(row, 16).Value = DescribeSide(segment.PreviousKnownSide.Value);
            if (segment.WarningStartFrameIndex is not null) sheet.Cell(row, 17).Value = segment.WarningStartFrameIndex.Value;
            if (segment.FoodLightStartFrameIndex is not null) sheet.Cell(row, 18).Value = segment.FoodLightStartFrameIndex.Value;
            if (segment.FoodLightEndFrameIndex is not null) sheet.Cell(row, 19).Value = segment.FoodLightEndFrameIndex.Value;
            if (segment.MappedBehavioralStartSeconds is not null) sheet.Cell(row, 20).Value = segment.MappedBehavioralStartSeconds.Value;
            if (segment.MappedBehavioralPressSeconds is not null) sheet.Cell(row, 21).Value = segment.MappedBehavioralPressSeconds.Value;
            if (segment.VisualStartResidualSeconds is not null) sheet.Cell(row, 22).Value = segment.VisualStartResidualSeconds.Value;
            if (segment.PostPressLightTailSeconds is not null) sheet.Cell(row, 23).Value = segment.PostPressLightTailSeconds.Value;
            row++;
        }

        if (planning.Segments.Count == 0)
            sheet.Cell(2, 1).Value = "No se planearon segmentos; revisar sincronización y avisos.";

        FormatTable(sheet, Math.Max(1, row - 1), 23);
    }

    private static void WriteBehavioralEvents(
        IXLWorksheet sheet,
        IReadOnlyList<BehavioralEvent> events,
        string? behavioralReadError)
    {
        WriteHeaders(sheet, "Fila", "Ensayo/evento", "Lado", "Tipo", "Estim", "Latencia palanqueo (s)", "Tiempo absoluto (s)", "Inicio MATLAB estimado (s)", "Inicio MATLAB estimado", "Palancas izq", "Palancas der", "Desplaz (s)");
        var row = 2;
        foreach (var item in events)
        {
            var estimatedStart = item.AbsoluteTimeSeconds - item.LeverLatencySeconds;
            sheet.Cell(row, 1).Value = row - 1;
            sheet.Cell(row, 2).Value = item.EventNumber;
            sheet.Cell(row, 3).Value = DescribeSide(item.Side);
            sheet.Cell(row, 4).Value = DescribeEventType(item.EventType);
            sheet.Cell(row, 5).Value = item.Stimulus;
            sheet.Cell(row, 6).Value = item.LeverLatencySeconds;
            sheet.Cell(row, 7).Value = item.AbsoluteTimeSeconds;
            sheet.Cell(row, 8).Value = estimatedStart;
            sheet.Cell(row, 9).Value = FormatTime(estimatedStart);
            sheet.Cell(row, 10).Value = item.LeftLeverPresses;
            sheet.Cell(row, 11).Value = item.RightLeverPresses;
            sheet.Cell(row, 12).Value = item.CrossingLatencySeconds;
            row++;
        }

        if (events.Count == 0)
            sheet.Cell(2, 1).Value = behavioralReadError ?? "No se encontro una fuente conductual legible.";

        FormatTable(sheet, Math.Max(1, row - 1), 12);
    }

    private static void WriteProvisionalComparison(
        IXLWorksheet sheet,
        LightTimelineDiagnosticComparison comparison)
    {
        WriteHeaders(sheet, "Estado", "Indice visual", "Senal visual", "Inicio video (s)", "Inicio video", "Fin video (s)", "MAT evento", "MAT lado", "Inicio MATLAB estimado (s)", "Palanqueo MATLAB (s)", "Desfase inicio bruto (s)", "Residuo tras desfase (s)", "Cola tras palanqueo (s)", "Nota");
        var row = 2;
        foreach (var item in comparison.Rows)
        {
            var interval = item.VisualInterval;
            var behavioral = item.BehavioralEvent;
            sheet.Cell(row, 1).Value = DescribeComparisonStatus(item.Status);
            if (interval is not null)
            {
                sheet.Cell(row, 2).Value = interval.Sequence;
                sheet.Cell(row, 3).Value = DescribeLight(interval.Light);
                sheet.Cell(row, 4).Value = interval.OnTimeSeconds;
                sheet.Cell(row, 5).Value = FormatTime(interval.OnTimeSeconds);
                if (interval.OffTimeSeconds is not null) sheet.Cell(row, 6).Value = interval.OffTimeSeconds.Value;
            }
            if (behavioral is not null)
            {
                var estimatedStart = LightTimelineDiagnosticComparer.EstimatedMatlabStart(behavioral);
                sheet.Cell(row, 7).Value = behavioral.EventNumber;
                sheet.Cell(row, 8).Value = DescribeSide(behavioral.Side);
                sheet.Cell(row, 9).Value = estimatedStart;
                sheet.Cell(row, 10).Value = behavioral.AbsoluteTimeSeconds;
                if (interval is not null)
                    sheet.Cell(row, 11).Value = interval.OnTimeSeconds - estimatedStart;
                if (item.StartResidualSeconds is not null)
                    sheet.Cell(row, 12).Value = item.StartResidualSeconds.Value;
                if (interval?.OffTimeSeconds is not null && comparison.EstimatedStartOffsetSeconds is not null)
                    sheet.Cell(row, 13).Value = interval.OffTimeSeconds.Value -
                                                 (behavioral.AbsoluteTimeSeconds + comparison.EstimatedStartOffsetSeconds.Value);
            }
            sheet.Cell(row, 14).Value = item.Note;
            row++;
        }

        FormatTable(sheet, Math.Max(1, row - 1), 14);
    }

    private static void WriteHeaders(IXLWorksheet sheet, params string[] headers)
    {
        for (var column = 0; column < headers.Length; column++)
            sheet.Cell(1, column + 1).Value = headers[column];

        sheet.Row(1).Style.Font.Bold = true;
        sheet.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("DCE6F1");
        sheet.SheetView.FreezeRows(1);
    }

    private static void FormatTable(IXLWorksheet sheet, int lastRow, int lastColumn)
    {
        if (lastRow >= 2)
            sheet.Range(1, 1, lastRow, lastColumn).CreateTable();
        sheet.Columns().AdjustToContents();
    }

    private static string DescribeLight(LightId light) => light switch
    {
        LightId.FoodLeft => "Comida izquierda",
        LightId.FoodRight => "Comida derecha",
        LightId.NoiseLed => "LED de ruido",
        _ => light.ToString(),
    };

    private static string DescribeRoi(LightRoi roi)
    {
        var geometry = $"centro=({roi.X + roi.Width / 2d:0.##}, {roi.Y + roi.Height / 2d:0.##}); radio={roi.Width / 2d:0.##} px";
        return roi.Threshold >= double.MaxValue / 2d
            ? $"{geometry}; desactivado para esta sesión (CS)"
            : $"{geometry}; umbral={roi.Threshold:0.###}";
    }

    private static string DescribeSourceRoi(
        LightRoi preparedRoi,
        VideoTransformConfig transform,
        VideoMetadata video)
    {
        if (!TryMapSourceRoi(preparedRoi, transform, video, out var source, out var error))
        {
            return $"No se pudo convertir: {error}";
        }

        return DescribeRoi(source!);
    }

    private static bool TryMapSourceRoi(
        LightRoi preparedRoi,
        VideoTransformConfig transform,
        VideoMetadata video,
        out LightRoi? source,
        out string? error)
    {
        source = null;
        if (!VideoCoordinateMapper.TryMapPreparedToSource(
                new VideoCropRect(preparedRoi.X, preparedRoi.Y, preparedRoi.Width, preparedRoi.Height),
                transform,
                video.Width,
                video.Height,
                out var sourceRect,
                out error))
        {
            return false;
        }

        source = new LightRoi(
            preparedRoi.Light,
            sourceRect!.X,
            sourceRect.Y,
            sourceRect.Width,
            sourceRect.Height,
            preparedRoi.Threshold,
            preparedRoi.Shape);
        return true;
    }

    private static string DescribeTransform(VideoTransformConfig transform)
    {
        var crop = transform.Crop is null
            ? "sin recorte"
            : $"crop fuente=({transform.Crop.X}, {transform.Crop.Y}, {transform.Crop.Width}, {transform.Crop.Height})";
        var rotation = transform.Rotation == VideoRotation.UpsideDown ? "giro=180 grados" : "giro=ninguno";
        var mirror = transform.MirrorHorizontally ? "espejo=horizontal" : "espejo=ninguno";
        return $"{crop}; {rotation}; {mirror}";
    }

    private static string DescribeSide(int side) => side switch
    {
        1 => "Izquierda (1)",
        0 => "Derecha (0)",
        -2 => "Timeout (-2)",
        _ => side.ToString(),
    };

    private static string DescribeEventType(BehavioralEventType eventType) => eventType switch
    {
        BehavioralEventType.SafeFood => "Seguro con comida",
        BehavioralEventType.ConflictWithFood => "Riesgo con comida",
        BehavioralEventType.SoundOnly => "Solo sonido",
        _ => eventType.ToString(),
    };

    private static string DescribeComparisonStatus(DiagnosticComparisonStatus status) => status switch
    {
        DiagnosticComparisonStatus.Matched => "Empatado",
        DiagnosticComparisonStatus.VisualWithoutBehavioral => "Visual sin MAT",
        DiagnosticComparisonStatus.BehavioralWithoutVisual => "MAT sin visual",
        _ => status.ToString(),
    };

    private static string DescribeSynchronizationStatus(BehavioralVideoSynchronizationStatus status) => status switch
    {
        BehavioralVideoSynchronizationStatus.Ready => "Listo para planear segmentos",
        BehavioralVideoSynchronizationStatus.Warning => "Revisar antes de planear segmentos",
        BehavioralVideoSynchronizationStatus.Blocked => "No planear segmentos hasta revisar",
        _ => status.ToString(),
    };

    private static string DescribeSegmentKind(PlannedSegmentKind kind) => kind switch
    {
        PlannedSegmentKind.InitialHabituation => "Habituacion inicial",
        PlannedSegmentKind.Event => "Evento",
        PlannedSegmentKind.InterTrialInterval => "ITI",
        PlannedSegmentKind.FinalHabituation => "Habituacion final",
        _ => kind.ToString(),
    };

    private static string DescribeTrialType(PlannedTrialType type) => type switch
    {
        PlannedTrialType.SafeFood => "Seguro con comida",
        PlannedTrialType.ConflictWithFood => "Riesgo con comida",
        PlannedTrialType.SoundOnly => "Solo sonido",
        _ => type.ToString(),
    };

    private static string DescribePlannedResult(PlannedBehavioralResult result) => result switch
    {
        PlannedBehavioralResult.Crossing => "Cruce",
        PlannedBehavioralResult.NoCrossing => "No cruce",
        PlannedBehavioralResult.Timeout => "Timeout",
        PlannedBehavioralResult.NotApplicable => "No aplica (primer evento)",
        _ => result.ToString(),
    };

    private static string DescribeSideComparison(PlannedVideoSegment segment)
    {
        if (segment.Kind != PlannedSegmentKind.Event || segment.CurrentSide is null)
            return "No aplica";
        if (segment.CurrentSide == -2)
            return "Timeout: no aplica comparación";
        if (segment.PreviousKnownSide is null)
            return "Sin lado previo: primer evento";

        var path = $"{DescribeSide(segment.PreviousKnownSide.Value)} -> {DescribeSide(segment.CurrentSide.Value)}";
        return segment.PreviousKnownSide == segment.CurrentSide
            ? $"Mismo lado: {path}"
            : $"Cambio de lado: {path}";
    }

    private static string FormatTime(double seconds)
    {
        var minutes = (int)Math.Floor(Math.Max(0, seconds) / 60d);
        var remaining = Math.Max(0, seconds) - minutes * 60d;
        return $"{minutes}:{remaining:00.000}";
    }
}
