using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using VideoBatchProcessor.Core.Nomenclature;
using VideoBatchProcessor.Core.SessionResolver;

namespace VideoBatchProcessor.App;

public sealed partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SessionSetupService _sessionSetup = new();

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
            var type = document.RootElement.GetProperty("type").GetString();

            switch (type)
            {
                case "selectFiles":
                    await SelectFilesAsync();
                    break;
                case "selectFolder":
                    await SelectFolderAsync();
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
        var sessions = entries
            .Except(skipped)
            .Select(ToWebSession)
            .ToArray();

        await SendToWebAsync(new
        {
            type = "sessionsLoaded",
            sessions,
            skippedCount = skipped.Length,
        });
    }

    private Task<string?> SendToWebAsync(object message)
    {
        var serialized = JsonSerializer.Serialize(message, JsonOptions);
        return Browser.InvokeScript($"window.receiveFromHost({serialized});");
    }

    private static WebSession ToWebSession(SessionSetupEntry entry)
    {
        var metadata = entry.Metadata;
        return new WebSession(
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
