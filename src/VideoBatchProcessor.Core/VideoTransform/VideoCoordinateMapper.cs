namespace VideoBatchProcessor.Core.VideoTransform;

/// <summary>
/// Convierte regiones entre el video fuente y el frame preparado que resulta
/// de crop, giro de 180 grados y espejo. Los perfiles se guardan en coordenadas
/// fuente; las mediciones usan coordenadas preparadas solo al analizar frames.
/// </summary>
public static class VideoCoordinateMapper
{
    public static bool TryMapPreparedToSource(
        VideoCropRect prepared,
        VideoTransformConfig transform,
        int sourceWidth,
        int sourceHeight,
        out VideoCropRect? source,
        out string? error)
    {
        source = null;
        if (!TryValidate(prepared, transform, sourceWidth, sourceHeight, out var crop, out error))
            return false;

        var x = prepared.X;
        var y = prepared.Y;
        UndoMirror(ref x, crop.Width, prepared.Width, transform.MirrorHorizontally);
        UndoRotation(ref x, ref y, crop.Width, crop.Height, prepared.Width, prepared.Height, transform.Rotation);

        source = new VideoCropRect(x + crop.X, y + crop.Y, prepared.Width, prepared.Height);
        return true;
    }

    public static bool TryMapSourceToPrepared(
        VideoCropRect source,
        VideoTransformConfig transform,
        int sourceWidth,
        int sourceHeight,
        out VideoCropRect? prepared,
        out string? error)
    {
        prepared = null;
        if (!transform.TryValidateFor(sourceWidth, sourceHeight, out error))
            return false;
        if (!source.IsWithin(sourceWidth, sourceHeight))
        {
            error = "La región debe quedar completamente dentro del video fuente.";
            return false;
        }

        var crop = transform.Crop ?? new VideoCropRect(0, 0, sourceWidth, sourceHeight);
        var x = source.X - crop.X;
        var y = source.Y - crop.Y;
        var cropRelative = new VideoCropRect(x, y, source.Width, source.Height);
        if (!cropRelative.IsWithin(crop.Width, crop.Height))
        {
            error = "La región fuente no cabe dentro del recorte configurado.";
            return false;
        }

        ApplyRotation(ref x, ref y, crop.Width, crop.Height, source.Width, source.Height, transform.Rotation);
        ApplyMirror(ref x, crop.Width, source.Width, transform.MirrorHorizontally);
        prepared = new VideoCropRect(x, y, source.Width, source.Height);
        return true;
    }

    private static bool TryValidate(
        VideoCropRect prepared,
        VideoTransformConfig transform,
        int sourceWidth,
        int sourceHeight,
        out VideoCropRect crop,
        out string? error)
    {
        crop = null!;
        if (!transform.TryValidateFor(sourceWidth, sourceHeight, out error))
            return false;

        crop = transform.Crop ?? new VideoCropRect(0, 0, sourceWidth, sourceHeight);
        if (!prepared.IsWithin(crop.Width, crop.Height))
        {
            error = "La región debe quedar completamente dentro del video preparado.";
            return false;
        }

        return true;
    }

    private static void ApplyRotation(
        ref int x,
        ref int y,
        int width,
        int height,
        int regionWidth,
        int regionHeight,
        VideoRotation rotation)
    {
        if (rotation == VideoRotation.UpsideDown)
        {
            x = width - x - regionWidth;
            y = height - y - regionHeight;
        }
    }

    private static void UndoRotation(
        ref int x,
        ref int y,
        int width,
        int height,
        int regionWidth,
        int regionHeight,
        VideoRotation rotation) =>
        ApplyRotation(ref x, ref y, width, height, regionWidth, regionHeight, rotation);

    private static void ApplyMirror(ref int x, int width, int regionWidth, bool mirrorHorizontally)
    {
        if (mirrorHorizontally)
            x = width - x - regionWidth;
    }

    private static void UndoMirror(ref int x, int width, int regionWidth, bool mirrorHorizontally) =>
        ApplyMirror(ref x, width, regionWidth, mirrorHorizontally);
}
