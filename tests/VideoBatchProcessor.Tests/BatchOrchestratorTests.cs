using VideoBatchProcessor.Core.BatchProcessing;
using VideoBatchProcessor.Core.ClipExport;
using VideoBatchProcessor.Core.SegmentPlanning;

namespace VideoBatchProcessor.Tests;

public sealed class BatchOrchestratorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"vbp-batch-{Guid.NewGuid():N}");

    [Fact]
    public void DiscoverCandidates_RecorreSubcarpetasYSeleccionaSoloSesionesCsFuente()
    {
        var nested = Path.Combine(_directory, "entrenamiento-a");
        Directory.CreateDirectory(nested);
        Create(nested, "exp_0126_cs_d1r1.mp4");
        Create(nested, "abs_2601_f2_d1r2_m_stx.mkv");
        Create(nested, "exp_0126_cp_d1r3.mp4");
        Create(nested, "abs_2601_f2_d1r2_m_e1_s_na_stx.mp4");
        Create(nested, "prueba.mp4");

        var candidates = new BatchOrchestrator().DiscoverCandidates(_directory);

        Assert.Equal(5, candidates.Count);
        Assert.Equal(2, candidates.Count(item => item.IsCsSource));
        Assert.Contains(candidates, item => item.VideoPath.EndsWith("exp_0126_cs_d1r1.mp4", StringComparison.Ordinal) && item.IsCsSource);
        Assert.Contains(candidates, item => item.VideoPath.EndsWith("abs_2601_f2_d1r2_m_stx.mkv", StringComparison.Ordinal) && item.IsCsSource);
        Assert.All(candidates.Where(item => !item.IsCsSource), item => Assert.False(string.IsNullOrWhiteSpace(item.SkipReason)));
    }

    [Fact]
    public void DiscoverCandidates_CarpetaInexistenteDevuelveListaVacia()
    {
        var candidates = new BatchOrchestrator().DiscoverCandidates(Path.Combine(_directory, "no-existe"));

        Assert.Empty(candidates);
    }

    [Fact]
    public void BatchClipManifestWriter_RegistraTiemposDelVideoOriginal()
    {
        var path = Path.Combine(_directory, "clips_exportados.csv");
        var segment = new PlannedVideoSegment(
            PlannedSegmentKind.Event, 2, 90, 149, 3, 149d / 30d,
            PlannedTrialType.SafeFood, PlannedBehavioralResult.Crossing, 2,
            1, 0, 1.2, 2.3, null, 90, 149, null, null, null, null);
        var clip = new BatchClipReport(segment, new ClipExportResult(
            true, "/tmp/abs_2605_f2_d4r1_m_e2_s_cr_stx.mp4", 3, 5, null));

        BatchClipManifestWriter.Write(path, [clip]);

        var lines = File.ReadAllLines(path);
        Assert.Equal(2, lines.Length);
        Assert.Contains("00:00:03.000", lines[1]);
        Assert.Contains("00:00:05.000", lines[1]);
        Assert.Contains("90,149", lines[1]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private static void Create(string directory, string fileName) =>
        File.WriteAllBytes(Path.Combine(directory, fileName), []);
}
