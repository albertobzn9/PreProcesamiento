using OpenCvSharp;

namespace VideoBatchProcessor.Core.FrameAnalyzer;

/// <summary>
/// Analiza regiones de interés (ROIs) sobre frames de video para extraer
/// métricas de brillo que serán consumidas por LightDetector.
/// <para>
/// Patrón de uso:
/// <code>
/// var analyzer = new FrameAnalyzer();
/// var rois = new[] {
///     new RoiDefinition { Tipo = TipoLed.Izquierda, Label = "LED izq", Region = new Rect(10,10,30,30) },
///     new RoiDefinition { Tipo = TipoLed.Derecha,   Label = "LED der", Region = new Rect(280,10,30,30) },
///     new RoiDefinition { Tipo = TipoLed.Ruido,     Label = "LED ruido", Region = new Rect(145,10,30,30) },
/// };
///
/// // Dentro del loop de lectura del VideoReader:
/// var result = analyzer.Analyze(frame, rois, frameIndex, timestamp);
/// foreach (var roi in result.Rois)
///     Console.WriteLine($"{roi.Definition.Label}: brillo={roi.MeanBrightness:F1}");
/// </code>
/// </para>
/// Thread-safe: no mantiene estado interno entre llamadas.
/// </summary>
public sealed class FrameAnalyzer
{
    /// <summary>
    /// Analiza todos los ROIs sobre un frame.
    /// </summary>
    /// <param name="frame">Frame del video (BGR, no se modifica).</param>
    /// <param name="rois">Definiciones de ROIs a analizar.</param>
    /// <param name="frameIndex">Índice del frame (para trazabilidad).</param>
    /// <param name="timestamp">Timestamp del frame (para trazabilidad).</param>
    /// <param name="includeCrop">
    /// Si true, cada resultado incluye un crop del ROI como Mat independiente.
    /// El caller debe hacer Dispose de cada crop cuando ya no lo necesite.
    /// Si false, Crop queda null (más rápido para procesamiento masivo).
    /// </param>
    /// <returns>Resultado con métricas por ROI.</returns>
    public FrameAnalysisResult Analyze(
        Mat                        frame,
        IReadOnlyList<RoiDefinition> rois,
        long                       frameIndex,
        TimeSpan                   timestamp,
        bool                       includeCrop = false)
    {
        if (frame is null || frame.Empty())
            throw new ArgumentException("El frame está vacío o es nulo.", nameof(frame));

        if (rois is null || rois.Count == 0)
            throw new ArgumentException("Debe haber al menos un ROI.", nameof(rois));

        var results = new List<RoiAnalysisResult>(rois.Count);

        foreach (var roi in rois)
        {
            var clipped = ClipToFrame(roi.Region, frame.Width, frame.Height);

            if (clipped.Width <= 0 || clipped.Height <= 0)
            {
                results.Add(new RoiAnalysisResult
                {
                    Definition     = roi,
                    MeanBrightness = 0,
                    Crop           = null,
                });
                continue;
            }

            var subMat = frame[clipped];
            var brightness = ComputeMeanBrightness(subMat);

            Mat? crop = null;
            if (includeCrop)
                crop = subMat.Clone();

            results.Add(new RoiAnalysisResult
            {
                Definition     = roi,
                MeanBrightness = brightness,
                Crop           = crop,
            });
        }

        return new FrameAnalysisResult
        {
            FrameIndex = frameIndex,
            Timestamp  = timestamp,
            Rois       = results,
        };
    }

    /// <summary>
    /// Analiza un solo ROI sobre un frame. Conveniente cuando solo necesitas
    /// una métrica rápida sin construir la lista de definiciones.
    /// </summary>
    public RoiAnalysisResult AnalyzeSingle(
        Mat            frame,
        RoiDefinition  roi,
        bool           includeCrop = false)
    {
        var result = Analyze(frame, [roi], 0, TimeSpan.Zero, includeCrop);
        return result.Rois[0];
    }

    /// <summary>
    /// Valida que todos los ROIs estén dentro de las dimensiones del frame.
    /// Devuelve la lista de ROIs que están total o parcialmente fuera.
    /// Útil para que la UI avise al usuario si un ROI se sale del frame.
    /// </summary>
    public static IReadOnlyList<RoiDefinition> ValidateRois(
        IReadOnlyList<RoiDefinition> rois,
        int frameWidth,
        int frameHeight)
    {
        var invalid = new List<RoiDefinition>();

        foreach (var roi in rois)
        {
            var r = roi.Region;

            if (r.Width <= 0 || r.Height <= 0 ||
                r.X >= frameWidth || r.Y >= frameHeight ||
                r.X + r.Width <= 0 || r.Y + r.Height <= 0)
            {
                invalid.Add(roi);
            }
        }

        return invalid;
    }

    // ── Helpers privados ──────────────────────────────────────────────────

    /// <summary>
    /// Convierte la sub-región a escala de grises y calcula el brillo promedio.
    /// </summary>
    private static double ComputeMeanBrightness(Mat roiMat)
    {
        using var gray = new Mat();

        if (roiMat.Channels() == 1)
            roiMat.CopyTo(gray);
        else
            Cv2.CvtColor(roiMat, gray, ColorConversionCodes.BGR2GRAY);

        var scalar = Cv2.Mean(gray);
        return scalar.Val0;
    }

    /// <summary>
    /// Recorta el rectángulo del ROI para que no se salga de los límites del frame.
    /// Si el ROI está completamente fuera, devuelve un Rect con Width/Height = 0.
    /// </summary>
    private static Rect ClipToFrame(Rect roi, int frameWidth, int frameHeight)
    {
        var x1 = Math.Max(0, roi.X);
        var y1 = Math.Max(0, roi.Y);
        var x2 = Math.Min(frameWidth,  roi.X + roi.Width);
        var y2 = Math.Min(frameHeight, roi.Y + roi.Height);

        if (x2 <= x1 || y2 <= y1)
            return new Rect(0, 0, 0, 0);

        return new Rect(x1, y1, x2 - x1, y2 - y1);
    }
}