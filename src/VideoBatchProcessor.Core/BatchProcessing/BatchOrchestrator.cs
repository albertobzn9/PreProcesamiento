using VideoBatchProcessor.Core.BehavioralData;
using VideoBatchProcessor.Core.BehavioralSynchronization;
using VideoBatchProcessor.Core.ClipExport;
using VideoBatchProcessor.Core.Diagnostics;
using VideoBatchProcessor.Core.LightDetection;
using VideoBatchProcessor.Core.Nomenclature;
using VideoBatchProcessor.Core.SegmentPlanning;
using VideoBatchProcessor.Core.SessionResolver;
using VideoBatchProcessor.Core.SessionFiles;
using VideoBatchProcessor.Core.VideoTransform;
using System.Globalization;
using System.Text;

namespace VideoBatchProcessor.Core.BatchProcessing;

/// <summary>
/// Ejecuta el flujo completo para un lote inicial de Cruces Seguros: descubre
/// videos fuente, empareja su MAT del mismo stem, valida luces contra conducta
/// y exporta todos los segmentos planeados. No modifica videos ni archivos MAT.
/// </summary>
public sealed class BatchOrchestrator
{
    private static readonly HashSet<string> s_videoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".m4v",
    };

    private readonly NomenclatureParser _nomenclatureParser;
    private readonly SessionMetadataResolver _metadataResolver;
    private readonly LightTimelineScanner _timelineScanner;
    private readonly BehavioralVideoSynchronizer _synchronizer;
    private readonly SegmentPlanner _segmentPlanner;
    private readonly ClipExporter _clipExporter;
    private readonly LightTimelineDiagnosticExcelExporter _diagnosticExporter;

    public BatchOrchestrator(
        NomenclatureParser? nomenclatureParser = null,
        SessionMetadataResolver? metadataResolver = null,
        LightTimelineScanner? timelineScanner = null,
        BehavioralVideoSynchronizer? synchronizer = null,
        SegmentPlanner? segmentPlanner = null,
        ClipExporter? clipExporter = null,
        LightTimelineDiagnosticExcelExporter? diagnosticExporter = null)
    {
        _nomenclatureParser = nomenclatureParser ?? new NomenclatureParser();
        _metadataResolver = metadataResolver ?? new SessionMetadataResolver();
        _timelineScanner = timelineScanner ?? new LightTimelineScanner();
        _synchronizer = synchronizer ?? new BehavioralVideoSynchronizer();
        _segmentPlanner = segmentPlanner ?? new SegmentPlanner();
        _clipExporter = clipExporter ?? new ClipExporter();
        _diagnosticExporter = diagnosticExporter ?? new LightTimelineDiagnosticExcelExporter();
    }

    /// <summary>
    /// Encuentra recursivamente solo sesiones fuente de Cruces Seguros. Omite
    /// clips de salida, nombres no reconocidos y las demás fases sin tratarlas
    /// como errores: pertenecen a etapas posteriores del producto.
    /// </summary>
    public IReadOnlyList<BatchCandidate> DiscoverCandidates(string inputDirectory)
    {
        if (string.IsNullOrWhiteSpace(inputDirectory) || !Directory.Exists(inputDirectory))
            return [];

        return SessionVideoSelector.SelectOnePerSession(
                Directory.EnumerateFiles(inputDirectory, "*", SearchOption.AllDirectories)
                    .Where(path => s_videoExtensions.Contains(Path.GetExtension(path))))
            .Select(source => CreateCandidate(source.VideoPath, source.AlternateVideoPaths))
            .ToArray();
    }

    public async Task<BatchReport> RunAsync(
        BatchProcessingRequest request,
        IProgress<BatchProcessingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        var candidates = DiscoverCandidates(request.InputDirectory);
        var reports = new List<BatchSessionReport>();
        for (var index = 0; index < candidates.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = candidates[index];
            ReportProgress("Preparando sesión", index, 0);

            var report = candidate.IsCsSource
                ? await ProcessCsSessionAsync(
                    candidate,
                    request,
                    (message, sessionPercent) => ReportProgress(message, index, sessionPercent),
                    cancellationToken)
                : BatchSessionReport.Skipped(candidate.VideoPath, candidate.SkipReason!);
            reports.Add(report);
            ReportProgress("Sesión terminada", index + 1, 0);
        }

        progress?.Report(new BatchProcessingProgress(candidates.Count, candidates.Count, null, "Lote terminado", 100));
        return new BatchReport(request.InputDirectory, request.OutputDirectory, reports);

        void ReportProgress(string message, int completedSessions, double sessionPercent)
        {
            var percent = candidates.Count == 0
                ? 100
                : (int)Math.Round((completedSessions + Math.Clamp(sessionPercent, 0, 100) / 100d) * 100d / candidates.Count);
            progress?.Report(new BatchProcessingProgress(
                completedSessions,
                candidates.Count,
                completedSessions < candidates.Count ? candidates[completedSessions].VideoPath : null,
                message,
                percent));
        }
    }

    private BatchCandidate CreateCandidate(string videoPath, IReadOnlyList<string> alternateVideoPaths)
    {
        if (!_nomenclatureParser.TryParse(videoPath, out var parsed))
            return new BatchCandidate(videoPath, null, false, "El nombre no corresponde a una sesión fuente reconocida.", alternateVideoPaths);
        if (!parsed.IsSourceSession)
            return new BatchCandidate(videoPath, parsed, false, "Es un clip de salida; nunca se vuelve a procesar como entrada.", alternateVideoPaths);
        if (parsed.FaseEstandar != "f2")
            return new BatchCandidate(videoPath, parsed, false, "El lote inicial solo procesa Cruces Seguros (f2/cs).", alternateVideoPaths);

        return new BatchCandidate(videoPath, parsed, true, null, alternateVideoPaths);
    }

    private async Task<BatchSessionReport> ProcessCsSessionAsync(
        BatchCandidate candidate,
        BatchProcessingRequest request,
        Action<string, double> reportProgress,
        CancellationToken cancellationToken)
    {
        try
        {
            reportProgress("Leyendo metadata y MAT", 5);
            var metadata = _metadataResolver.Resolve(candidate.ParsedName!, request.Manifest, candidate.VideoPath);
            if (!metadata.IsComplete)
            {
                return BatchSessionReport.Blocked(
                    candidate.VideoPath,
                    $"Faltan datos para nombrar la salida: {string.Join(", ", metadata.MissingFields)}.");
            }

            if (!TryResolveMatPath(candidate.VideoPath, request.Manifest, out var matPath, out var sourceError))
                return BatchSessionReport.Blocked(candidate.VideoPath, sourceError!);

            var behavioralSource = new BehavioralSourceResolution
            {
                SourcePath = matPath,
                SourceKind = BehavioralSourceKind.LegacyMat,
                Origin = HasBehavioralOverride(candidate.VideoPath, request.Manifest)
                    ? BehavioralSourceOrigin.ExplicitOverride
                    : BehavioralSourceOrigin.MatSibling,
            };
            var behavior = new LegacyMatBehavioralSessionReader(new MatV5MatrixReader()).Read(behavioralSource);
            if (behavior.Events.Count == 0)
                return BatchSessionReport.Blocked(candidate.VideoPath, "El MAT no contiene eventos utilizables.");

            reportProgress("Analizando luces", 10);
            var scanProgress = new Progress<LightTimelineScanProgress>(update =>
                reportProgress($"Analizando luces ({update.Percent}%)", 10 + update.Percent * 0.70));
            var scan = _timelineScanner.Scan(
                candidate.VideoPath,
                request.Transform,
                request.LightConfig,
                progress: scanProgress,
                cancellationToken: cancellationToken);
            var intervals = LightEventIntervalBuilder.Build(scan.Timeline);
            var fullRange = new LightTimelineScanRange(0, checked((int)scan.Metadata.TotalFrames - 1));
            reportProgress("Sincronizando video y MAT", 82);
            var synchronization = _synchronizer.Synchronize(
                intervals,
                behavior.Events,
                fullRange,
                scan.Metadata.Fps,
                matPath);
            if (synchronization.Status == BehavioralVideoSynchronizationStatus.Blocked)
            {
                return BatchSessionReport.Blocked(
                    candidate.VideoPath,
                    DescribeSynchronizationBlock(synchronization),
                    matPath,
                    synchronization);
            }

            reportProgress("Planeando recortes", 88);
            var plan = _segmentPlanner.Plan(new SegmentPlanningInput(
                scan.Metadata,
                fullRange,
                intervals,
                behavior.Events,
                synchronization));
            if (plan.Segments.Count == 0)
            {
                return BatchSessionReport.Blocked(
                    candidate.VideoPath,
                    "No se pudo planear ningún segmento de esta sesión.",
                    matPath,
                    synchronization,
                    plan.Warnings);
            }

            var sessionOutputDirectory = Path.Combine(
                request.OutputDirectory,
                Path.GetFileNameWithoutExtension(candidate.VideoPath));
            if (Directory.Exists(sessionOutputDirectory))
            {
                return BatchSessionReport.Blocked(
                    candidate.VideoPath,
                    "Ya existe una carpeta de salida para esta sesión. Se conserva para no mezclar ni sobrescribir clips.",
                    matPath,
                    synchronization,
                    plan.Warnings);
            }

            Directory.CreateDirectory(sessionOutputDirectory);
            reportProgress("Guardando diagnóstico", 91);
            var diagnosticPath = Path.Combine(sessionOutputDirectory, "diagnostico_luces.xlsx");
            _diagnosticExporter.Export(new LightTimelineDiagnosticReport(
                scan.Metadata,
                fullRange,
                request.Transform,
                scan.Timeline,
                intervals,
                request.LightConfig,
                behavior.Events,
                matPath,
                null), diagnosticPath);

            var exports = new List<BatchClipReport>();
            var segmentsToExport = SelectSegmentsForExport(plan.Segments, request.ExportMode);
            for (var exportIndex = 0; exportIndex < segmentsToExport.Count; exportIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var segment = segmentsToExport[exportIndex];
                reportProgress($"Exportando clip {exportIndex + 1} de {segmentsToExport.Count}", 92 + exportIndex * 7d / Math.Max(1, segmentsToExport.Count));
                var outputPath = Path.Combine(
                    sessionOutputDirectory,
                    BuildOutputName(metadata, segment));
                var exported = await _clipExporter.ExportAsync(new ClipExportRequest(
                    candidate.VideoPath,
                    scan.Metadata,
                    segment,
                    request.Transform,
                    outputPath,
                    request.ClipOptions), cancellationToken);
                exports.Add(new BatchClipReport(segment, exported));

                if (!exported.Succeeded)
                {
                    return BatchSessionReport.Failed(
                        candidate.VideoPath,
                        exported.ErrorMessage ?? "No se pudo exportar un clip.",
                        matPath,
                        synchronization,
                        plan.Warnings,
                        exports,
                        diagnosticPath);
                }
            }

            reportProgress("Guardando índice de clips", 99);
            BatchClipManifestWriter.Write(
                Path.Combine(sessionOutputDirectory, "clips_exportados.csv"),
                exports);

            var hasWarnings = synchronization.Status == BehavioralVideoSynchronizationStatus.Warning || plan.Warnings.Count > 0;
            return new BatchSessionReport(
                candidate.VideoPath,
                hasWarnings ? BatchSessionStatus.ExportedWithWarnings : BatchSessionStatus.Exported,
                null,
                matPath,
                synchronization,
                plan.Warnings,
                exports,
                diagnosticPath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return BatchSessionReport.Failed(candidate.VideoPath, exception.Message);
        }
    }

    private static bool TryResolveMatPath(
        string videoPath,
        BatchManifest manifest,
        out string? matPath,
        out string? error)
    {
        var stem = Path.GetFileNameWithoutExtension(videoPath);
        manifest.Overrides.TryGetValue(stem, out var fileOverride);
        matPath = fileOverride?.BehavioralSourcePath ?? fileOverride?.MatPath ?? Path.ChangeExtension(videoPath, ".mat");
        error = null;

        if (!Path.IsPathRooted(matPath))
            matPath = Path.GetFullPath(matPath, Path.GetDirectoryName(videoPath)!);

        if (!string.Equals(Path.GetExtension(matPath), ".mat", StringComparison.OrdinalIgnoreCase))
        {
            error = "El lote CS actual requiere un archivo .mat; el CSV quedará para una etapa posterior.";
            return false;
        }
        if (!File.Exists(matPath))
        {
            error = "No se encontró el MAT de esta sesión. Debe llamarse igual que el video o declararse como override explícito.";
            return false;
        }

        matPath = Path.GetFullPath(matPath);
        return true;
    }

    private static bool HasBehavioralOverride(string videoPath, BatchManifest manifest)
    {
        var stem = Path.GetFileNameWithoutExtension(videoPath);
        return manifest.Overrides.TryGetValue(stem, out var fileOverride) &&
               !string.IsNullOrWhiteSpace(fileOverride.BehavioralSourcePath ?? fileOverride.MatPath);
    }

    private static string BuildOutputName(SessionMetadata metadata, PlannedVideoSegment segment)
    {
        var segmentCode = segment.Kind switch
        {
            PlannedSegmentKind.InitialHabituation => "habini",
            PlannedSegmentKind.FinalHabituation => "habfin",
            PlannedSegmentKind.InterTrialInterval => $"iti{segment.BehavioralEventNumber ?? segment.Sequence}",
            PlannedSegmentKind.Event => $"e{segment.BehavioralEventNumber}",
            _ => throw new ArgumentOutOfRangeException(nameof(segment)),
        };
        var trialCode = segment.TrialType switch
        {
            PlannedTrialType.SafeFood => "s",
            PlannedTrialType.ConflictWithFood => "p",
            null => "na",
            _ => throw new InvalidOperationException("El lote CS no debe exportar eventos de solo ruido."),
        };
        var resultCode = segment.Result switch
        {
            PlannedBehavioralResult.Crossing => "cr",
            PlannedBehavioralResult.NoCrossing => "nc",
            PlannedBehavioralResult.Timeout => "to",
            PlannedBehavioralResult.NotApplicable => "na",
            _ => throw new ArgumentOutOfRangeException(nameof(segment)),
        };

        return NomenclatureParser.BuildVbpOutputName(
            metadata.Iniciales,
            metadata.Fecha,
            metadata.Fase,
            metadata.Dia,
            metadata.Rata,
            metadata.Sexo,
            segmentCode,
            trialCode,
            resultCode,
            metadata.Tratamiento);
    }

    private static IReadOnlyList<PlannedVideoSegment> SelectSegmentsForExport(
        IReadOnlyList<PlannedVideoSegment> segments,
        BatchExportMode exportMode)
    {
        if (exportMode == BatchExportMode.AllSegments)
            return segments;

        // La primera corrida real solo necesita evidencia de los límites clave:
        // inicio, primeros eventos/ITI y final. El orden final sigue el video.
        var selected = new HashSet<PlannedVideoSegment>();
        AddFirst(PlannedSegmentKind.InitialHabituation);
        foreach (var item in segments.Where(item => item.Kind == PlannedSegmentKind.Event).Take(2))
            selected.Add(item);
        AddFirst(PlannedSegmentKind.InterTrialInterval);
        AddFirst(PlannedSegmentKind.FinalHabituation);
        return selected.OrderBy(item => item.Sequence).ToArray();

        void AddFirst(PlannedSegmentKind kind)
        {
            var segment = segments.FirstOrDefault(item => item.Kind == kind);
            if (segment is not null)
                selected.Add(segment);
        }
    }

    private static string DescribeSynchronizationBlock(BehavioralVideoSynchronizationResult synchronization) =>
        synchronization.Findings.Count == 0
            ? "La sincronización entre video y MAT quedó bloqueada."
            : string.Join(" ", synchronization.Findings.Select(item => item.Message));

    private static void ValidateRequest(BatchProcessingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.InputDirectory) || !Directory.Exists(request.InputDirectory))
            throw new DirectoryNotFoundException("La carpeta de entrada no existe.");
        if (string.IsNullOrWhiteSpace(request.OutputDirectory) || !Path.IsPathRooted(request.OutputDirectory))
            throw new ArgumentException("La carpeta de salida debe ser una ruta absoluta.", nameof(request));
        ArgumentNullException.ThrowIfNull(request.Transform);
        ArgumentNullException.ThrowIfNull(request.LightConfig);
        ArgumentNullException.ThrowIfNull(request.Manifest);
        ArgumentNullException.ThrowIfNull(request.ClipOptions);
    }
}

