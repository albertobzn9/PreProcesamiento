using VideoBatchProcessor.Core.BehavioralData;
using VideoBatchProcessor.Core.BehavioralSynchronization;
using VideoBatchProcessor.Core.ClipExport;
using VideoBatchProcessor.Core.Diagnostics;
using VideoBatchProcessor.Core.LightDetection;
using VideoBatchProcessor.Core.Nomenclature;
using VideoBatchProcessor.Core.SegmentPlanning;
using VideoBatchProcessor.Core.SessionResolver;
using VideoBatchProcessor.Core.SessionFiles;
using VideoBatchProcessor.Core.SessionPairing;
using VideoBatchProcessor.Core.VideoTransform;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace VideoBatchProcessor.Core.BatchProcessing;

/// <summary>
/// Ejecuta el flujo de Cruces Seguros y Cruces Peligrosos: descubre videos,
/// confirma cada fuente conductual por contenido, valida la sincronización y
/// exporta los segmentos planeados. No modifica las fuentes originales.
/// </summary>
public sealed class BatchOrchestrator
{
    private static readonly Regex s_recoverableSwappedLegacyName = new(
        @"^exp_\d{4}_(cs|cp|dis|pb)_r\d+d\d+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
    private readonly BatchSessionPairingAnalyzer _pairingAnalyzer;

    public BatchOrchestrator(
        NomenclatureParser? nomenclatureParser = null,
        SessionMetadataResolver? metadataResolver = null,
        LightTimelineScanner? timelineScanner = null,
        BehavioralVideoSynchronizer? synchronizer = null,
        SegmentPlanner? segmentPlanner = null,
        ClipExporter? clipExporter = null,
        LightTimelineDiagnosticExcelExporter? diagnosticExporter = null,
        BatchSessionPairingAnalyzer? pairingAnalyzer = null)
    {
        _nomenclatureParser = nomenclatureParser ?? new NomenclatureParser();
        _metadataResolver = metadataResolver ?? new SessionMetadataResolver();
        _timelineScanner = timelineScanner ?? new LightTimelineScanner();
        _synchronizer = synchronizer ?? new BehavioralVideoSynchronizer();
        _segmentPlanner = segmentPlanner ?? new SegmentPlanner();
        _clipExporter = clipExporter ?? new ClipExporter();
        _diagnosticExporter = diagnosticExporter ?? new LightTimelineDiagnosticExcelExporter();
        _pairingAnalyzer = pairingAnalyzer ?? new BatchSessionPairingAnalyzer(
            new VideoPairingEvidenceReader(_timelineScanner),
            resolver: new SessionPairingResolver(_synchronizer));
    }

    /// <summary>
    /// Encuentra recursivamente sesiones fuente CS/CP y nombres legacy
    /// intercambiados que todavía puedan recuperarse por contenido. Omite
    /// clips de salida y fases que pertenecen a etapas posteriores.
    /// </summary>
    public IReadOnlyList<BatchCandidate> DiscoverCandidates(string inputDirectory)
    {
        if (string.IsNullOrWhiteSpace(inputDirectory) || !Directory.Exists(inputDirectory))
            return [];

        return DiscoverCandidates(
            Directory.EnumerateFiles(inputDirectory, "*", SearchOption.AllDirectories)
                .Where(path => s_videoExtensions.Contains(Path.GetExtension(path))));
    }

    /// <summary>
    /// Prepara una selección explícita de una o más sesiones. La interfaz usa
    /// esta entrada para que un video elegido individualmente siga el mismo
    /// flujo completo que una carpeta con muchas sesiones.
    /// </summary>
    public IReadOnlyList<BatchCandidate> DiscoverCandidates(IEnumerable<string> videoPaths)
    {
        ArgumentNullException.ThrowIfNull(videoPaths);

        return SessionVideoSelector.SelectOnePerSession(
                videoPaths.Where(path => !string.IsNullOrWhiteSpace(path) &&
                                         s_videoExtensions.Contains(Path.GetExtension(path))))
            .Select(source => CreateCandidate(source.VideoPath, source.AlternateVideoPaths))
            .ToArray();
    }

    /// <summary>
    /// Revisa las salidas antes de escanear frames, para que la interfaz pueda
    /// pedir una decisión explícita sin gastar tiempo ni sobrescribir evidencia.
    /// </summary>
    public IReadOnlyList<ExistingBatchOutput> FindExistingOutputs(IEnumerable<string> videoPaths) =>
        DiscoverCandidates(videoPaths)
            .Where(candidate => candidate.IsSupportedSource)
            .Select(candidate => new ExistingBatchOutput(
                candidate.VideoPath,
                GetSessionOutputDirectory(candidate.VideoPath)))
            .Where(item => Directory.Exists(item.OutputDirectory))
            .ToArray();

    public async Task<BatchReport> RunAsync(
        BatchProcessingRequest request,
        IProgress<BatchProcessingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        var candidates = request.SourceVideoPaths is { Count: > 0 }
            ? DiscoverCandidates(request.SourceVideoPaths)
            : DiscoverCandidates(request.InputDirectory);
        var pairingCandidates = candidates.Where(candidate => candidate.IsSupportedSource).ToArray();
        var behavioralSources = DiscoverBehavioralSources(request, pairingCandidates);
        var pairingProgress = new InlineProgress<BatchSessionPairingProgress>(update =>
            progress?.Report(new BatchProcessingProgress(
                0,
                candidates.Count,
                update.CurrentPath,
                $"Emparejando sesiones: {update.Stage}",
                (int)Math.Round(update.Percent * 0.75d))));
        var pairing = _pairingAnalyzer.Analyze(
            pairingCandidates.Select(candidate => candidate.VideoPath),
            behavioralSources,
            request.Transform,
            request.LightConfig,
            pairingProgress,
            cancellationToken);
        var pairingReportPath = Path.Combine(request.OutputDirectory, "emparejamiento_sesiones.csv");
        SessionPairingReportCsvWriter.Write(pairingReportPath, pairing);
        var pairingByVideo = pairing.Report.Resolutions.ToDictionary(
            item => Path.GetFullPath(item.VideoPath),
            StringComparer.OrdinalIgnoreCase);
        var evidenceByVideo = pairing.VideoEvidence.ToDictionary(
            item => Path.GetFullPath(item.VideoPath),
            StringComparer.OrdinalIgnoreCase);
        var evidenceBySource = pairing.BehavioralEvidence.ToDictionary(
            item => Path.GetFullPath(item.SourcePath),
            StringComparer.OrdinalIgnoreCase);
        var reports = new List<BatchSessionReport>();
        for (var index = 0; index < candidates.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = candidates[index];
            ReportProgress("Preparando sesión", index, 0);

            BatchSessionReport report;
            if (!candidate.IsSupportedSource)
            {
                report = BatchSessionReport.Skipped(candidate.VideoPath, candidate.SkipReason!);
            }
            else if (!pairingByVideo.TryGetValue(Path.GetFullPath(candidate.VideoPath), out var resolution) ||
                     resolution.Status != SessionPairingStatus.Confirmed ||
                     resolution.SelectedCandidate is null ||
                     resolution.BehavioralSourcePath is null ||
                     !evidenceByVideo.TryGetValue(Path.GetFullPath(candidate.VideoPath), out var videoEvidence) ||
                     !evidenceBySource.TryGetValue(Path.GetFullPath(resolution.BehavioralSourcePath), out var behavioralEvidence))
            {
                report = BatchSessionReport.Blocked(
                    candidate.VideoPath,
                    resolution?.Message ?? FindInputIssue(pairing, candidate.VideoPath) ?? "No se pudo preparar la evidencia de esta sesión.");
            }
            else if (!TryCreatePairedCandidate(candidate, resolution.BehavioralSourcePath, out var pairedCandidate, out var pairingError))
            {
                report = BatchSessionReport.Blocked(candidate.VideoPath, pairingError!);
            }
            else
            {
                report = await ProcessCandidateAsync(
                    pairedCandidate!,
                    request,
                    new PairedSessionEvidence(videoEvidence, behavioralEvidence, resolution.SelectedCandidate.Synchronization),
                    (message, sessionPercent) => ReportProgress(message, index, sessionPercent),
                    cancellationToken);
            }
            reports.Add(report);
            ReportProgress("Sesión terminada", index + 1, 0);
        }

        progress?.Report(new BatchProcessingProgress(
            candidates.Count,
            candidates.Count,
            null,
            "Lote terminado",
            100,
            IsComplete: true));
        return new BatchReport(request.InputDirectory, request.OutputDirectory, reports, pairingReportPath);

        void ReportProgress(string message, int completedSessions, double sessionPercent)
        {
            var processingFraction = candidates.Count == 0
                ? 1d
                : (completedSessions + Math.Clamp(sessionPercent, 0, 100) / 100d) / candidates.Count;
            var percent = 75 + (int)Math.Round(processingFraction * 25d);
            progress?.Report(new BatchProcessingProgress(
                completedSessions,
                candidates.Count,
                completedSessions < candidates.Count ? candidates[completedSessions].VideoPath : null,
                message,
                percent));
        }
    }

    private async Task<BatchSessionReport> ProcessCandidateAsync(
        BatchCandidate candidate,
        BatchProcessingRequest request,
        PairedSessionEvidence pairedEvidence,
        Action<string, double> reportProgress,
        CancellationToken cancellationToken)
    {
        var sessionOutputDirectory = GetSessionOutputDirectory(candidate.VideoPath);
        if (Directory.Exists(sessionOutputDirectory))
        {
            switch (request.ExistingOutputPolicy)
            {
                case ExistingOutputPolicy.SkipExisting:
                    return BatchSessionReport.Skipped(
                        candidate.VideoPath,
                        "Se omitió porque ya existe una carpeta de resultados para esta sesión.");
                case ExistingOutputPolicy.ArchiveAndReplace:
                    try
                    {
                        ArchiveOutputDirectory(sessionOutputDirectory);
                    }
                    catch (Exception exception)
                    {
                        return BatchSessionReport.Failed(
                            candidate.VideoPath,
                            $"No se pudo archivar la salida existente: {exception.Message}");
                    }
                    break;
                case ExistingOutputPolicy.Block:
                default:
                    return BatchSessionReport.Blocked(
                        candidate.VideoPath,
                        "Ya existe una carpeta de salida para esta sesión. Elige omitirla o archivarla antes de volver a procesar.");
            }
        }

        return await ProcessSessionAsync(
            candidate,
            request,
            pairedEvidence,
            sessionOutputDirectory,
            reportProgress,
            cancellationToken);
    }

    private BatchCandidate CreateCandidate(string videoPath, IReadOnlyList<string> alternateVideoPaths)
    {
        if (!_nomenclatureParser.TryParse(videoPath, out var parsed))
        {
            var recoverable = s_recoverableSwappedLegacyName.IsMatch(Path.GetFileNameWithoutExtension(videoPath));
            return new BatchCandidate(
                videoPath,
                parsed,
                recoverable,
                recoverable ? null : "El nombre no corresponde a una sesión fuente reconocida.",
                alternateVideoPaths);
        }
        if (!parsed.IsSourceSession)
            return new BatchCandidate(videoPath, parsed, false, "Es un clip de salida; nunca se vuelve a procesar como entrada.", alternateVideoPaths);
        if (parsed.FaseEstandar is not "f2" and not "f4")
            return new BatchCandidate(videoPath, parsed, false, "Esta validación procesa Cruces Seguros (f2/cs) y Cruces Peligrosos (f4/cp).", alternateVideoPaths);

        return new BatchCandidate(videoPath, parsed, true, null, alternateVideoPaths);
    }

    private async Task<BatchSessionReport> ProcessSessionAsync(
        BatchCandidate candidate,
        BatchProcessingRequest request,
        PairedSessionEvidence pairedEvidence,
        string sessionOutputDirectory,
        Action<string, double> reportProgress,
        CancellationToken cancellationToken)
    {
        try
        {
            reportProgress("Preparando metadata y evidencia", 5);
            var behavioralSourcePath = pairedEvidence.Behavioral.SourcePath;
            var metadata = _metadataResolver.Resolve(
                candidate.ParsedName!,
                WithBehavioralOverride(request.Manifest, candidate.VideoPath, behavioralSourcePath),
                candidate.VideoPath);
            if (!metadata.IsComplete)
            {
                return BatchSessionReport.Blocked(
                    candidate.VideoPath,
                    $"Faltan datos para nombrar la salida: {string.Join(", ", metadata.MissingFields)}.");
            }

            var scan = pairedEvidence.Video.ScanResult ?? throw new InvalidOperationException(
                "El preflight no conservó el escaneo necesario para exportar esta sesión.");
            var intervals = pairedEvidence.Video.VisualIntervals;
            var fullRange = pairedEvidence.Video.ScanRange;
            var synchronization = pairedEvidence.Synchronization;
            reportProgress("Pareja video-conducta confirmada", 82);
            if (synchronization.Status == BehavioralVideoSynchronizationStatus.Blocked)
            {
                return BatchSessionReport.Blocked(
                    candidate.VideoPath,
                    DescribeSynchronizationBlock(synchronization),
                    behavioralSourcePath,
                    synchronization);
            }

            reportProgress("Planeando recortes", 88);
            var plan = _segmentPlanner.Plan(new SegmentPlanningInput(
                scan.Metadata,
                fullRange,
                intervals,
                pairedEvidence.Behavioral.Events,
                synchronization));
            if (plan.Segments.Count == 0)
            {
                return BatchSessionReport.Blocked(
                    candidate.VideoPath,
                    "No se pudo planear ningún segmento de esta sesión.",
                    behavioralSourcePath,
                    synchronization,
                    plan.Warnings);
            }

            if (Directory.Exists(sessionOutputDirectory))
            {
                return BatchSessionReport.Blocked(
                    candidate.VideoPath,
                    "Apareció una carpeta de salida mientras se procesaba la sesión; se conserva para no sobrescribir clips.",
                    behavioralSourcePath,
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
                pairedEvidence.Behavioral.Events,
                behavioralSourcePath,
                null), diagnosticPath);

            var exports = new List<BatchClipReport>();
            var segmentsToExport = SelectSegmentsForExport(plan.Segments, request.ExportMode);
            var outputSegmentCodes = OutputSegmentCodePlanner.Create(plan.Segments);
            for (var exportIndex = 0; exportIndex < segmentsToExport.Count; exportIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var segment = segmentsToExport[exportIndex];
                reportProgress($"Exportando clip {exportIndex + 1} de {segmentsToExport.Count}", 92 + exportIndex * 7d / Math.Max(1, segmentsToExport.Count));
                var outputPath = Path.Combine(
                    sessionOutputDirectory,
                    BuildOutputName(metadata, segment, outputSegmentCodes[segment]));
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
                        behavioralSourcePath,
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
                behavioralSourcePath,
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

    private static IReadOnlyList<string> DiscoverBehavioralSources(
        BatchProcessingRequest request,
        IReadOnlyList<BatchCandidate> candidates)
    {
        if (request.BehavioralSourcePaths is { Count: > 0 })
            return request.BehavioralSourcePaths;

        return candidates
            .Select(candidate => Path.GetDirectoryName(Path.GetFullPath(candidate.VideoPath)))
            .Where(directory => !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(directory => Directory.EnumerateFiles(directory!, "*", SearchOption.TopDirectoryOnly))
            .Where(BatchSessionPairingAnalyzer.IsMainBehavioralSource)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private bool TryCreatePairedCandidate(
        BatchCandidate original,
        string behavioralSourcePath,
        out BatchCandidate? paired,
        out string? error)
    {
        paired = null;
        error = null;
        _nomenclatureParser.TryParse(behavioralSourcePath, out var behavioralName);
        var parsed = behavioralName.IsSourceSession ? behavioralName : original.ParsedName;
        if (parsed is null || !parsed.IsSourceSession)
        {
            error = "La evidencia coincide, pero ni el video ni la tabla tienen un nombre con día y rata recuperables.";
            return false;
        }
        if (parsed.FaseEstandar is not "f2" and not "f4")
        {
            error = "La pareja confirmada no pertenece a Cruces Seguros ni Cruces Peligrosos.";
            return false;
        }

        paired = original with { ParsedName = parsed, IsSupportedSource = true, SkipReason = null };
        return true;
    }

    private static string? FindInputIssue(BatchSessionPairingAnalysis analysis, string videoPath) =>
        analysis.InputIssues.FirstOrDefault(item =>
            item.Kind == SessionPairingInputKind.Video &&
            string.Equals(Path.GetFullPath(item.Path), Path.GetFullPath(videoPath), StringComparison.OrdinalIgnoreCase))?.Message;

    private static BatchManifest WithBehavioralOverride(
        BatchManifest manifest,
        string videoPath,
        string behavioralSourcePath)
    {
        var overrides = new Dictionary<string, FileOverride>(manifest.Overrides, StringComparer.OrdinalIgnoreCase)
        {
            [Path.GetFileNameWithoutExtension(videoPath)] = manifest.Overrides.TryGetValue(
                Path.GetFileNameWithoutExtension(videoPath),
                out var current)
                ? current with { BehavioralSourcePath = behavioralSourcePath }
                : new FileOverride { BehavioralSourcePath = behavioralSourcePath },
        };
        return manifest with { Overrides = overrides };
    }

    private static string BuildOutputName(
        SessionMetadata metadata,
        PlannedVideoSegment segment,
        string segmentCode)
    {
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

    public static IReadOnlyList<PlannedVideoSegment> SelectSegmentsForExport(
        IReadOnlyList<PlannedVideoSegment> segments,
        BatchExportMode exportMode)
    {
        if (exportMode == BatchExportMode.AllSegments)
            return segments;

        // La validación rápida comprueba límites de eventos e ITIs sin gastar
        // tiempo exportando habituaciones largas.
        return segments
            .Where(item => item.Kind is PlannedSegmentKind.Event or PlannedSegmentKind.InterTrialInterval)
            .OrderBy(item => item.Sequence)
            .Take(5)
            .ToArray();
    }

    private static string DescribeSynchronizationBlock(BehavioralVideoSynchronizationResult synchronization) =>
        synchronization.Findings.Count == 0
            ? "La sincronización entre video y MAT quedó bloqueada."
            : string.Join(" ", synchronization.Findings.Select(item => item.Message));

    private static string GetSessionOutputDirectory(string videoPath)
    {
        var sourceDirectory = Path.GetDirectoryName(videoPath);
        if (string.IsNullOrWhiteSpace(sourceDirectory))
            throw new ArgumentException("No se pudo identificar la carpeta del video fuente.", nameof(videoPath));

        return Path.Combine(sourceDirectory, Path.GetFileNameWithoutExtension(videoPath));
    }

    private static void ArchiveOutputDirectory(string outputDirectory)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var archivedDirectory = $"{outputDirectory}_anterior_{stamp}";
        var suffix = 2;
        while (Directory.Exists(archivedDirectory))
            archivedDirectory = $"{outputDirectory}_anterior_{stamp}_{suffix++}";

        Directory.Move(outputDirectory, archivedDirectory);
    }

    private static void ValidateRequest(BatchProcessingRequest request)
    {
        if ((request.SourceVideoPaths is null || request.SourceVideoPaths.Count == 0) &&
            (string.IsNullOrWhiteSpace(request.InputDirectory) || !Directory.Exists(request.InputDirectory)))
            throw new DirectoryNotFoundException("La carpeta de entrada no existe.");
        if (request.SourceVideoPaths is { Count: > 0 } && request.SourceVideoPaths.Any(path => string.IsNullOrWhiteSpace(path) || !File.Exists(path)))
            throw new FileNotFoundException("Uno de los videos seleccionados ya no existe.");
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
    BatchExportMode ExportMode = BatchExportMode.ValidationSample,
    IReadOnlyList<string>? SourceVideoPaths = null,
    ExistingOutputPolicy ExistingOutputPolicy = ExistingOutputPolicy.Block,
    IReadOnlyList<string>? BehavioralSourcePaths = null);

public enum BatchExportMode
{
    ValidationSample,
    AllSegments,
}

public enum ExistingOutputPolicy
{
    Block,
    SkipExisting,
    ArchiveAndReplace,
}

public sealed record BatchCandidate(
    string VideoPath,
    ParsedFileName? ParsedName,
    bool IsSupportedSource,
    string? SkipReason,
    IReadOnlyList<string>? AlternateVideoPaths = null)
{
    public bool IsCsSource => IsSupportedSource && ParsedName?.FaseEstandar == "f2";
    public bool IsCpSource => IsSupportedSource && ParsedName?.FaseEstandar == "f4";
}

public sealed record ExistingBatchOutput(string VideoPath, string OutputDirectory);

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
    IReadOnlyList<BatchSessionReport> Sessions,
    string? PairingReportPath = null)
{
    public int ExportedSessionCount => Sessions.Count(item => item.Status is BatchSessionStatus.Exported or BatchSessionStatus.ExportedWithWarnings);
    public int BlockedSessionCount => Sessions.Count(item => item.Status == BatchSessionStatus.Blocked);
    public int FailedSessionCount => Sessions.Count(item => item.Status == BatchSessionStatus.Failed);
    public int SkippedSessionCount => Sessions.Count(item => item.Status == BatchSessionStatus.Skipped);
    public int ExportedClipCount => Sessions.Sum(item => item.Clips.Count(clip => clip.Export.Succeeded));
}

internal sealed record PairedSessionEvidence(
    VideoPairingEvidence Video,
    BehavioralPairingEvidence Behavioral,
    BehavioralVideoSynchronizationResult Synchronization);

public sealed record BatchProcessingProgress(
    int CompletedSessions,
    int TotalSessions,
    string? CurrentVideoPath,
    string Message,
    int? PercentOverride = null,
    bool IsComplete = false)
{
    public int Percent
    {
        get
        {
            if (IsComplete)
                return 100;

            var calculated = PercentOverride ?? (TotalSessions == 0
                ? 0
                : (int)Math.Round(CompletedSessions * 100d / TotalSessions));
            return Math.Clamp(calculated, 0, 99);
        }
    }
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
        writer.WriteLine("archivo_clip,tipo,ensayo,resultado,frame_inicio_evento,frame_final_evento,inicio_evento,final_evento,frame_inicio_clip,frame_final_clip,inicio_clip,final_clip,duracion_clip_s");

        foreach (var clip in clips.Where(item => item.Export.Succeeded))
        {
            var segment = clip.Segment;
            var clipStart = clip.Export.StartSeconds ?? segment.StartTimeSeconds;
            var clipEnd = clip.Export.EndExclusiveSeconds ?? segment.EndTimeSeconds;
            writer.WriteLine(string.Join(',',
                Csv(Path.GetFileName(clip.Export.OutputPath)),
                Csv(KindCode(segment.Kind)),
                Csv(segment.BehavioralEventNumber?.ToString(CultureInfo.InvariantCulture) ?? "na"),
                Csv(ResultCode(segment.Result)),
                segment.StartFrameIndex.ToString(CultureInfo.InvariantCulture),
                segment.EndFrameIndex.ToString(CultureInfo.InvariantCulture),
                Csv(FormatVideoTime(segment.StartTimeSeconds)),
                Csv(FormatVideoTime(segment.EndTimeSeconds)),
                (clip.Export.StartFrameIndex ?? segment.StartFrameIndex).ToString(CultureInfo.InvariantCulture),
                (clip.Export.EndFrameIndex ?? segment.EndFrameIndex).ToString(CultureInfo.InvariantCulture),
                Csv(FormatVideoTime(clipStart)),
                Csv(FormatVideoTime(clipEnd)),
                (clipEnd - clipStart).ToString("0.000", CultureInfo.InvariantCulture)));
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
