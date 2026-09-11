using System.Globalization;
using VideoBatchProcessor.Core.BehavioralData;
using VideoBatchProcessor.Core.BehavioralSynchronization;
using VideoBatchProcessor.Core.Diagnostics;
using VideoBatchProcessor.Core.LightDetection;

namespace VideoBatchProcessor.Core.SessionPairing;

/// <summary>
/// Compara la evidencia visual de varios videos contra varias fuentes
/// conductuales. Solo confirma parejas que sean compatibles, mutuamente
/// preferidas y claramente mejores que sus alternativas.
/// </summary>
public sealed class SessionPairingResolver
{
    private const int MinimumMatchedEvents = 3;
    private const double MinimumBehavioralCoverage = 0.90d;
    private const double MaximumMedianResidualSeconds = 0.15d;
    private const double MaximumResidualSeconds = 0.75d;
    private const double MinimumCoverageAdvantage = 0.10d;
    private const double MinimumResidualAdvantageSeconds = 0.10d;

    private readonly BehavioralVideoSynchronizer _synchronizer;

    public SessionPairingResolver(BehavioralVideoSynchronizer? synchronizer = null)
    {
        _synchronizer = synchronizer ?? new BehavioralVideoSynchronizer();
    }

    public SessionPairingReport Resolve(
        IReadOnlyList<VideoPairingEvidence> videos,
        IReadOnlyList<BehavioralPairingEvidence> behavioralSources)
    {
        ArgumentNullException.ThrowIfNull(videos);
        ArgumentNullException.ThrowIfNull(behavioralSources);
        ValidateUniquePaths(videos.Select(item => item.VideoPath), nameof(videos));
        ValidateUniquePaths(behavioralSources.Select(item => item.SourcePath), nameof(behavioralSources));

        var candidates = (
            from video in videos
            from source in behavioralSources
            select Evaluate(video, source))
            .ToArray();
        var viable = candidates.Where(item => item.IsViable).ToArray();

        var rankedByVideo = videos.ToDictionary(
            video => video.VideoPath,
            video => Rank(viable.Where(item => PathsEqual(item.VideoPath, video.VideoPath))).ToArray(),
            StringComparer.OrdinalIgnoreCase);
        var rankedBySource = behavioralSources.ToDictionary(
            source => source.SourcePath,
            source => Rank(viable.Where(item => PathsEqual(item.BehavioralSourcePath, source.SourcePath))).ToArray(),
            StringComparer.OrdinalIgnoreCase);

        var confirmed = new Dictionary<string, SessionPairingCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var video in videos)
        {
            var videoCandidates = rankedByVideo[video.VideoPath];
            if (videoCandidates.Length == 0)
                continue;

            var best = videoCandidates[0];
            var sourceCandidates = rankedBySource[best.BehavioralSourcePath];
            if (sourceCandidates.Length == 0 ||
                !PathsEqual(sourceCandidates[0].VideoPath, video.VideoPath) ||
                !HasClearAdvantage(best, videoCandidates) ||
                !HasClearAdvantage(best, sourceCandidates))
            {
                continue;
            }

            confirmed.Add(video.VideoPath, best);
        }

        var resolutions = videos.Select(video =>
        {
            var alternatives = rankedByVideo[video.VideoPath].Take(3).ToArray();
            if (confirmed.TryGetValue(video.VideoPath, out var selected))
            {
                return new SessionPairingResolution(
                    video.VideoPath,
                    SessionPairingStatus.Confirmed,
                    selected.BehavioralSourcePath,
                    selected,
                    alternatives,
                    "La pareja tiene evidencia suficiente y es la mejor opción para ambos archivos.");
            }

            if (alternatives.Length > 0)
            {
                return new SessionPairingResolution(
                    video.VideoPath,
                    SessionPairingStatus.Ambiguous,
                    null,
                    null,
                    alternatives,
                    "Hay evidencia compatible, pero no una ventaja suficiente para asignar la pareja automáticamente.");
            }

            return new SessionPairingResolution(
                video.VideoPath,
                SessionPairingStatus.NoMatch,
                null,
                null,
                [],
                "Ninguna fuente conductual alcanzó la evidencia mínima para este video.");
        }).ToArray();

        var assignedSources = confirmed.Values
            .Select(item => item.BehavioralSourcePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unassignedSources = behavioralSources
            .Select(item => item.SourcePath)
            .Where(path => !assignedSources.Contains(path))
            .ToArray();

        return new SessionPairingReport(
            resolutions,
            unassignedSources,
            FindDuplicateBehavioralEvidence(behavioralSources),
            candidates);
    }

    private SessionPairingCandidate Evaluate(
        VideoPairingEvidence video,
        BehavioralPairingEvidence source)
    {
        var synchronization = _synchronizer.Synchronize(
            video.VisualIntervals,
            source.Events,
            video.ScanRange,
            video.FramesPerSecond,
            source.SourcePath);
        var residuals = synchronization.Comparison.Rows
            .Where(row => row.Status == DiagnosticComparisonStatus.Matched && row.StartResidualSeconds is not null)
            .Select(row => Math.Abs(row.StartResidualSeconds!.Value))
            .OrderBy(value => value)
            .ToArray();
        var matchedFraction = source.Events.Count == 0
            ? 0d
            : synchronization.Comparison.MatchedCount / (double)source.Events.Count;
        var medianResidual = residuals.Length == 0 ? double.PositiveInfinity : Median(residuals);
        var maximumResidual = residuals.Length == 0 ? double.PositiveInfinity : residuals[^1];
        var viable = synchronization.Status != BehavioralVideoSynchronizationStatus.Blocked &&
                     synchronization.Comparison.MatchedCount >= MinimumMatchedEvents &&
                     matchedFraction >= MinimumBehavioralCoverage &&
                     medianResidual <= MaximumMedianResidualSeconds &&
                     maximumResidual <= MaximumResidualSeconds;

        return new SessionPairingCandidate(
            video.VideoPath,
            source.SourcePath,
            synchronization,
            matchedFraction,
            medianResidual,
            maximumResidual,
            SameStem(video.VideoPath, source.SourcePath),
            viable);
    }

