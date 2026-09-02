using OpenCvSharp;

namespace VideoBatchProcessor.Core.VideoReader;

/// <summary>
/// Lectura de archivos de video con OpenCvSharp.
/// <para>
/// Patrón de uso:
/// <code>
/// if (!VideoReader.TryOpen(path, out var reader, out var error))
/// {
///     // Mostrar error al usuario
///     return;
/// }
/// using (reader)
/// {
///     Console.WriteLine($"FPS: {reader.Metadata.Fps}");
///
///     // Lectura secuencial
///     while (await reader.MoveNextAsync(ct))
///     {
///         var frame = reader.Current;   // Mat reusable, no hacer Dispose
///         // ... procesar frame
///     }
///
///     // Lectura aleatoria
///     using var snapshot = await reader.ReadFrameAtAsync(TimeSpan.FromSeconds(30), ct);
/// }
/// </code>
/// </para>
/// <para>
/// No es thread-safe. Un VideoReader por hilo / por consumidor.
/// </para>
/// </summary>
public sealed class VideoReader : IDisposable
{
    private readonly VideoCapture _capture;
    private readonly Mat          _currentFrame = new();
    private          bool         _disposed;

    public VideoMetadata Metadata { get; }

    /// <summary>
    /// Frame actual después de MoveNext/MoveNextAsync.
    /// El Mat es reutilizado entre lecturas; si necesitas conservarlo, clona con frame.Clone().
    /// </summary>
    public Mat Current => _currentFrame;

    /// <summary>
    /// Índice del frame actual (0-based). -1 antes del primer MoveNext.
    /// </summary>
    public long CurrentFrameIndex { get; private set; } = -1;

    /// <summary>
    /// Timestamp del frame actual.
    /// </summary>
    public TimeSpan CurrentTimestamp =>
        Metadata.Fps > 0
            ? TimeSpan.FromSeconds(CurrentFrameIndex / Metadata.Fps)
            : TimeSpan.Zero;

    // ── Construcción privada — usar TryOpen ───────────────────────────────

    private VideoReader(VideoCapture capture, VideoMetadata metadata)
    {
        _capture = capture;
        Metadata = metadata;
    }

    // ── API estática de apertura ──────────────────────────────────────────

    /// <summary>
    /// Intenta abrir un archivo de video.
    /// Devuelve true y el reader si funcionó; false y un mensaje de error si no.
    /// Nunca lanza excepciones.
    /// </summary>
    /// <param name="filePath">Ruta al archivo de video.</param>
    /// <param name="reader">VideoReader listo para usar (debe usarse con `using`).</param>
    /// <param name="error">Mensaje legible para el usuario si falla.</param>
    public static bool TryOpen(
        string filePath,
        out VideoReader? reader,
        out string? error)
    {
        reader = null;
        error  = null;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            error = "No se proporcionó una ruta de archivo.";
            return false;
        }

        if (!File.Exists(filePath))
        {
            error = $"El archivo no existe: {filePath}";
            return false;
        }

