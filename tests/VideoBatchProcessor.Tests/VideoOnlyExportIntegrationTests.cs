using OpenCvSharp;
using VideoBatchProcessor.Core.BehavioralData;
using VideoBatchProcessor.Core.BatchProcessing;
using VideoBatchProcessor.Core.ClipExport;
using VideoBatchProcessor.Core.FrameAnalyzer;
using VideoBatchProcessor.Core.LightDetection;
using VideoBatchProcessor.Core.SessionResolver;

namespace VideoBatchProcessor.Tests;

public sealed class VideoOnlyExportIntegrationTests
{
    [FfmpegFact]
    public Task VideoWithoutTable_ScansAndExportsPlayableClips() => ScanAndExport(false);

    [FfmpegFact]
    public Task PastedTable_SynchronizesAndExportsPlayableClips() => ScanAndExport(true);

    private static async Task ScanAndExport(bool pastedTable)
    {
        var directory = Directory.CreateTempSubdirectory("vbp-real-export-").FullName;
        try
        {
            var path = Path.Combine(directory, "exp_0526_cs_d1r1.avi");
            using (var writer = new VideoWriter(path, FourCC.MJPG, 30, new Size(160, 100)))
            {
                Assert.True(writer.IsOpened());
                for (var index = 0; index < (pastedTable ? 240 : 180); index++)
                {
                    using var frame = new Mat(100, 160, MatType.CV_8UC3, Scalar.Black);
                    if (index is >= 30 and < 60) Cv2.Circle(frame, new Point(25, 25), 12, Scalar.White, -1);
                    if (index is >= 90 and < 120) Cv2.Circle(frame, new Point(135, 25), 12, Scalar.White, -1);
                    if (pastedTable && index is >= 150 and < 180) Cv2.Circle(frame, new Point(25, 25), 12, Scalar.White, -1);
                    writer.Write(frame);
                }
            }
            var config = new LightDetectionConfig(
                new LightRoi(LightId.FoodLeft, 13, 13, 24, 24, 120, RoiShape.Circle),
                new LightRoi(LightId.FoodRight, 123, 13, 24, 24, 120, RoiShape.Circle),
                new LightRoi(LightId.NoiseLed, 68, 13, 24, 24, double.MaxValue, RoiShape.Circle));
            var request = new BatchProcessingRequest(directory, directory, new(), config,
                new BatchManifest { Iniciales = "abs", Sexo = "m", Tratamiento = "stx" },
                new ClipExportOptions { FfmpegPath = Environment.GetEnvironmentVariable("VBP_TEST_FFMPEG")! },
                BatchExportMode.AllSegments, [path], VideoOnlySourcePaths: [path]);
            if (pastedTable)
            {
                var source = PastedBehavioralTable.Save(Path.Combine(directory, "tables"), path,
                    "1\t1\t0\t1\t2\t1\t0\t2\n2\t0\t0\t1\t4\t1\t1\t2\n3\t1\t0\t1\t6\t2\t1\t2", false);
                request = request with { VideoOnlySourcePaths = null, Manifest = request.Manifest with
                {
                    Overrides = new Dictionary<string, FileOverride>
                    {
                        [Path.GetFileNameWithoutExtension(path)] = new() { BehavioralSourcePath = source },
                    },
                } };
            }
            var progress = new List<BatchProcessingProgress>();
            var session = Assert.Single((await new BatchOrchestrator().RunAsync(request, new CaptureProgress(progress))).Sessions);
            Assert.True(session.Status is BatchSessionStatus.Exported or BatchSessionStatus.ExportedWithWarnings, session.Message);
            Assert.Equal(pastedTable ? 7 : 5, session.Clips.Count);
            if (pastedTable) Assert.Equal(3, session.Synchronization!.Comparison.MatchedCount);
            Assert.All(session.Clips, clip => Assert.Equal(!pastedTable, clip.Segment.IsVideoOnly));
            Assert.Contains(progress, p => p.Message.Contains("frames"));
            Assert.Contains(progress, p => p.Message.Contains("Exportando clip"));
            Assert.True(progress[^1].IsComplete);
            foreach (var clip in session.Clips)
            {
                Assert.True(clip.Export.Succeeded, clip.Export.ErrorMessage);
                using var video = new VideoCapture(clip.Export.OutputPath);
                Assert.True(video.IsOpened());
                Assert.Equal(clip.Export.EndFrameIndex - clip.Export.StartFrameIndex + 1, (int)video.FrameCount);
                using var first = new Mat();
                Assert.True(video.Read(first));
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class CaptureProgress(List<BatchProcessingProgress> updates) : IProgress<BatchProcessingProgress>
    {
        public void Report(BatchProcessingProgress value) => updates.Add(value);
    }
}

public sealed class FfmpegFactAttribute : FactAttribute
{
    public FfmpegFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VBP_TEST_FFMPEG")))
            Skip = "Set VBP_TEST_FFMPEG to run the real video scan and FFmpeg export test.";
    }
}
