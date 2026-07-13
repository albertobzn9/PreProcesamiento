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
            using var cropped = decoded[crop].Clone();
            using var rotated = Rotate(cropped, config.Rotation);
            using var transformed = Mirror(rotated, config.MirrorHorizontally);
            Cv2.ImEncode(
                ".jpg",
                transformed,
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
                Width = transformed.Width,
                Height = transformed.Height,
            };
            return true;
        }
        catch (Exception ex)
        {
            error = $"No se pudo aplicar la configuración de cámara: {ex.Message}";
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