    private static IEnumerable<SessionPairingCandidate> Rank(IEnumerable<SessionPairingCandidate> candidates) =>
        candidates
            .OrderByDescending(item => item.MatchedFraction)
            .ThenByDescending(item => item.Synchronization.Comparison.MatchedCount)
            .ThenBy(item => item.MedianAbsoluteResidualSeconds)
            .ThenBy(item => item.BehavioralSourcePath, StringComparer.OrdinalIgnoreCase);

    private static bool HasClearAdvantage(
        SessionPairingCandidate best,
        IReadOnlyList<SessionPairingCandidate> rankedCandidates)
    {
        if (rankedCandidates.Count < 2)
            return true;

        var second = rankedCandidates[1];
        if (best.MatchedFraction - second.MatchedFraction >= MinimumCoverageAdvantage)
            return true;
        return second.MedianAbsoluteResidualSeconds - best.MedianAbsoluteResidualSeconds >= MinimumResidualAdvantageSeconds;
    }

    private static IReadOnlyList<DuplicateBehavioralEvidenceGroup> FindDuplicateBehavioralEvidence(
        IReadOnlyList<BehavioralPairingEvidence> sources) =>
        sources
            .GroupBy(source => BehavioralSignature(source.Events), StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => new DuplicateBehavioralEvidenceGroup(
                group.Select(item => item.SourcePath).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray()))
            .ToArray();

    private static string BehavioralSignature(IReadOnlyList<BehavioralEvent> events) =>
        string.Join('|', events.Select(item => string.Join(';',
            item.EventNumber.ToString(CultureInfo.InvariantCulture),
            item.Side.ToString(CultureInfo.InvariantCulture),
            item.Stimulus.ToString(CultureInfo.InvariantCulture),
            item.LeverLatencySeconds.ToString("R", CultureInfo.InvariantCulture),
            item.AbsoluteTimeSeconds.ToString("R", CultureInfo.InvariantCulture),
            item.LeftLeverPresses.ToString(CultureInfo.InvariantCulture),
            item.RightLeverPresses.ToString(CultureInfo.InvariantCulture),
            item.CrossingLatencySeconds.ToString("R", CultureInfo.InvariantCulture),
            ((int)item.EventType).ToString(CultureInfo.InvariantCulture))));

    private static void ValidateUniquePaths(IEnumerable<string> paths, string parameterName)
    {
        var raw = paths.ToArray();
        if (raw.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Las rutas no pueden estar vacías.", parameterName);

        var canonical = raw.Select(Path.GetFullPath).ToArray();
        if (canonical.Distinct(StringComparer.OrdinalIgnoreCase).Count() != canonical.Length)
            throw new ArgumentException("La colección contiene rutas duplicadas.", parameterName);
    }

    private static bool SameStem(string left, string right) =>
        string.Equals(
            Path.GetFileNameWithoutExtension(left),
            Path.GetFileNameWithoutExtension(right),
            StringComparison.OrdinalIgnoreCase);

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private static double Median(IReadOnlyList<double> orderedValues)
    {
        var middle = orderedValues.Count / 2;
        return orderedValues.Count % 2 == 0
            ? (orderedValues[middle - 1] + orderedValues[middle]) / 2d
            : orderedValues[middle];
    }
}

public sealed record VideoPairingEvidence(
    string VideoPath,
    IReadOnlyList<LightEventInterval> VisualIntervals,
    LightTimelineScanRange ScanRange,
    double FramesPerSecond,
    LightTimelineScanResult? ScanResult = null);

public sealed record BehavioralPairingEvidence(
    string SourcePath,
    IReadOnlyList<BehavioralEvent> Events);

public sealed record SessionPairingCandidate(
    string VideoPath,
    string BehavioralSourcePath,
    BehavioralVideoSynchronizationResult Synchronization,
    double MatchedFraction,
    double MedianAbsoluteResidualSeconds,
    double MaximumAbsoluteResidualSeconds,
    bool IsExactStemMatch,
    bool IsViable);

public enum SessionPairingStatus
{
    Confirmed,
    Ambiguous,
    NoMatch,
}

public sealed record SessionPairingResolution(
    string VideoPath,
    SessionPairingStatus Status,
    string? BehavioralSourcePath,
    SessionPairingCandidate? SelectedCandidate,
    IReadOnlyList<SessionPairingCandidate> Alternatives,
    string Message);

public sealed record DuplicateBehavioralEvidenceGroup(
    IReadOnlyList<string> SourcePaths);

public sealed record SessionPairingReport(
    IReadOnlyList<SessionPairingResolution> Resolutions,
    IReadOnlyList<string> UnassignedBehavioralSources,
    IReadOnlyList<DuplicateBehavioralEvidenceGroup> DuplicateBehavioralEvidence,
    IReadOnlyList<SessionPairingCandidate> Candidates)
{
    public int ConfirmedCount => Resolutions.Count(item => item.Status == SessionPairingStatus.Confirmed);
    public int AmbiguousCount => Resolutions.Count(item => item.Status == SessionPairingStatus.Ambiguous);
    public int UnmatchedVideoCount => Resolutions.Count(item => item.Status == SessionPairingStatus.NoMatch);
}
