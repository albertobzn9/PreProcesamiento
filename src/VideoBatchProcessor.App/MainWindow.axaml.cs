using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using VideoBatchProcessor.Core.Nomenclature;
using VideoBatchProcessor.Core.SessionResolver;
using VideoBatchProcessor.Core.VideoReader;
using VideoBatchProcessor.Core.VideoTransform;

namespace VideoBatchProcessor.App;

public sealed partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SessionSetupService _sessionSetup = new();
    private readonly Dictionary<string, SessionSetupEntry> _loadedSessions = [];
    private readonly Dictionary<string, VideoPreview> _loadedPreviews = [];
    private readonly Dictionary<string, VideoTransformConfig> _cameraConfigs = [];

    public MainWindow()
    {
        InitializeComponent();
        Browser.Source = new Uri(Path.Combine(AppContext.BaseDirectory, "WebUi", "index.html"));
    }

    private async void Browser_WebMessageReceived(object? sender, WebMessageReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Body))
        {
            await SendToWebAsync(new { type = "status", message = "La interfaz envió un mensaje vacío." });
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(e.Body);
            if (!document.RootElement.TryGetProperty("type", out var typeProperty))
            {
                await SendToWebAsync(new { type = "status", message = "La interfaz no indicó una acción." });
                return;
            }

            var type = typeProperty.GetString();

            switch (type)
            {
                case "selectFiles":
                    await SelectFilesAsync();
                    break;
                case "selectFolder":
                    await SelectFolderAsync();
                    break;
                case "selectSession":
                    await LoadPreviewAsync(document.RootElement);
                    break;
                case "updateCameraSetup":
                    await UpdateCameraSetupAsync(document.RootElement);
                    break;
            }
        }
        catch (JsonException)
        {
            await SendToWebAsync(new { type = "status", message = "La interfaz envió un mensaje no reconocido." });
        }
    }

    private async Task SelectFilesAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Agregar videos de sesión",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Video")
                {
                    Patterns = ["*.mp4", "*.avi", "*.mov", "*.mkv", "*.m4v"],
                },
            ],
        });

        if (files.Count == 0)
            return;

        await SendSessionsAsync(files.Select(file => file.Path.LocalPath));
    }

    private async Task SelectFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Seleccionar carpeta con videos de sesión",
            AllowMultiple = false,
        });

        var folderPath = folders.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        try
        {
            var videoPaths = Directory.EnumerateFiles(folderPath, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
            })
                .Where(SessionSetupService.IsSupportedVideo);
            await SendSessionsAsync(videoPaths);
        }
        catch (UnauthorizedAccessException)
        {
            await SendToWebAsync(new { type = "status", message = "No hay permiso para leer esa carpeta." });
        }
        catch (IOException)
        {
            await SendToWebAsync(new { type = "status", message = "No se pudo leer esa carpeta." });
        }
    }

    private async Task SendSessionsAsync(IEnumerable<string> videoPaths)
    {
        var entries = _sessionSetup.AnalyzeFiles(videoPaths);
        var skipped = entries
            .Where(entry => entry.ParsedName.IsExcludedFromBatchInput)
            .ToArray();
        _loadedSessions.Clear();
        _loadedPreviews.Clear();
        _cameraConfigs.Clear();
        var sessions = entries
            .Except(skipped)
            .Select(entry =>
            {
                var sessionId = Guid.NewGuid().ToString("N");
                _loadedSessions[sessionId] = entry;
                return ToWebSession(sessionId, entry);
            })
            .ToArray();

        await SendToWebAsync(new
        {
            type = "sessionsLoaded",
            sessions,
            skippedCount = skipped.Length,
        });
    }

    private async Task LoadPreviewAsync(JsonElement message)
    {
        if (!message.TryGetProperty("sessionId", out var sessionIdProperty) ||
            string.IsNullOrWhiteSpace(sessionIdProperty.GetString()) ||
            !_loadedSessions.TryGetValue(sessionIdProperty.GetString()!, out var entry))
        {
            await SendToWebAsync(new { type = "status", message = "La sesión seleccionada ya no está disponible." });
            return;
        }

        if (!entry.ParsedName.IsSourceSession)
        {
            await SendToWebAsync(new { type = "status", message = "Solo los videos fuente compatibles se pueden previsualizar." });
            return;
        }

        var result = await Task.Run(() =>
        {
            var success = VideoReader.TryReadPreview(entry.Metadata.SourceVideoPath, out var preview, out var error);
            return (success, preview, error);
        });

        if (!result.success || result.preview is null)
        {
            await SendToWebAsync(new { type = "previewFailed", sessionId = sessionIdProperty.GetString(), message = result.error ?? "No se pudo generar el preview." });
            return;
        }

        var sessionId = sessionIdProperty.GetString()!;
        _loadedPreviews[sessionId] = result.preview;
        _cameraConfigs[sessionId] = new VideoTransformConfig();
        await SendCameraPreviewAsync(sessionId, result.preview, _cameraConfigs[sessionId]);
    }

    private async Task UpdateCameraSetupAsync(JsonElement message)
    {
        if (!TryGetSessionPreview(message, out var sessionId, out var sourcePreview))
        {
            await SendToWebAsync(new { type = "status", message = "Abre primero una sesión fuente para configurar su cámara." });
            return;
        }

        if (!TryReadCameraConfig(message, out var config, out var error))
        {
            await SendToWebAsync(new { type = "cameraPreviewFailed", sessionId, message = error });
            return;
        }

        var previous = _cameraConfigs.GetValueOrDefault(sessionId) ?? new VideoTransformConfig();
        _cameraConfigs[sessionId] = config;
        if (!await SendCameraPreviewAsync(sessionId, sourcePreview, config))
            _cameraConfigs[sessionId] = previous;
    }

    private bool TryGetSessionPreview(JsonElement message, out string sessionId, out VideoPreview preview)
    {
        sessionId = string.Empty;
        preview = null!;
        if (!message.TryGetProperty("sessionId", out var sessionIdProperty) ||
            string.IsNullOrWhiteSpace(sessionIdProperty.GetString()))
            return false;

        sessionId = sessionIdProperty.GetString()!;
        return _loadedPreviews.TryGetValue(sessionId, out preview!);
    }

    private static bool TryReadCameraConfig(JsonElement message, out VideoTransformConfig config, out string error)
    {
        config = new VideoTransformConfig();
        error = "La configuración de cámara no es válida.";

        if (!message.TryGetProperty("rotation", out var rotationProperty) ||
            !TryParseRotation(rotationProperty.GetString(), out var rotation))
        {
            error = "La rotación seleccionada no es válida.";
            return false;
        }

        if (!message.TryGetProperty("mirrorHorizontally", out var mirrorProperty) ||
            (mirrorProperty.ValueKind is not JsonValueKind.True and not JsonValueKind.False))
        {
            error = "El valor de espejo no es válido.";
            return false;
        }

        VideoCropRect? crop = null;
        if (message.TryGetProperty("crop", out var cropProperty) && cropProperty.ValueKind != JsonValueKind.Null)
        {
            if (!TryReadCrop(cropProperty, out crop))
            {
                error = "El recorte debe tener x, y, ancho y alto enteros.";
                return false;
            }
        }

        config = new VideoTransformConfig
        {
            Rotation = rotation,
            MirrorHorizontally = mirrorProperty.GetBoolean(),
            Crop = crop,
        };
        return true;
    }

    private static bool TryParseRotation(string? value, out VideoRotation rotation) =>
        Enum.TryParse(value, ignoreCase: false, out rotation) && Enum.IsDefined(rotation);

    private static bool TryReadCrop(JsonElement cropProperty, out VideoCropRect? crop)
    {
        crop = null;
        if (cropProperty.ValueKind != JsonValueKind.Object ||
            !cropProperty.TryGetProperty("x", out var x) || !x.TryGetInt32(out var cropX) ||
            !cropProperty.TryGetProperty("y", out var y) || !y.TryGetInt32(out var cropY) ||
            !cropProperty.TryGetProperty("width", out var width) || !width.TryGetInt32(out var cropWidth) ||
            !cropProperty.TryGetProperty("height", out var height) || !height.TryGetInt32(out var cropHeight))
            return false;

        crop = new VideoCropRect(cropX, cropY, cropWidth, cropHeight);
        return true;
    }

    private async Task<bool> SendCameraPreviewAsync(string sessionId, VideoPreview sourcePreview, VideoTransformConfig config)
    {
        var result = await Task.Run(() =>
        {
            var success = VideoTransformPreviewRenderer.TryRender(sourcePreview, config, out var preview, out var error);
            return (success, preview, error);
        });

        if (!result.success || result.preview is null)
        {
            await SendToWebAsync(new
            {
                type = "cameraPreviewFailed",
                sessionId,
                message = result.error ?? "No se pudo aplicar la configuración de cámara.",
            });
            return false;
        }

        var preview = result.preview;
        await SendToWebAsync(new
        {
            type = "cameraPreviewLoaded",
            sessionId,
            imageDataUrl = $"data:image/jpeg;base64,{Convert.ToBase64String(preview.JpegBytes)}",
            sourceImageDataUrl = $"data:image/jpeg;base64,{Convert.ToBase64String(sourcePreview.JpegBytes)}",
            width = sourcePreview.Metadata.Width,
            height = sourcePreview.Metadata.Height,
            sourcePreviewWidth = sourcePreview.PreviewWidth,
            sourcePreviewHeight = sourcePreview.PreviewHeight,
            previewWidth = preview.Width,
            previewHeight = preview.Height,
            fps = sourcePreview.Metadata.Fps,
            durationSeconds = sourcePreview.Metadata.Duration.TotalSeconds,
            totalFrames = sourcePreview.Metadata.TotalFrames,
            rotation = config.Rotation.ToString(),
            mirrorHorizontally = config.MirrorHorizontally,
            crop = config.Crop,
        });
        return true;
    }

    private Task<string?> SendToWebAsync(object message)
    {
        var serialized = JsonSerializer.Serialize(message, JsonOptions);
        return Browser.InvokeScript($"window.receiveFromHost({serialized});");
    }

    private static WebSession ToWebSession(string sessionId, SessionSetupEntry entry)
    {
        var metadata = entry.Metadata;
        return new WebSession(
            sessionId,
            Path.GetFileName(metadata.SourceVideoPath),
            metadata.Scheme.ToString(),
            entry.ParsedName.IsSourceSession,
            metadata.IsComplete,
            metadata.Fase,
            metadata.Dia == 0 ? null : metadata.Dia.ToString(),
            metadata.Rata == 0 ? null : metadata.Rata.ToString(),
            metadata.IsComplete ? null : string.Join(", ", metadata.MissingFields),
            DescribeInputMessage(entry.ParsedName),
            DescribeBehavioralSource(metadata));
    }

    private static string? DescribeInputMessage(ParsedFileName parsedName) =>
        parsedName.Scheme == NamingScheme.VideoBatchOutput
            ? "Este archivo ya es un clip generado por Video Batch Processor. Carga el video completo de la sesión."
            : null;

    private static string DescribeBehavioralSource(SessionMetadata metadata) => metadata.SourceBehavioralKind switch
    {
        "CsvV1" when metadata.SourceBehavioralPath is { } path => $"CSV V1: {Path.GetFileName(path)}",
        "LegacyMat" when metadata.SourceBehavioralPath is { } path => $"MAT: {Path.GetFileName(path)}",
        _ => "Sin fuente conductual",
    };

    private sealed record WebSession(
        string SessionId,
        string FileName,
        string NamingScheme,
        bool IsSourceSession,
        bool IsComplete,
        string? Phase,
        string? Day,
        string? Rat,
        string? MissingFields,
        string? InputMessage,
        string BehavioralSource);
}
