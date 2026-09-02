using VideoBatchProcessor.Core.BatchProcessing;

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

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private static void Create(string directory, string fileName) =>
        File.WriteAllBytes(Path.Combine(directory, fileName), []);
}
