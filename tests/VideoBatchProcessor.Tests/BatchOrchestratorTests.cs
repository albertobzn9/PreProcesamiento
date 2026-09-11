using VideoBatchProcessor.Core.BatchProcessing;
using VideoBatchProcessor.Core.ClipExport;
using VideoBatchProcessor.Core.Nomenclature;
using VideoBatchProcessor.Core.SegmentPlanning;

namespace VideoBatchProcessor.Tests;

public sealed class BatchOrchestratorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"vbp-batch-{Guid.NewGuid():N}");

    [Fact]
    public void DiscoverCandidates_RecorreSubcarpetasYSeleccionaSesionesCsYCpFuente()
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
        Assert.Equal(3, candidates.Count(item => item.IsSupportedSource));
        Assert.Equal(2, candidates.Count(item => item.IsCsSource));
        Assert.Single(candidates, item => item.IsCpSource);
        Assert.Contains(candidates, item => item.VideoPath.EndsWith("exp_0126_cs_d1r1.mp4", StringComparison.Ordinal) && item.IsCsSource);
        Assert.Contains(candidates, item => item.VideoPath.EndsWith("abs_2601_f2_d1r2_m_stx.mkv", StringComparison.Ordinal) && item.IsCsSource);
        Assert.All(candidates.Where(item => !item.IsSupportedSource), item => Assert.False(string.IsNullOrWhiteSpace(item.SkipReason)));
    }

    [Fact]
    public void DiscoverCandidates_CarpetaInexistenteDevuelveListaVacia()
    {
        var candidates = new BatchOrchestrator().DiscoverCandidates(Path.Combine(_directory, "no-existe"));

        Assert.Empty(candidates);
    }

    [Fact]
    public void DiscoverCandidates_SeleccionExplicitaProcesaUnSoloVideo()
    {
        Directory.CreateDirectory(_directory);
        var video = Path.Combine(_directory, "exp_0126_cs_d1r1.mkv");
        File.WriteAllBytes(video, []);

        var candidates = new BatchOrchestrator().DiscoverCandidates([video]);

        var candidate = Assert.Single(candidates);
        Assert.Equal(video, candidate.VideoPath);
        Assert.True(candidate.IsCsSource);
    }

    [Fact]
    public void DiscoverCandidates_ConservaNombreLegacyIntercambiadoParaEmpatarPorContenido()
    {
        Directory.CreateDirectory(_directory);
        var video = Path.Combine(_directory, "exp_0526_cp_r1d5.mp4");
        File.WriteAllBytes(video, []);

        var candidate = Assert.Single(new BatchOrchestrator().DiscoverCandidates([video]));

        Assert.True(candidate.IsSupportedSource);
        Assert.False(candidate.IsCsSource);
        Assert.False(candidate.IsCpSource);
        Assert.Equal(NamingScheme.Unknown, candidate.ParsedName!.Scheme);
    }

    [Fact]
    public void FindExistingOutputs_DetectaLaCarpetaJuntoAlVideoAntesDeProcesar()
    {
        Directory.CreateDirectory(_directory);
        var video = Path.Combine(_directory, "exp_0126_cs_d1r1.mp4");
        File.WriteAllBytes(video, []);
        var outputFolder = Path.Combine(_directory, "exp_0126_cs_d1r1");
        Directory.CreateDirectory(outputFolder);

        var existing = new BatchOrchestrator().FindExistingOutputs([video]);

        var item = Assert.Single(existing);
        Assert.Equal(video, item.VideoPath);
        Assert.Equal(outputFolder, item.OutputDirectory);
    }

    [Fact]
    public void OutputSegmentCodePlanner_EnumeraCrucesYNoCrucesPorSeparado()
    {
        var first = Segment(1, PlannedBehavioralResult.NotApplicable, 1);
        var noCrossingOne = Segment(2, PlannedBehavioralResult.NoCrossing, 2);
        var crossingOne = Segment(3, PlannedBehavioralResult.Crossing, 3);
        var noCrossingTwo = Segment(4, PlannedBehavioralResult.NoCrossing, 4);
        var crossingTwo = Segment(5, PlannedBehavioralResult.Crossing, 5);

        var codes = OutputSegmentCodePlanner.Create([
            crossingTwo, noCrossingTwo, first, crossingOne, noCrossingOne,
        ]);

        Assert.Equal("e1", codes[first]);
        Assert.Equal("nc1", codes[noCrossingOne]);
        Assert.Equal("cr1", codes[crossingOne]);
        Assert.Equal("nc2", codes[noCrossingTwo]);
        Assert.Equal("cr2", codes[crossingTwo]);
    }

    [Fact]
    public void SelectSegmentsForExport_MuestraCincoClipsSinHabituacion()
    {
        var initialHabituation = Segment(1, PlannedBehavioralResult.NotApplicable, 0) with
        {
            Kind = PlannedSegmentKind.InitialHabituation,
        };
        var eventOne = Segment(2, PlannedBehavioralResult.NotApplicable, 1);
        var itiOne = eventOne with { Kind = PlannedSegmentKind.InterTrialInterval, Sequence = 3 };
        var eventTwo = Segment(4, PlannedBehavioralResult.Crossing, 2);
        var itiTwo = eventTwo with { Kind = PlannedSegmentKind.InterTrialInterval, Sequence = 5 };
        var eventThree = Segment(6, PlannedBehavioralResult.NoCrossing, 3);
        var finalHabituation = Segment(7, PlannedBehavioralResult.NotApplicable, 0) with
        {
            Kind = PlannedSegmentKind.FinalHabituation,
        };

        var selected = BatchOrchestrator.SelectSegmentsForExport(
            [initialHabituation, eventOne, itiOne, eventTwo, itiTwo, eventThree, finalHabituation],
            BatchExportMode.ValidationSample);

        Assert.Equal(5, selected.Count);
        Assert.Equal([2, 3, 4, 5, 6], selected.Select(item => item.Sequence));
        Assert.DoesNotContain(selected, item => item.Kind is PlannedSegmentKind.InitialHabituation or PlannedSegmentKind.FinalHabituation);
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
            true, "/tmp/abs_2605_f2_d4r1_m_e2_s_cr_stx.mp4", 3, 5, null, 85, 154));

        BatchClipManifestWriter.Write(path, [clip]);

        var lines = File.ReadAllLines(path);
        Assert.Equal(2, lines.Length);
        Assert.Contains("00:00:03.000", lines[1]);
        Assert.Contains("00:00:05.000", lines[1]);
        Assert.Contains("90,149,\"00:00:03.000\",\"00:00:04.966\",85,154", lines[1]);
    }

    [Fact]
    public void BatchProcessingProgress_TrabajoActivoNuncaMuestraCien()
    {
        var progress = new BatchProcessingProgress(1, 1, "/tmp/video.mp4", "Guardando índice", 100);

        Assert.False(progress.IsComplete);
        Assert.Equal(99, progress.Percent);
    }

    [Fact]
    public void BatchProcessingProgress_SoloElFinalMuestraCien()
    {
        var progress = new BatchProcessingProgress(1, 1, null, "Lote terminado", 100, IsComplete: true);

        Assert.True(progress.IsComplete);
        Assert.Equal(100, progress.Percent);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private static void Create(string directory, string fileName) =>
        File.WriteAllBytes(Path.Combine(directory, fileName), []);

    private static PlannedVideoSegment Segment(
        int sequence,
        PlannedBehavioralResult result,
        int behavioralEventNumber) =>
        new(
            PlannedSegmentKind.Event,
            sequence,
            sequence * 10,
            sequence * 10 + 9,
            sequence,
            sequence + 0.3,
            PlannedTrialType.SafeFood,
            result,
            behavioralEventNumber,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
}
