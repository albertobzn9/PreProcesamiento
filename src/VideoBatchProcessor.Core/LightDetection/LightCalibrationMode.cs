namespace VideoBatchProcessor.Core.LightDetection;

/// <summary>
/// Señales que el protocolo espera encontrar durante una calibración. Cruces
/// Seguros no incluye LED de ruido; CP y DIS sí lo usan como señal relevante.
/// </summary>
public enum LightCalibrationMode
{
    FoodOnly,
    FoodAndNoise,
}
