using VideoBatchProcessor.Core.VideoReader;
using VideoBatchProcessor.Core.VideoTransform;
using VideoFileReader = VideoBatchProcessor.Core.VideoReader.VideoReader;

namespace VideoBatchProcessor.Core.LightDetection;

/// <summary>
/// Recorre un video, mide las ROIs reales de cada frame y construye su
/// <see cref="LightTimeline"/>. Las ROIs del video preparado se convierten una
/// vez a coordenadas fuente para evitar transformar el frame completo.
/// </summary>
public sealed class LightTimelineScanner
{
    public LightTimelineScanResult Scan(
        string videoPath,
        VideoTransformConfig transformConfig,
        LightDetectionConfig lightConfig,
        LightTimelineConfig? timelineConfig = null,
        LightTimelineScanRange? scanRange = null,
        IProgress<LightTimelineScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoPath);
        ArgumentNullException.ThrowIfNull(transformConfig);
        ArgumentNullException.ThrowIfNull(lightConfig);

        if (!VideoFileReader.TryOpen(videoPath, out var reader, out var error))
            throw new InvalidOperationException(error ?? "No se pudo abrir el video para analizar luces.");

        using (reader)
        {
            if (!transformConfig.TryValidateFor(reader!.Metadata.Width, reader.Metadata.Height, out error))
                throw new ArgumentException(error, nameof(transformConfig));
            if (reader.Metadata.TotalFrames > int.MaxValue)
                throw new InvalidOperationException("El video tiene más frames de los que el modelo actual puede representar.");

            if (!LightDetectionCoordinateMapper.TryMapPreparedToSource(
                    lightConfig,
                    transformConfig,
                    reader.Metadata.Width,
                    reader.Metadata.Height,
                    out var sourceLightConfig,
                    out error))
            {
                throw new ArgumentException(error, nameof(lightConfig));
            }

            var samples = new List<LightSample>();
            var detector = new LightDetector(sourceLightConfig!);
            var videoFrames = (int)reader.Metadata.TotalFrames;
            var effectiveRange = scanRange ?? new LightTimelineScanRange(0, videoFrames - 1);
            effectiveRange.Validate(videoFrames);
            var totalFrames = effectiveRange.FrameCount;
            var lastReportedPercent = -1;
            using var brightness = new ReusableFrameBrightnessSource(sourceLightConfig!);

            ReportProgress(0);

            using var firstRangeFrame = effectiveRange.StartFrame == 0
                ? null
                : reader.ReadFrameAt(effectiveRange.StartFrame, cancellationToken);
            var hasFrame = firstRangeFrame is not null || reader.MoveNext(cancellationToken);
            while (hasFrame && reader.CurrentFrameIndex <= effectiveRange.EndFrame)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourceFrame = samples.Count == 0 && firstRangeFrame is not null
                    ? firstRangeFrame
                    : reader.Current;
                brightness.Update(sourceFrame);
                samples.Add(detector.Analyze(
                    brightness,
                    (int)reader.CurrentFrameIndex,
                    reader.CurrentTimestamp.TotalSeconds));

                ReportProgress(samples.Count);
                hasFrame = reader.MoveNext(cancellationToken);
            }

            var timeline = new LightTimelineBuilder(timelineConfig).Build(samples);
            return new LightTimelineScanResult(reader.Metadata, timeline);

            void ReportProgress(int framesProcessed)
            {
                var update = new LightTimelineScanProgress(framesProcessed, totalFrames);
                if (update.Percent <= lastReportedPercent && framesProcessed != totalFrames)
                    return;

                lastReportedPercent = update.Percent;
                progress?.Report(update);
            }
        }
    }
}