        VideoCapture? capture = null;
        try
        {
            capture = new VideoCapture(filePath);

            if (!capture.IsOpened())
            {
                error = "No se pudo abrir el video. Verifica que el formato sea compatible (mp4, mkv, avi, mov o m4v).";
                capture.Dispose();
                return false;
            }

            var metadata = ReadMetadata(capture, filePath);

            if (metadata.Fps <= 0 || metadata.TotalFrames <= 0)
            {
                error = "El video se abrió pero su metadata es inválida (fps o frames totales en cero).";
                capture.Dispose();
                return false;
            }

            reader = new VideoReader(capture, metadata);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Error al abrir el video: {ex.Message}";
            capture?.Dispose();
            return false;
        }
    }

    /// <summary>
    /// Lee el primer frame disponible y lo convierte en JPEG para mostrarlo en
    /// una interfaz. La imagen se reduce si rebasa <paramref name="maxDimension"/>,
    /// pero la metadata conserva las dimensiones reales del video.
    /// </summary>
    public static bool TryReadPreview(
        string filePath,
        out VideoPreview? preview,
        out string? error,
        int maxDimension = 1280)
    {
        preview = null;
        error = null;

        if (maxDimension <= 0)
        {
            error = "El tamaño máximo del preview debe ser mayor que cero.";
            return false;
        }

        if (!TryOpen(filePath, out var reader, out error))
            return false;

        try
        {
            using var openedReader = reader!;
            if (!openedReader.MoveNext())
            {
                error = "No se pudo leer el primer frame del video.";
                return false;
            }

            using var source = openedReader.Current.Clone();
            using var previewFrame = ResizeForPreview(source, maxDimension);
            Cv2.ImEncode(
                ".jpg",
                previewFrame,
                out var jpegBytes,
                new ImageEncodingParam(ImwriteFlags.JpegQuality, 85));

            if (jpegBytes.Length == 0)
            {
                error = "No se pudo convertir el frame a una imagen de preview.";
                return false;
            }

            preview = new VideoPreview
            {
                Metadata = openedReader.Metadata,
                JpegBytes = jpegBytes,
                PreviewWidth = previewFrame.Width,
                PreviewHeight = previewFrame.Height,
                FrameIndex = openedReader.CurrentFrameIndex,
                Timestamp = openedReader.CurrentTimestamp,
            };
            return true;
        }
        catch (Exception ex)
        {
            error = $"No se pudo generar el preview: {ex.Message}";
            return false;
        }
    }

    // ── Lectura secuencial ────────────────────────────────────────────────

    /// <summary>
    /// Avanza al siguiente frame. Devuelve true si lo leyó; false si llegó al final.
    /// El frame queda disponible en <see cref="Current"/>.
    /// </summary>
    /// <param name="ct">Token de cancelación. Si se cancela durante la lectura, devuelve false.</param>
    public Task<bool> MoveNextAsync(CancellationToken ct = default)
        => Task.Run(() => MoveNext(ct), ct);

    /// <summary>
    /// Versión síncrona de MoveNextAsync. Útil cuando ya estás en un Task.Run.
    /// </summary>
    public bool MoveNext(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (ct.IsCancellationRequested) return false;

        var ok = _capture.Read(_currentFrame);
        if (!ok || _currentFrame.Empty()) return false;

        CurrentFrameIndex++;
        return true;
    }

    // ── Lectura aleatoria ─────────────────────────────────────────────────

    /// <summary>
    /// Salta a un timestamp específico y devuelve ese frame como un Mat nuevo.
    /// El caller es dueño del Mat devuelto y debe llamar Dispose() o usar `using`.
    /// </summary>
    /// <param name="timestamp">Posición en el video. Se redondea al frame más cercano.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Mat con el frame; null si el timestamp está fuera de rango o la lectura falló.</returns>
    public Task<Mat?> ReadFrameAtAsync(TimeSpan timestamp, CancellationToken ct = default)
        => Task.Run(() => ReadFrameAt(timestamp, ct), ct);

    /// <summary>
    /// Salta a un índice de frame específico (0-based) y devuelve ese frame.
    /// El caller es dueño del Mat devuelto y debe llamar Dispose() o usar `using`.
    /// </summary>
    public Task<Mat?> ReadFrameAtAsync(long frameIndex, CancellationToken ct = default)
        => Task.Run(() => ReadFrameAt(frameIndex, ct), ct);

    /// <summary>Versión síncrona de ReadFrameAtAsync.</summary>
    public Mat? ReadFrameAt(TimeSpan timestamp, CancellationToken ct = default)
    {
        var index = (long)Math.Round(timestamp.TotalSeconds * Metadata.Fps);
        return ReadFrameAt(index, ct);
    }

    /// <summary>Versión síncrona de ReadFrameAtAsync.</summary>
    public Mat? ReadFrameAt(long frameIndex, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (ct.IsCancellationRequested)        return null;
        if (frameIndex < 0)                    return null;
        if (frameIndex >= Metadata.TotalFrames) return null;

        _capture.Set(VideoCaptureProperties.PosFrames, frameIndex);

        var frame = new Mat();
        var ok = _capture.Read(frame);
        if (!ok || frame.Empty())
        {
            frame.Dispose();
            return null;
        }

        CurrentFrameIndex = frameIndex;
        return frame;
    }

    /// <summary>
    /// Reinicia la lectura secuencial al inicio del video.
    /// </summary>
    public void Reset()
    {
        ThrowIfDisposed();
        _capture.Set(VideoCaptureProperties.PosFrames, 0);
        CurrentFrameIndex = -1;
    }

    // ── Helpers privados ──────────────────────────────────────────────────

    private static VideoMetadata ReadMetadata(VideoCapture cap, string filePath)
    {
        var fps    = cap.Get(VideoCaptureProperties.Fps);
        var width  = (int)cap.Get(VideoCaptureProperties.FrameWidth);
        var height = (int)cap.Get(VideoCaptureProperties.FrameHeight);
        var frames = (long)cap.Get(VideoCaptureProperties.FrameCount);
        var fourcc = (int)cap.Get(VideoCaptureProperties.FourCC);

        var duration = fps > 0
            ? TimeSpan.FromSeconds(frames / fps)
            : TimeSpan.Zero;

        return new VideoMetadata
        {
            Fps          = fps,
            Width        = width,
            Height       = height,
            TotalFrames  = frames,
            Duration     = duration,
            Codec        = FourCcToString(fourcc),
            FilePath     = Path.GetFullPath(filePath),
        };
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

    private static string FourCcToString(int fourcc)
    {
        Span<char> chars = stackalloc char[4];
        chars[0] = (char)( fourcc        & 0xFF);
        chars[1] = (char)((fourcc >>  8) & 0xFF);
        chars[2] = (char)((fourcc >> 16) & 0xFF);
        chars[3] = (char)((fourcc >> 24) & 0xFF);
        return new string(chars);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(VideoReader));
    }

    // ── IDisposable ───────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _currentFrame.Dispose();
        _capture.Dispose();
    }
}
