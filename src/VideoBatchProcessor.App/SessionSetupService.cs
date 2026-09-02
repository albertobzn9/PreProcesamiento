using VideoBatchProcessor.Core.Nomenclature;
using VideoBatchProcessor.Core.SessionResolver;
using VideoBatchProcessor.Core.SessionFiles;

namespace VideoBatchProcessor.App;

public sealed record SessionSetupEntry(
    ParsedFileName ParsedName,
    SessionMetadata Metadata,
    IReadOnlyList<string>? AlternateVideoPaths = null);

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
        entry with { Metadata = _resolver.Complete(entry.Metadata, values) };

    public static bool IsSupportedVideo(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path));

    private SessionSetupEntry AnalyzeFile(string videoPath, IReadOnlyList<string> alternateVideoPaths)
    {
        _parser.TryParse(videoPath, out var parsedName);
        var metadata = _resolver.Resolve(parsedName, BatchManifest.Empty, videoPath);
        return new SessionSetupEntry(parsedName, metadata, alternateVideoPaths);
    }

    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".avi", ".mov", ".mkv", ".m4v" };
}
