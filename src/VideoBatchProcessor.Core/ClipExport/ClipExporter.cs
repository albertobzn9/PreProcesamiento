using System.Diagnostics;
using System.Globalization;
using VideoBatchProcessor.Core.SegmentPlanning;
using VideoBatchProcessor.Core.VideoReader;
using VideoBatchProcessor.Core.VideoTransform;

namespace VideoBatchProcessor.Core.ClipExport;

/// <summary>
/// Exporta un segmento ya planeado sin modificar el video fuente. Esta primera
/// versión sirve para Cruces Seguros: recibe una sola sesión y un solo clip.
/// </summary>
public sealed class ClipExporter
{
    private readonly IFfmpegRunner _ffmpegRunner;

    public ClipExporter(IFfmpegRunner? ffmpegRunner = null)
    {
        _ffmpegRunner = ffmpegRunner ?? new FfmpegProcessRunner();
    }

    public async Task<ClipExportResult> ExportAsync(
        ClipExportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryValidate(request, out var error))
            return ClipExportResult.Failed(request.OutputPath, error!);

        var outputDirectory = Path.GetDirectoryName(request.OutputPath)!;
        Directory.CreateDirectory(outputDirectory);

        var temporaryPath = CreateTemporaryPath(request.OutputPath);
        try
        {
            var exportRange = ResolveExportRange(request);
            var arguments = BuildArguments(request, temporaryPath, exportRange);
            var execution = await _ffmpegRunner.RunAsync(
                request.Options.FfmpegPath,
                arguments,
                cancellationToken);

            if (!execution.Succeeded)
            {
                TryDelete(temporaryPath);
                return ClipExportResult.Failed(
                    request.OutputPath,
                    $"FFmpeg no pudo exportar el clip: {execution.ErrorMessage}");
            }

            if (!File.Exists(temporaryPath))
            {
                return ClipExportResult.Failed(
                    request.OutputPath,
                    "FFmpeg terminó sin crear el archivo temporal del clip.");
            }

            File.Move(temporaryPath, request.OutputPath);
            return new ClipExportResult(
                true,
                request.OutputPath,
                exportRange.StartFrameIndex / request.SourceVideo.Fps,
                (exportRange.EndFrameIndex + 1) / request.SourceVideo.Fps,
                null,
                exportRange.StartFrameIndex,
                exportRange.EndFrameIndex);
        }
        catch (OperationCanceledException)
        {
            TryDelete(temporaryPath);
            return ClipExportResult.Failed(request.OutputPath, "La exportación fue cancelada.");
        }
        catch (Exception ex)
        {
            TryDelete(temporaryPath);
            return ClipExportResult.Failed(request.OutputPath, $"No se pudo exportar el clip: {ex.Message}");
        }
    }

    internal static IReadOnlyList<string> BuildArguments(
        ClipExportRequest request,
        string temporaryPath,
        ClipExportFrameRange? exportRange = null)
    {
        var range = exportRange ?? ResolveExportRange(request);
        var start = range.StartFrameIndex / request.SourceVideo.Fps;
        var frameCount = range.EndFrameIndex - range.StartFrameIndex + 1;
        var arguments = new List<string>
        {
            "-hide_banner",
            "-loglevel", "error",
            "-y",
            "-ss", FormatSeconds(start),
            "-i", request.SourceVideoPath,
            "-map", "0:v:0",
            "-map", "0:a?",
            "-frames:v", frameCount.ToString(CultureInfo.InvariantCulture),
            "-c:v", request.Options.VideoCodec,
            "-crf", request.Options.Crf.ToString(CultureInfo.InvariantCulture),
            "-c:a", "aac",
            "-shortest",
        };

        var filters = BuildVideoFilters(request.Transform);
        if (filters.Count > 0)
        {
            arguments.Add("-vf");
            arguments.Add(string.Join(',', filters));
        }

        arguments.Add(temporaryPath);
        return arguments;
    }

    internal static IReadOnlyList<string> BuildVideoFilters(VideoTransformConfig transform)
    {
        var filters = new List<string>();
        if (transform.Crop is not null)
        {
            var crop = transform.Crop;
            filters.Add($"crop={crop.Width}:{crop.Height}:{crop.X}:{crop.Y}");
        }

        if (transform.Rotation == VideoRotation.UpsideDown)
        {
            filters.Add("hflip");
            filters.Add("vflip");
        }

        if (transform.MirrorHorizontally)
            filters.Add("hflip");

        return filters;
    }

    private static bool TryValidate(ClipExportRequest request, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(request.SourceVideoPath) || !File.Exists(request.SourceVideoPath))
        {
            error = "El video fuente no existe.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.OutputPath) || !Path.IsPathRooted(request.OutputPath))
        {
            error = "La ruta de salida debe ser absoluta.";
            return false;
        }

        if (File.Exists(request.OutputPath))
        {
            error = "Ya existe un archivo con ese nombre de salida.";
            return false;
        }

        if (request.SourceVideo.Fps <= 0 || request.SourceVideo.TotalFrames <= 0)
        {
            error = "El video fuente no tiene FPS o frames totales válidos.";
            return false;
        }

        if (request.Segment.StartFrameIndex < 0 ||
            request.Segment.EndFrameIndex < request.Segment.StartFrameIndex ||
            request.Segment.EndFrameIndex >= request.SourceVideo.TotalFrames)
        {
            error = "Los límites del segmento no caben dentro del video fuente.";
            return false;
        }

        if (request.Options.EventContextFramesBefore < 0 || request.Options.EventContextFramesAfter < 0)
        {
            error = "Los frames de contexto no pueden ser negativos.";
            return false;
        }

        return request.Transform.TryValidateFor(request.SourceVideo.Width, request.SourceVideo.Height, out error);
    }

    internal static ClipExportFrameRange ResolveExportRange(ClipExportRequest request)
    {
        var isEvent = request.Segment.Kind == PlannedSegmentKind.Event;
        var startPadding = isEvent ? request.Options.EventContextFramesBefore : 0;
        var endPadding = isEvent ? request.Options.EventContextFramesAfter : 0;
        return new ClipExportFrameRange(
            Math.Max(0, request.Segment.StartFrameIndex - startPadding),
            Math.Min((int)request.SourceVideo.TotalFrames - 1, request.Segment.EndFrameIndex + endPadding));
    }

    private static string CreateTemporaryPath(string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath)!;
        var stem = Path.GetFileNameWithoutExtension(outputPath);
        var extension = Path.GetExtension(outputPath);
        return Path.Combine(directory, $".{stem}.{Guid.NewGuid():N}.partial{extension}");
    }

    private static double EndExclusiveSeconds(PlannedVideoSegment segment, double fps) =>
        (segment.EndFrameIndex + 1) / fps;

    private static string FormatSeconds(double seconds) =>
        seconds.ToString("0.########", CultureInfo.InvariantCulture);

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // El resultado ya informa el error principal de exportación.
        }
    }
}