public sealed record BatchProcessingRequest(
    string InputDirectory,
    string OutputDirectory,
    VideoTransformConfig Transform,
    LightDetectionConfig LightConfig,
    BatchManifest Manifest,
    ClipExportOptions ClipOptions,
    BatchExportMode ExportMode = BatchExportMode.ValidationSample);

public enum BatchExportMode
{
    ValidationSample,
    AllSegments,
}

public sealed record BatchCandidate(
    string VideoPath,
    ParsedFileName? ParsedName,
    bool IsCsSource,
    string? SkipReason,
    IReadOnlyList<string>? AlternateVideoPaths = null);

public enum BatchSessionStatus
{
    Exported,
    ExportedWithWarnings,
    Blocked,
    Failed,
    Skipped,
}

public sealed record BatchClipReport(
    PlannedVideoSegment Segment,
    ClipExportResult Export);

public sealed record BatchSessionReport(
    string VideoPath,
    BatchSessionStatus Status,
    string? Message,
    string? MatPath,
    BehavioralVideoSynchronizationResult? Synchronization,
    IReadOnlyList<SegmentPlanningWarning> PlanningWarnings,
    IReadOnlyList<BatchClipReport> Clips,
    string? DiagnosticPath)
{
    public static BatchSessionReport Skipped(string videoPath, string message) =>
        new(videoPath, BatchSessionStatus.Skipped, message, null, null, [], [], null);

    public static BatchSessionReport Blocked(
        string videoPath,
        string message,
        string? matPath = null,
        BehavioralVideoSynchronizationResult? synchronization = null,
        IReadOnlyList<SegmentPlanningWarning>? warnings = null) =>
        new(videoPath, BatchSessionStatus.Blocked, message, matPath, synchronization, warnings ?? [], [], null);

    public static BatchSessionReport Failed(
        string videoPath,
        string message,
        string? matPath = null,
        BehavioralVideoSynchronizationResult? synchronization = null,
        IReadOnlyList<SegmentPlanningWarning>? warnings = null,
        IReadOnlyList<BatchClipReport>? clips = null,
        string? diagnosticPath = null) =>
        new(videoPath, BatchSessionStatus.Failed, message, matPath, synchronization, warnings ?? [], clips ?? [], diagnosticPath);
}

