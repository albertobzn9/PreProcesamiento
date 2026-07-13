using OpenCvSharp;
using VideoBatchProcessor.Core.FrameAnalyzer;
using FrameBrightnessAnalyzer = VideoBatchProcessor.Core.FrameAnalyzer.FrameAnalyzer;

namespace VideoBatchProcessor.Core.LightDetection;

/// <summary>
/// Adaptador puntual entre un frame de OpenCV y <see cref="LightDetector"/>.
/// Mide las tres ROIs una sola vez al crearse y no conserva ni modifica el
/// <see cref="Mat"/> recibido.
/// </summary>
public sealed class FrameAnalyzerBrightnessSource : IFrameBrightnessSource
{
    private readonly IReadOnlyDictionary<LightId, double> _brightnessByLight;

    public FrameAnalyzerBrightnessSource(
        Mat frame,
        LightDetectionConfig config,
        FrameBrightnessAnalyzer? analyzer = null)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(config);

        var definitions = new[]
        {
            ToRoiDefinition(config.FoodLeft, TipoLed.Izquierda, "FoodLeft"),
            ToRoiDefinition(config.FoodRight, TipoLed.Derecha, "FoodRight"),
            ToRoiDefinition(config.NoiseLed, TipoLed.Ruido, "NoiseLed"),
        };

        var analysis = (analyzer ?? new FrameBrightnessAnalyzer()).Analyze(
            frame,
            definitions,
            frameIndex: 0,
            timestamp: TimeSpan.Zero);

        _brightnessByLight = analysis.Rois.ToDictionary(
            result => ToLightId(result.Definition.Tipo),
            result => result.MeanBrightness);
    }

    public double GetMeanBrightness(LightRoi roi)
    {
        ArgumentNullException.ThrowIfNull(roi);

        return _brightnessByLight.TryGetValue(roi.Light, out var brightness)
            ? brightness
            : throw new ArgumentException("La ROI no pertenece a esta fuente de brillo.", nameof(roi));
    }

    private static RoiDefinition ToRoiDefinition(LightRoi roi, TipoLed tipo, string label) => new()
    {
        Tipo = tipo,
        Label = label,
        Region = new Rect(roi.X, roi.Y, roi.Width, roi.Height),
        Shape = roi.Shape,
    };

    private static LightId ToLightId(TipoLed tipo) => tipo switch
    {
        TipoLed.Izquierda => LightId.FoodLeft,
        TipoLed.Derecha => LightId.FoodRight,
        TipoLed.Ruido => LightId.NoiseLed,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de luz no reconocido."),
    };
}