public sealed record ClipExportRequest(
    string SourceVideoPath,
    VideoMetadata SourceVideo,
    PlannedVideoSegment Segment,
    VideoTransformConfig Transform,
    string OutputPath,
    ClipExportOptions Options);

public sealed record ClipExportOptions
{
    public string FfmpegPath { get; init; } = "ffmpeg";
    public string VideoCodec { get; init; } = "libx264";
    public int Crf { get; init; } = 18;
    public int EventContextFramesBefore { get; init; } = 5;
    public int EventContextFramesAfter { get; init; } = 5;
}

public sealed record ClipExportResult(
    bool Succeeded,
    string OutputPath,
    double? StartSeconds,
    double? EndExclusiveSeconds,
    string? ErrorMessage,
    int? StartFrameIndex = null,
    int? EndFrameIndex = null)
{
    public static ClipExportResult Failed(string outputPath, string errorMessage) =>
        new(false, outputPath, null, null, errorMessage);
}

public sealed record ClipExportFrameRange(int StartFrameIndex, int EndFrameIndex);

public interface IFfmpegRunner
{
    Task<FfmpegExecutionResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken);
}

public sealed record FfmpegExecutionResult(bool Succeeded, string? ErrorMessage)
{
    public static FfmpegExecutionResult Success() => new(true, null);
    public static FfmpegExecutionResult Failure(string errorMessage) => new(false, errorMessage);
}

internal sealed class FfmpegProcessRunner : IFfmpegRunner
{
    public async Task<FfmpegExecutionResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return FfmpegExecutionResult.Failure($"No se pudo iniciar FFmpeg: {ex.Message}");
        }

        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var error = await standardError;
        return process.ExitCode == 0
            ? FfmpegExecutionResult.Success()
            : FfmpegExecutionResult.Failure(string.IsNullOrWhiteSpace(error) ? $"Código de salida {process.ExitCode}." : error.Trim());
    }
}
