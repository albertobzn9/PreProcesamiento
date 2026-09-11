using VideoBatchProcessor.Core.VideoTransform;

namespace VideoBatchProcessor.Core.LightDetection;

/// <summary>
/// Convierte las ROIs marcadas sobre el video preparado a coordenadas del
/// video fuente. Esto permite medir solo las regiones pequeñas sin transformar
/// el frame completo durante el escaneo.
/// </summary>
public static class LightDetectionCoordinateMapper
{
    public static bool TryMapPreparedToSource(
        LightDetectionConfig prepared,
        VideoTransformConfig transform,
        int sourceWidth,
        int sourceHeight,
        out LightDetectionConfig? source,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(transform);
        source = null;
        error = null;

        if (!TryMap(prepared.FoodLeft, transform, sourceWidth, sourceHeight, out var foodLeft, out error) ||
            !TryMap(prepared.FoodRight, transform, sourceWidth, sourceHeight, out var foodRight, out error) ||
            !TryMap(prepared.NoiseLed, transform, sourceWidth, sourceHeight, out var noiseLed, out error))
        {
            return false;
        }

        source = new LightDetectionConfig(foodLeft!, foodRight!, noiseLed!);
        return true;
    }

    private static bool TryMap(
        LightRoi roi,
        VideoTransformConfig transform,
        int sourceWidth,
        int sourceHeight,
        out LightRoi? mappedRoi,
        out string? error)
    {
        mappedRoi = null;
        if (!VideoCoordinateMapper.TryMapPreparedToSource(
                new VideoCropRect(roi.X, roi.Y, roi.Width, roi.Height),
                transform,
                sourceWidth,
                sourceHeight,
                out var mapped,
                out error))
        {
            return false;
        }

        mappedRoi = new LightRoi(
            roi.Light,
            mapped!.X,
            mapped.Y,
            mapped.Width,
            mapped.Height,
            roi.Threshold,
            roi.Shape);
        return true;
    }
}
