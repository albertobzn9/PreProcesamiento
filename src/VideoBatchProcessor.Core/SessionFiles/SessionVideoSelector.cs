namespace VideoBatchProcessor.Core.SessionFiles;

/// <summary>
/// Selecciona una sola fuente de video por sesión cuando la misma captura está
/// presente en más de un contenedor (por ejemplo, stem.mp4 y stem.mkv). Los
/// alternativos se conservan en disco y se informan; nunca se borran solos.
/// </summary>
public static class SessionVideoSelector
{
    private static readonly IReadOnlyDictionary<string, int> s_extensionPriority =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [".mp4"] = 0,
            [".mkv"] = 1,
            [".mov"] = 2,
            [".avi"] = 3,
            [".m4v"] = 4,
        };

    public static IReadOnlyList<SelectedSessionVideo> SelectOnePerSession(
        IEnumerable<string> videoPaths)
    {
        ArgumentNullException.ThrowIfNull(videoPaths);

        return videoPaths
            .Where(path => !string.IsNullOrWhiteSpace(path) &&
                           s_extensionPriority.ContainsKey(Path.GetExtension(path)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .GroupBy(SessionKey, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group
                    .OrderBy(path => s_extensionPriority[Path.GetExtension(path)])
                    .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return new SelectedSessionVideo(ordered[0], ordered[1..]);
            })
            .ToArray();
    }

    private static string SessionKey(string videoPath) =>
        Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(videoPath)) ?? string.Empty,
            Path.GetFileNameWithoutExtension(videoPath));
}

public sealed record SelectedSessionVideo(
    string VideoPath,
    IReadOnlyList<string> AlternateVideoPaths);
