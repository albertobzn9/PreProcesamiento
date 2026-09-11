using VideoBatchProcessor.Core.BehavioralData;
using VideoBatchProcessor.Core.LightDetection;
using VideoBatchProcessor.Core.Nomenclature;
using VideoBatchProcessor.Core.SessionFiles;
using VideoBatchProcessor.Core.VideoTransform;

namespace VideoBatchProcessor.Core.SessionPairing;

/// <summary>
/// Prepara y compara todas las sesiones antes de recortar. Cada video y cada
/// fuente conductual se leen una sola vez durante este análisis.
/// </summary>
public sealed class BatchSessionPairingAnalyzer
{
    private static readonly HashSet<string> s_videoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".m4v",
    };

    private readonly IVideoPairingEvidenceReader _videoReader;
    private readonly IBehavioralPairingEvidenceReader _behavioralReader;
    private readonly SessionPairingResolver _resolver;
    private readonly NomenclatureParser _nomenclatureParser;

    public BatchSessionPairingAnalyzer(
        IVideoPairingEvidenceReader? videoReader = null,
        IBehavioralPairingEvidenceReader? behavioralReader = null,
        SessionPairingResolver? resolver = null,
        NomenclatureParser? nomenclatureParser = null)
    {
        _videoReader = videoReader ?? new VideoPairingEvidenceReader();
        _behavioralReader = behavioralReader ?? new BehavioralPairingEvidenceReader();
        _resolver = resolver ?? new SessionPairingResolver();
        _nomenclatureParser = nomenclatureParser ?? new NomenclatureParser();
    }

    public BatchSessionPairingAnalysis AnalyzeDirectory(
        string inputDirectory,
        VideoTransformConfig transform,
        LightDetectionConfig lightConfig,
        IProgress<BatchSessionPairingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(inputDirectory) || !Directory.Exists(inputDirectory))
            throw new DirectoryNotFoundException("La carpeta de sesiones no existe.");

        var files = Directory.EnumerateFiles(inputDirectory, "*", SearchOption.AllDirectories).ToArray();
        var videos = files.Where(IsSourceVideo).ToArray();
        var behavioralSources = files.Where(IsMainBehavioralSource).ToArray();
        return Analyze(videos, behavioralSources, transform, lightConfig, progress, cancellationToken);
    }

    public BatchSessionPairingAnalysis Analyze(
        IEnumerable<string> videoPaths,
        IEnumerable<string> behavioralSourcePaths,
        VideoTransformConfig transform,
        LightDetectionConfig lightConfig,
        IProgress<BatchSessionPairingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(videoPaths);
        ArgumentNullException.ThrowIfNull(behavioralSourcePaths);
        ArgumentNullException.ThrowIfNull(transform);
        ArgumentNullException.ThrowIfNull(lightConfig);

        var selectedVideos = SessionVideoSelector.SelectOnePerSession(videoPaths).ToArray();
        var sources = behavioralSourcePaths
            .Where(path => !string.IsNullOrWhiteSpace(path) && IsMainBehavioralSource(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var totalInputs = selectedVideos.Length + sources.Length;
        var completedInputs = 0;
        var issues = new List<SessionPairingInputIssue>();
        var videoEvidence = new List<VideoPairingEvidence>();
        var behavioralEvidence = new List<BehavioralPairingEvidence>();

        for (var sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
        {
            var source = sources[sourceIndex];
            cancellationToken.ThrowIfCancellationRequested();
            Report("Leyendo datos conductuales", source, BehavioralProgress(sourceIndex, sources.Length));
            try
            {
                behavioralEvidence.Add(_behavioralReader.Read(source));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                issues.Add(new SessionPairingInputIssue(source, SessionPairingInputKind.BehavioralSource, exception.Message));
            }

            completedInputs++;
        }

        for (var videoIndex = 0; videoIndex < selectedVideos.Length; videoIndex++)
        {
            var selected = selectedVideos[videoIndex];
            cancellationToken.ThrowIfCancellationRequested();
            Report("Analizando luces del video", selected.VideoPath, VideoProgress(videoIndex, selectedVideos.Length, 0));
            try
            {
                var frameProgress = new InlineProgress<LightTimelineScanProgress>(update =>
                    progress?.Report(new BatchSessionPairingProgress(
                        completedInputs,
                        totalInputs,
                        selected.VideoPath,
                        "Analizando luces del video",
                        update.Percent,
                        VideoProgress(videoIndex, selectedVideos.Length, update.Percent))));
                videoEvidence.Add(_videoReader.Read(
                    selected.VideoPath,
                    transform,
                    lightConfig,
                    frameProgress,
                    cancellationToken));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                issues.Add(new SessionPairingInputIssue(selected.VideoPath, SessionPairingInputKind.Video, exception.Message));
            }

            completedInputs++;
        }

        var report = _resolver.Resolve(videoEvidence, behavioralEvidence);
        progress?.Report(new BatchSessionPairingProgress(totalInputs, totalInputs, null, "Emparejamiento terminado", 100, 100));
        return new BatchSessionPairingAnalysis(report, selectedVideos, videoEvidence, behavioralEvidence, issues);

        void Report(string stage, string path, int overallPercent) =>
            progress?.Report(new BatchSessionPairingProgress(completedInputs, totalInputs, path, stage, 0, overallPercent));

        static int BehavioralProgress(int index, int count) => count <= 0
            ? 5
            : (int)Math.Round(index * 5d / count);

        static int VideoProgress(int index, int count, int currentVideoPercent) => count <= 0
            ? 95
            : (int)Math.Round(5d + ((index + Math.Clamp(currentVideoPercent, 0, 100) / 100d) * 90d / count));
    }

    private bool IsSourceVideo(string path)
    {
        if (!s_videoExtensions.Contains(Path.GetExtension(path)))
            return false;

        return !_nomenclatureParser.TryParse(path, out var parsed) || parsed.IsSourceSession;
    }

    internal static bool IsMainBehavioralSource(string path)
    {
        var extension = Path.GetExtension(path);
        if (string.Equals(extension, ".mat", StringComparison.OrdinalIgnoreCase))
            return true;
        if (!string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
            return false;

        return !Path.GetFileNameWithoutExtension(path)
                   .EndsWith("_palanqueos", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(Path.GetFileName(path), "clips_exportados.csv", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(Path.GetFileName(path), "emparejamiento_sesiones.csv", StringComparison.OrdinalIgnoreCase);
    }
}

public interface IVideoPairingEvidenceReader
{
    VideoPairingEvidence Read(
        string videoPath,
        VideoTransformConfig transform,
        LightDetectionConfig lightConfig,
        IProgress<LightTimelineScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed class VideoPairingEvidenceReader : IVideoPairingEvidenceReader
{
    private readonly LightTimelineScanner _scanner;

    public VideoPairingEvidenceReader(LightTimelineScanner? scanner = null)
    {
        _scanner = scanner ?? new LightTimelineScanner();
    }

    public VideoPairingEvidence Read(
        string videoPath,
        VideoTransformConfig transform,
        LightDetectionConfig lightConfig,
        IProgress<LightTimelineScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var scan = _scanner.Scan(videoPath, transform, lightConfig, progress: progress, cancellationToken: cancellationToken);
        if (scan.Metadata.TotalFrames > int.MaxValue)
            throw new InvalidOperationException("El video tiene más frames de los que el modelo actual puede representar.");

        return new VideoPairingEvidence(
            Path.GetFullPath(videoPath),
            LightEventIntervalBuilder.Build(scan.Timeline),
            new LightTimelineScanRange(0, checked((int)scan.Metadata.TotalFrames - 1)),
            scan.Metadata.Fps,
            scan);
    }
}

public interface IBehavioralPairingEvidenceReader
{
    BehavioralPairingEvidence Read(string sourcePath);
}

public sealed class BehavioralPairingEvidenceReader : IBehavioralPairingEvidenceReader
{
    private readonly IBehavioralSessionReader _matReader;
    private readonly IBehavioralSessionReader _csvReader;

    public BehavioralPairingEvidenceReader(
        IBehavioralSessionReader? matReader = null,
        IBehavioralSessionReader? csvReader = null)
    {
        _matReader = matReader ?? new LegacyMatBehavioralSessionReader(new MatV5MatrixReader());
        _csvReader = csvReader ?? new CsvV1BehavioralSessionReader();
    }

    public BehavioralPairingEvidence Read(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var fullPath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("No se encontró la fuente conductual.", fullPath);

        var kind = Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".mat" => BehavioralSourceKind.LegacyMat,
            ".csv" when BatchSessionPairingAnalyzer.IsMainBehavioralSource(fullPath) => BehavioralSourceKind.CsvV1,
            ".csv" => throw new BehavioralDataFormatException("El CSV de palanqueos no puede usarse como tabla principal."),
            _ => throw new BehavioralDataFormatException("La fuente conductual debe ser un archivo .mat o CSV V1."),
        };
        var resolution = new BehavioralSourceResolution
        {
            SourcePath = fullPath,
            SourceKind = kind,
            Origin = BehavioralSourceOrigin.ExplicitOverride,
        };
        var data = kind == BehavioralSourceKind.LegacyMat
            ? _matReader.Read(resolution)
            : _csvReader.Read(resolution);
        if (data.Events.Count == 0)
            throw new BehavioralDataFormatException("La fuente conductual no contiene eventos utilizables.");

        return new BehavioralPairingEvidence(fullPath, data.Events);
    }
}

public enum SessionPairingInputKind
{
    Video,
    BehavioralSource,
}

public sealed record SessionPairingInputIssue(
    string Path,
    SessionPairingInputKind Kind,
    string Message);

public sealed record BatchSessionPairingAnalysis(
    SessionPairingReport Report,
    IReadOnlyList<SelectedSessionVideo> SelectedVideos,
    IReadOnlyList<VideoPairingEvidence> VideoEvidence,
    IReadOnlyList<BehavioralPairingEvidence> BehavioralEvidence,
    IReadOnlyList<SessionPairingInputIssue> InputIssues);

public sealed record BatchSessionPairingProgress(
    int CompletedInputs,
    int TotalInputs,
    string? CurrentPath,
    string Stage,
    int CurrentInputPercent,
    int? OverallPercent = null)
{
    public int Percent => OverallPercent is not null
        ? Math.Clamp(OverallPercent.Value, 0, 100)
        : TotalInputs == 0
            ? 100
            : (int)Math.Round((CompletedInputs + Math.Clamp(CurrentInputPercent, 0, 100) / 100d) * 100d / TotalInputs);
}

internal sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}
