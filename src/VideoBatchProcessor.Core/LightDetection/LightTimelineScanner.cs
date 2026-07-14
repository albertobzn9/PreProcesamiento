using VideoBatchProcessor.Core.VideoReader;
using VideoBatchProcessor.Core.VideoTransform;
using VideoFileReader = VideoBatchProcessor.Core.VideoReader.VideoReader;

namespace VideoBatchProcessor.Core.LightDetection;

/// <summary>
/// Recorre un video preparado, mide las ROIs reales de cada frame y construye
/// su <see cref="LightTimeline"/>. Las transformaciones se aplican antes de
/// medir, igual que durante la calibración de la interfaz.
/// </summary>
public sealed class LightTimelineScanner
{
    public LightTimelineScanResult Scan(
        string videoPath,
        VideoTransformConfig transformConfig,
        LightDetectionConfig lightConfig,
        LightTimelineConfig? timelineConfig = null,
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

            var samples = new List<LightSample>();
            var detector = new LightDetector(lightConfig);

            while (reader.MoveNext(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (reader.CurrentFrameIndex > int.MaxValue)
                    throw new InvalidOperationException("El video tiene más frames de los que el modelo actual puede representar.");

                if (!VideoTransformPreviewRenderer.TryTransformFrame(
                        reader.Current,
                        reader.Metadata,
                        transformConfig,
                        out var prepared,
                        out error))
                {
                    throw new InvalidOperationException(error ?? "No se pudo preparar un frame para medir las luces.");
                }

                using (prepared)
                {
                    var brightness = new FrameAnalyzerBrightnessSource(prepared!, lightConfig);
                    samples.Add(detector.Analyze(
                        brightness,
                        (int)reader.CurrentFrameIndex,
                        reader.CurrentTimestamp.TotalSeconds));
                }
            }

            var timeline = new LightTimelineBuilder(timelineConfig).Build(samples);
            return new LightTimelineScanResult(reader.Metadata, timeline);
        }
    }
}
