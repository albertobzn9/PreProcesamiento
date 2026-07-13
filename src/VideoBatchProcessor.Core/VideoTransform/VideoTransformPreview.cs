using OpenCvSharp;
using VideoBatchProcessor.Core.VideoReader;

namespace VideoBatchProcessor.Core.VideoTransform;

/// <summary>
/// Resultado JPEG de aplicar una configuración de cámara a un preview. Conserva
/// la configuración en coordenadas del video fuente aunque el preview esté reducido.
/// </summary>
public sealed record VideoTransformPreview
{
    public required byte[] JpegBytes { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}

public static class VideoTransformPreviewRenderer
{
    public static bool TryRender(
        VideoPreview source,
        VideoTransformConfig config,
        out VideoTransformPreview? preview,
        out string? error)
    {
        preview = null;
        error = null;

        if (!config.TryValidateFor(source.Metadata.Width, source.Metadata.Height, out error))
            return false;

        try
        {
            using var decoded = Cv2.ImDecode(source.JpegBytes, ImreadModes.Color);
            if (decoded.Empty())
            {
                error = "No se pudo leer la imagen de preview para aplicar la configuración de cámara.";
                return false;
            }

            var crop = ToPreviewCrop(config.Crop, source.Metadata, decoded.Size());
            if (!TryTransform(decoded, crop, config, out var transformed, out error))
                return false;

            using (transformed)
                return TryEncodePreview(transformed!, out preview, out error);
        }
        catch (Exception ex)
        {
            error = $"No se pudo aplicar la configuración de cámara: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Aplica crop, giro y espejo a un frame en resolución original. El caller
    /// es dueño del <see cref="Mat"/> resultante y debe liberarlo.
    /// </summary>
    public static bool TryTransformFrame(
        Mat source,
        VideoMetadata metadata,
        VideoTransformConfig config,
        out Mat? transformed,
        out string? error)
    {
        transformed = null;
        error = null;

        if (source is null || source.Empty())
        {
            error = "No se recibió un frame válido para aplicar la configuración de cámara.";
            return false;
        }

        if (source.Width != metadata.Width || source.Height != metadata.Height)
        {
            error = "El frame no coincide con las dimensiones del video fuente.";
            return false;
        }

        if (!config.TryValidateFor(metadata.Width, metadata.Height, out error))
            return false;

        var crop = config.Crop is null
            ? new Rect(0, 0, source.Width, source.Height)
            : new Rect(config.Crop.X, config.Crop.Y, config.Crop.Width, config.Crop.Height);

        return TryTransform(source, crop, config, out transformed, out error);
    }

    /// <summary>
    /// Convierte un frame ya preparado a JPEG para la interfaz. Reduce la
    /// imagen si es necesario, pero no cambia el frame usado para medir brillo.
    /// </summary>
    public static bool TryEncodePreview(
        Mat source,
        out VideoTransformPreview? preview,
        out string? error,
        int maxDimension = 1280)
    {
        preview = null;
        error = null;

        if (source is null || source.Empty())
        {
            error = "No se recibió un frame válido para crear el preview.";
            return false;
        }

        if (maxDimension <= 0)
        {
            error = "El tamaño máximo del preview debe ser mayor que cero.";
            return false;
        }

        try
        {
            using var previewFrame = ResizeForPreview(source, maxDimension);
            Cv2.ImEncode(
                ".jpg",
                previewFrame,
                out var jpegBytes,
                new ImageEncodingParam(ImwriteFlags.JpegQuality, 85));

            if (jpegBytes.Length == 0)
            {
                error = "No se pudo convertir la configuración de cámara a una imagen de preview.";
                return false;
            }

            preview = new VideoTransformPreview
            {
                JpegBytes = jpegBytes,
                Width = previewFrame.Width,
                Height = previewFrame.Height,
            };
            return true;
        }
        catch (Exception ex)
        {
            error = $"No se pudo crear el preview del frame: {ex.Message}";
            return false;
        }
    }

    private static Rect ToPreviewCrop(VideoCropRect? crop, VideoMetadata metadata, Size previewSize)
    {
        if (crop is null)
            return new Rect(0, 0, previewSize.Width, previewSize.Height);

        var left = (int)Math.Floor(crop.X * previewSize.Width / (double)metadata.Width);
        var top = (int)Math.Floor(crop.Y * previewSize.Height / (double)metadata.Height);
        var right = (int)Math.Ceiling((crop.X + crop.Width) * previewSize.Width / (double)metadata.Width);
        var bottom = (int)Math.Ceiling((crop.Y + crop.Height) * previewSize.Height / (double)metadata.Height);

        left = Math.Clamp(left, 0, previewSize.Width - 1);
        top = Math.Clamp(top, 0, previewSize.Height - 1);
        right = Math.Clamp(right, left + 1, previewSize.Width);
        bottom = Math.Clamp(bottom, top + 1, previewSize.Height);
        return new Rect(left, top, right - left, bottom - top);
    }

    private static bool TryTransform(
        Mat source,
        Rect crop,
        VideoTransformConfig config,
        out Mat? transformed,
        out string? error)
    {
        transformed = null;
        error = null;

        try
        {
            using var cropped = source[crop].Clone();
            using var rotated = Rotate(cropped, config.Rotation);
            transformed = Mirror(rotated, config.MirrorHorizontally);
            return true;
        }
        catch (Exception ex)
        {
            error = $"No se pudo aplicar la configuración de cámara: {ex.Message}";
            transformed?.Dispose();
            transformed = null;
            return false;
        }
    }

    private static Mat ResizeForPreview(Mat source, int maxDimension)
    {
        var largestDimension = Math.Max(source.Width, source.Height);
        if (largestDimension <= maxDimension)
            return source.Clone();

        var scale = maxDimension / (double)largestDimension;
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var resized = new Mat();
        Cv2.Resize(source, resized, new Size(width, height), interpolation: InterpolationFlags.Area);
        return resized;
    }

    private static Mat Rotate(Mat source, VideoRotation rotation)
    {
        var result = new Mat();
        switch (rotation)
        {
            case VideoRotation.None:
                source.CopyTo(result);
                break;
            case VideoRotation.UpsideDown:
                Cv2.Rotate(source, result, RotateFlags.Rotate180);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(rotation), rotation, "Rotación no reconocida.");
        }

        return result;
    }

    private static Mat Mirror(Mat source, bool mirrorHorizontally)
    {
        if (!mirrorHorizontally)
            return source.Clone();

        var result = new Mat();
        Cv2.Flip(source, result, FlipMode.Y);
        return result;
    }
}