public sealed record BatchReport(
    string InputDirectory,
    string OutputDirectory,
    IReadOnlyList<BatchSessionReport> Sessions)
{
    public int ExportedSessionCount => Sessions.Count(item => item.Status is BatchSessionStatus.Exported or BatchSessionStatus.ExportedWithWarnings);
    public int BlockedSessionCount => Sessions.Count(item => item.Status == BatchSessionStatus.Blocked);
    public int FailedSessionCount => Sessions.Count(item => item.Status == BatchSessionStatus.Failed);
    public int ExportedClipCount => Sessions.Sum(item => item.Clips.Count(clip => clip.Export.Succeeded));
}

public sealed record BatchProcessingProgress(
    int CompletedSessions,
    int TotalSessions,
    string? CurrentVideoPath,
    string Message,
    int? PercentOverride = null)
{
    public int Percent => PercentOverride ?? (TotalSessions == 0 ? 100 : (int)Math.Round(CompletedSessions * 100d / TotalSessions));
}

/// <summary>
/// Deja una tabla breve junto a los clips para que el investigador pueda ubicar
/// cada fragmento en el video original sin alterar la nomenclatura oficial.
/// </summary>
public static class BatchClipManifestWriter
{
    public static void Write(string outputPath, IReadOnlyList<BatchClipReport> clips)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(clips);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? throw new ArgumentException("La ruta de manifest no tiene carpeta.", nameof(outputPath)));
        using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        writer.WriteLine("archivo_clip,tipo,ensayo,resultado,frame_inicio_original,frame_final_original,inicio_original,final_original,duracion_s");

        foreach (var clip in clips.Where(item => item.Export.Succeeded))
        {
            var segment = clip.Segment;
            var start = clip.Export.StartSeconds ?? segment.StartTimeSeconds;
            var end = clip.Export.EndExclusiveSeconds ?? segment.EndTimeSeconds;
            writer.WriteLine(string.Join(',',
                Csv(Path.GetFileName(clip.Export.OutputPath)),
                Csv(KindCode(segment.Kind)),
                Csv(segment.BehavioralEventNumber?.ToString(CultureInfo.InvariantCulture) ?? "na"),
                Csv(ResultCode(segment.Result)),
                segment.StartFrameIndex.ToString(CultureInfo.InvariantCulture),
                segment.EndFrameIndex.ToString(CultureInfo.InvariantCulture),
                Csv(FormatVideoTime(start)),
                Csv(FormatVideoTime(end)),
                (end - start).ToString("0.000", CultureInfo.InvariantCulture)));
        }
    }

    private static string KindCode(PlannedSegmentKind kind) => kind switch
    {
        PlannedSegmentKind.InitialHabituation => "habituacion_inicial",
        PlannedSegmentKind.Event => "evento",
        PlannedSegmentKind.InterTrialInterval => "iti",
        PlannedSegmentKind.FinalHabituation => "habituacion_final",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string ResultCode(PlannedBehavioralResult result) => result switch
    {
        PlannedBehavioralResult.Crossing => "cruce",
        PlannedBehavioralResult.NoCrossing => "no_cruce",
        PlannedBehavioralResult.Timeout => "timeout",
        PlannedBehavioralResult.NotApplicable => "na",
        _ => throw new ArgumentOutOfRangeException(nameof(result)),
    };

    private static string FormatVideoTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds:000}";
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
