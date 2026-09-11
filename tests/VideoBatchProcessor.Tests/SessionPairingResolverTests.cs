using VideoBatchProcessor.Core.BehavioralData;
using VideoBatchProcessor.Core.LightDetection;
using VideoBatchProcessor.Core.SessionPairing;

namespace VideoBatchProcessor.Tests;

public sealed class SessionPairingResolverTests
{
    [Fact]
    public void Resolve_ConfirmaDosParejasPorEvidenciaYNoSoloPorNombre()
    {
        var videos = new[]
        {
            Video("/tmp/exp_0526_cp_d1r1.mp4", [100, 120, 140, 160], [0, 1, 0, 1]),
            Video("/tmp/nombre_equivocado.mp4", [100, 133, 171, 212], [1, 0, 1, 0]),
        };
        var sources = new[]
        {
            Behavioral("/tmp/exp_0526_cp_d1r1.mat", [100, 120, 140, 160], [0, 1, 0, 1]),
            Behavioral("/tmp/exp_0526_cp_d1r2.mat", [100, 133, 171, 212], [1, 0, 1, 0]),
        };

        var report = new SessionPairingResolver().Resolve(videos, sources);

        Assert.Equal(2, report.ConfirmedCount);
        Assert.Equal(
            "/tmp/exp_0526_cp_d1r1.mat",
            report.Resolutions.Single(item => item.VideoPath.EndsWith("d1r1.mp4")).BehavioralSourcePath);
        Assert.Equal(
            "/tmp/exp_0526_cp_d1r2.mat",
            report.Resolutions.Single(item => item.VideoPath.EndsWith("nombre_equivocado.mp4")).BehavioralSourcePath);
    }

    [Fact]
    public void Resolve_NoDecideCuandoDosMatTienenLaMismaEvidencia()
    {
        var video = Video("/tmp/sesion_sin_nombre.mp4", [100, 120, 140, 160], [0, 1, 0, 1]);
        var events = Events([100, 120, 140, 160], [0, 1, 0, 1]);
        var sources = new[]
        {
            new BehavioralPairingEvidence("/tmp/candidato_a.mat", events),
            new BehavioralPairingEvidence("/tmp/candidato_b.mat", events),
        };

        var report = new SessionPairingResolver().Resolve([video], sources);

        var resolution = Assert.Single(report.Resolutions);
        Assert.Equal(SessionPairingStatus.Ambiguous, resolution.Status);
        Assert.Null(resolution.BehavioralSourcePath);
        Assert.Equal(2, resolution.Alternatives.Count);
        Assert.Single(report.DuplicateBehavioralEvidence);
    }

    [Fact]
    public void Resolve_ElNombreIdenticoNoRompeUnEmpateDeContenido()
    {
        var video = Video("/tmp/exp_0526_cp_d1r1.mp4", [100, 120, 140, 160], [0, 1, 0, 1]);
        var events = Events([100, 120, 140, 160], [0, 1, 0, 1]);
        var sources = new[]
        {
            new BehavioralPairingEvidence("/tmp/exp_0526_cp_d1r1.mat", events),
            new BehavioralPairingEvidence("/tmp/exp_0526_cp_d1r2.mat", events),
        };

        var resolution = Assert.Single(new SessionPairingResolver().Resolve([video], sources).Resolutions);

        Assert.Equal(SessionPairingStatus.Ambiguous, resolution.Status);
        Assert.Null(resolution.BehavioralSourcePath);
    }

    [Fact]
    public void Resolve_NoAsignaUnaCoincidenciaDebil()
    {
        var video = Video("/tmp/video.mp4", [100, 120], [0, 1]);
        var source = Behavioral("/tmp/datos.mat", [100, 120, 140, 160], [0, 1, 0, 1]);

        var report = new SessionPairingResolver().Resolve([video], [source]);

        var resolution = Assert.Single(report.Resolutions);
        Assert.Equal(SessionPairingStatus.NoMatch, resolution.Status);
        Assert.Null(resolution.BehavioralSourcePath);
        Assert.Contains(source.SourcePath, report.UnassignedBehavioralSources);
    }

    [Fact]
    public void Resolve_PuedeEmpatarTimeoutsSinInventarSuLado()
    {
        var video = Video("/tmp/exp_0526_cp_d5r2.mp4", [100, 220, 240, 360], [0, 1, 1, 0]);
        var source = Behavioral("/tmp/exp_0526_cp_d5r2.mat", [100, 220, 240, 360], [0, -2, 1, -2]);

        var report = new SessionPairingResolver().Resolve([video], [source]);

        var resolution = Assert.Single(report.Resolutions);
        Assert.Equal(SessionPairingStatus.Confirmed, resolution.Status);
        Assert.Equal(4, resolution.SelectedCandidate!.Synchronization.Comparison.MatchedCount);
    }

    private static VideoPairingEvidence Video(
        string path,
        IReadOnlyList<double> starts,
        IReadOnlyList<int> sides)
    {
        var intervals = starts.Select((start, index) => new LightEventInterval(
            index + 1,
            sides[index] == 1 ? LightId.FoodLeft : LightId.FoodRight,
            (int)((start + 2.4) * 30),
            start + 2.4,
            (int)((start + 7.4) * 30),
            start + 7.4)).ToArray();
        return new VideoPairingEvidence(path, intervals, new LightTimelineScanRange(0, 20_000), 30);
    }

    private static BehavioralPairingEvidence Behavioral(
        string path,
        IReadOnlyList<double> starts,
        IReadOnlyList<int> sides) =>
        new(path, Events(starts, sides));

    private static IReadOnlyList<BehavioralEvent> Events(
        IReadOnlyList<double> starts,
        IReadOnlyList<int> sides) =>
        starts.Select((start, index) => new BehavioralEvent(
            index + 1,
            sides[index],
            1,
            5,
            start + 5,
            0,
            0,
            sides[index] == -2 ? 120 : 0.2,
            BehavioralEventType.ConflictWithFood,
            [])).ToArray();
}
