using VideoBatchProcessor.Core.Nomenclature;
using VideoBatchProcessor.Core.SessionResolver;
using VideoBatchProcessor.Core.SessionFiles;
using VideoBatchProcessor.Core.SessionPairing;

namespace VideoBatchProcessor.App;

public sealed record SessionSetupEntry(
    ParsedFileName ParsedName,
    SessionMetadata Metadata,
    IReadOnlyList<string>? AlternateVideoPaths = null,
    bool BehavioralSourceLoaded = false,
    string? BehavioralSourceReadError = null);

public sealed class SessionSetupService
{
    private readonly NomenclatureParser _parser = new();
    private readonly SessionMetadataResolver _resolver = new();

    public IReadOnlyList<SessionSetupEntry> AnalyzeFiles(IEnumerable<string> videoPaths)
    {
        return SessionVideoSelector.SelectOnePerSession(videoPaths.Where(IsSupportedVideo))
            .Select(source => AnalyzeFile(source.VideoPath, source.AlternateVideoPaths))
            .OrderBy(entry => Path.GetFileName(entry.Metadata.SourceVideoPath), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public SessionSetupEntry Complete(SessionSetupEntry entry, UserFieldValues values) =>
        ValidateBehavioralSource(entry with { Metadata = _resolver.Complete(entry.Metadata, values) });

    public SessionSetupEntry RefreshBehavioralSource(SessionSetupEntry entry, string? explicitPath = null)
    {
        var manifest = new BatchManifest
        {
            Overrides = new Dictionary<string, FileOverride>
            {
                [Path.GetFileNameWithoutExtension(entry.Metadata.SourceVideoPath)] = new()
                {
                    Iniciales = entry.Metadata.Iniciales,
                    Sexo = entry.Metadata.Sexo,
                    Tratamiento = entry.Metadata.Tratamiento,
                    BehavioralSourcePath = explicitPath,
                },
            },
        };
        return ValidateBehavioralSource(entry with
        {
            Metadata = _resolver.Resolve(entry.ParsedName, manifest, entry.Metadata.SourceVideoPath),
        });
    }

    private static SessionSetupEntry ValidateBehavioralSource(SessionSetupEntry entry)
    {
        if (entry.Metadata.SourceBehavioralPath is not { } path)
            return entry with { BehavioralSourceLoaded = false, BehavioralSourceReadError = null };
        try
        {
            new BehavioralPairingEvidenceReader().Read(path);
            return entry with { BehavioralSourceLoaded = true, BehavioralSourceReadError = null };
        }
        catch (Exception exception)
        {
            return entry with { BehavioralSourceLoaded = false, BehavioralSourceReadError = exception.Message };
        }
    }

    public static bool IsSupportedVideo(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path));

    private SessionSetupEntry AnalyzeFile(string videoPath, IReadOnlyList<string> alternateVideoPaths)
    {
        _parser.TryParse(videoPath, out var parsedName);
        var metadata = _resolver.Resolve(parsedName, BatchManifest.Empty, videoPath);
        return ValidateBehavioralSource(new SessionSetupEntry(parsedName, metadata, alternateVideoPaths));
    }

    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".avi", ".mov", ".mkv", ".m4v" };
}
