using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using VideoBatchProcessor.Core.BehavioralData;
using VideoBatchProcessor.Core.BatchProcessing;
using VideoBatchProcessor.Core.CameraProfiles;
using VideoBatchProcessor.Core.ClipExport;
using VideoBatchProcessor.Core.Diagnostics;
using VideoBatchProcessor.Core.FrameAnalyzer;
using VideoBatchProcessor.Core.LightDetection;
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
    private readonly Dictionary<string, LightDetectionConfig> _lightConfigs = [];
    private readonly Dictionary<string, CalibrationFrameMeasurement> _calibrationFrames = [];
    private readonly Dictionary<string, SessionLightCalibration> _lightCalibrations = [];
    private readonly Dictionary<string, LightTimelineScanResult> _lightTimelines = [];
    private readonly Dictionary<string, LightTimelineScanRange> _lightTimelineRanges = [];
    private readonly CameraSetupProfileStore _cameraProfileStore = new();
    private string? _loadedFolderPath;

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
                case "updateLightRois":
                    await UpdateLightRoisAsync(document.RootElement);
                    break;
                case "requestCalibrationFrame":
                    await SendCalibrationFrameAsync(document.RootElement);
                    break;
                case "saveLightCalibration":
                    await SaveLightCalibrationAsync(document.RootElement);
                    break;
                case "analyzeLightTimeline":
                    await AnalyzeLightTimelineAsync(document.RootElement);
                    break;
                case "exportLightTimelineDiagnostic":
                    await ExportLightTimelineDiagnosticAsync(document.RootElement);
                    break;
                case "processBatch":
                    await ProcessBatchAsync(document.RootElement);
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

        _loadedFolderPath = null;
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

        _loadedFolderPath = folderPath;

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
        var alternateFormatCount = entries.Sum(entry => entry.AlternateVideoPaths?.Count ?? 0);
        _loadedSessions.Clear();
        _loadedPreviews.Clear();
        _cameraConfigs.Clear();
        _lightConfigs.Clear();
        _calibrationFrames.Clear();
        _lightCalibrations.Clear();
        _lightTimelines.Clear();
        _lightTimelineRanges.Clear();
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
            alternateFormatCount,
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
        if (TryRestoreCameraProfile(entry.Metadata.SourceVideoPath, result.preview, out var restoredProfile, out var profileError))
        {
            _cameraConfigs[sessionId] = restoredProfile!.Transform;
            _lightConfigs[sessionId] = restoredProfile.Lights;
        }
        await SendCameraPreviewAsync(sessionId, result.preview, _cameraConfigs[sessionId]);
        if (restoredProfile is not null)
            await SendToWebAsync(new { type = "status", message = "Se recuperaron el recorte, orientación y las tres ROIs guardadas para este video. Solo falta volver a calibrar las luces." });
        else if (!string.IsNullOrWhiteSpace(profileError))
            await SendToWebAsync(new { type = "status", message = profileError });
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
        if (previous != config)
        {
            _lightConfigs.Remove(sessionId);
            ClearCalibrationState(sessionId);
            _lightTimelines.Remove(sessionId);
            _lightTimelineRanges.Remove(sessionId);
            if (_loadedSessions.TryGetValue(sessionId, out var session))
                _cameraProfileStore.Delete(session.Metadata.SourceVideoPath);
        }

        if (!await SendCameraPreviewAsync(sessionId, sourcePreview, config))
            _cameraConfigs[sessionId] = previous;
    }

    private async Task UpdateLightRoisAsync(JsonElement message)
    {
        if (!TryGetSessionPreview(message, out var sessionId, out var preview))
        {
            await SendToWebAsync(new { type = "status", message = "Abre primero una sesión fuente para marcar sus luces." });
            return;
        }

        if (!message.TryGetProperty("lightRois", out var roisProperty))
        {
            await SendToWebAsync(new
            {
                type = "lightRoisRejected",
                sessionId,
                message = "Faltan las regiones de las tres luces.",
            });
            return;
        }

        if (!TryReadLightConfig(roisProperty, out var lightConfig, out var error))
        {
            await SendToWebAsync(new { type = "lightRoisRejected", sessionId, message = error });
            return;
        }

        var cameraConfig = _cameraConfigs.GetValueOrDefault(sessionId) ?? new VideoTransformConfig();
        var frameWidth = cameraConfig.Crop?.Width ?? preview.Metadata.Width;
        var frameHeight = cameraConfig.Crop?.Height ?? preview.Metadata.Height;
        if (!AreRoisFullyInside(lightConfig, frameWidth, frameHeight))
        {
            await SendToWebAsync(new
            {
                type = "lightRoisRejected",
                sessionId,
                message = "Cada región de luz debe quedar completamente dentro del video preparado.",
            });
            return;
        }

        var previousCalibration = _lightCalibrations.GetValueOrDefault(sessionId);
        _lightConfigs[sessionId] = lightConfig;
        ClearCalibrationState(sessionId);
        _lightTimelines.Remove(sessionId);
        _lightTimelineRanges.Remove(sessionId);
        var profileSaveError = _loadedSessions.TryGetValue(sessionId, out var profileSession) &&
            !_cameraProfileStore.TrySave(
                profileSession.Metadata.SourceVideoPath,
                preview.Metadata.Width,
                preview.Metadata.Height,
                cameraConfig,
                lightConfig,
                out var savedProfileError)
            ? savedProfileError
            : null;

        object? restoredCalibration = null;
        var updateMessage = profileSaveError is null
            ? "Las tres regiones quedaron validadas y guardadas para este video preparado."
            : $"Las tres regiones quedaron validadas, pero no se pudieron guardar para reabrir el video: {profileSaveError}";
        if (previousCalibration is not null && _loadedSessions.TryGetValue(sessionId, out var entry))
        {
            var restoration = await Task.Run(() => TryRestoreCalibration(
                entry.Metadata.SourceVideoPath,
                cameraConfig,
                lightConfig,
                previousCalibration));

            if (restoration.Success && restoration.OffFrame?.Measurement is not null &&
                restoration.ActiveFrame?.Measurement is not null && restoration.Rebuilt is not null)
            {
                var offToken = Guid.NewGuid().ToString("N");
                var activeToken = Guid.NewGuid().ToString("N");
                var offFrame = restoration.OffFrame.Measurement with { SessionId = sessionId };
                var activeFrame = restoration.ActiveFrame.Measurement with { SessionId = sessionId };
                _calibrationFrames[offToken] = offFrame;
                _calibrationFrames[activeToken] = activeFrame;
                _lightConfigs[sessionId] = restoration.Rebuilt.Config;
                _lightCalibrations[sessionId] = restoration.Rebuilt.Calibration;
                restoredCalibration = new
                {
                    offReference = ToWebCalibrationReference(offFrame, offToken, restoration.OffFrame.Preview!),
                    activeReference = ToWebCalibrationReference(activeFrame, activeToken, restoration.ActiveFrame.Preview!),
                    thresholds = ToWebThresholds(restoration.Rebuilt.Calibration),
                    directFoodSide = restoration.Rebuilt.Calibration.DirectFoodSide.ToString(),
                    mode = restoration.Rebuilt.Calibration.Mode.ToString(),
                    noiseLedEnabled = restoration.Rebuilt.Calibration.NoiseLed is not null,
                };
                updateMessage = "Las ROIs se actualizaron y los mismos frames OFF/ON se volvieron a medir con las nuevas regiones.";
            }
            else
            {
                updateMessage = $"Las ROIs se actualizaron, pero no se pudo volver a medir la calibración anterior: {restoration.Error ?? "elige de nuevo los frames OFF y ON."}";
            }
        }

        await SendToWebAsync(new
        {
            type = "lightRoisUpdated",
            sessionId,
            lightRois = ToWebLightRois(lightConfig),
            calibration = restoredCalibration,
            message = updateMessage,
        });
    }

    private async Task SendCalibrationFrameAsync(JsonElement message)
    {
        if (!TryReadCalibrationRequest(message, out var sessionId, out var frameIndex, out var requestId, out var error))
        {
            await SendToWebAsync(new { type = "calibrationFrameRejected", requestId = 0L, message = error });
            return;
        }

        if (!_loadedSessions.TryGetValue(sessionId, out var entry) ||
            !_lightConfigs.TryGetValue(sessionId, out var lightConfig))
        {
            await SendToWebAsync(new
            {
                type = "calibrationFrameRejected",
                requestId,
                message = "Marca y guarda primero las tres regiones de luz para abrir la calibración.",
            });
            return;
        }

        var cameraConfig = _cameraConfigs.GetValueOrDefault(sessionId) ?? new VideoTransformConfig();
        var result = await Task.Run(() => TryMeasureCalibrationFrame(
            entry.Metadata.SourceVideoPath,
            cameraConfig,
            lightConfig,
            frameIndex));

        if (!result.Success || result.Measurement is null || result.Preview is null)
        {
            await SendToWebAsync(new
            {
                type = "calibrationFrameRejected",
                requestId,
                message = result.Error ?? "No se pudo leer ese frame para la calibración.",
            });
            return;
        }

        var token = Guid.NewGuid().ToString("N");
        _calibrationFrames[token] = result.Measurement with { SessionId = sessionId };
        var measurement = result.Measurement;
        await SendToWebAsync(new
        {
            type = "calibrationFrameLoaded",
            sessionId,
            requestId,
            frameToken = token,
            frameIndex = measurement.FrameIndex,
            estimatedTimestampSeconds = measurement.EstimatedTimestamp.TotalSeconds,
            imageDataUrl = $"data:image/jpeg;base64,{Convert.ToBase64String(result.Preview.JpegBytes)}",
            width = result.Preview.Width,
            height = result.Preview.Height,
            brightness = new
            {
                foodLeft = measurement.FoodLeftBrightness,
                foodRight = measurement.FoodRightBrightness,
                noiseLed = measurement.NoiseLedBrightness,
            },
        });
    }

    private async Task SaveLightCalibrationAsync(JsonElement message)
    {
        if (!TryReadCalibrationSave(message, out var sessionId, out var offToken, out var activeToken, out var foodSide, out var mode, out var error))
        {
            await SendToWebAsync(new { type = "lightCalibrationRejected", message = error });
            return;
        }

        if (!_lightConfigs.TryGetValue(sessionId, out var previousConfig) ||
            !_calibrationFrames.TryGetValue(offToken, out var offFrame) ||
            !_calibrationFrames.TryGetValue(activeToken, out var activeFrame) ||
            offFrame.SessionId != sessionId ||
            activeFrame.SessionId != sessionId)
        {
            await SendToWebAsync(new
            {
                type = "lightCalibrationRejected",
                message = "Las referencias ya no son válidas. Abre otra vez la calibración y selecciónalas de nuevo.",
            });
            return;
        }

        try
        {
            var rebuilt = BuildCalibration(previousConfig, offFrame, activeFrame, foodSide, mode);
            _lightConfigs[sessionId] = rebuilt.Config;
            _lightCalibrations[sessionId] = rebuilt.Calibration;
            _lightTimelines.Remove(sessionId);
            _lightTimelineRanges.Remove(sessionId);
            await SendToWebAsync(new
            {
                type = "lightCalibrationSaved",
                sessionId,
                thresholds = ToWebThresholds(rebuilt.Calibration),
                directFoodSide = foodSide.ToString(),
                mode = mode.ToString(),
                noiseLedEnabled = rebuilt.Calibration.NoiseLed is not null,
                message = mode == LightCalibrationMode.FoodOnly
                    ? "Calibración segura guardada. El LED de ruido queda desactivado para esta sesión."
                    : "Calibración guardada para este lote. La otra luz de comida usa una referencia compartida provisional.",
            });
        }
        catch (ArgumentException exception)
        {
            await SendToWebAsync(new { type = "lightCalibrationRejected", message = exception.Message });
        }
    }

    private async Task AnalyzeLightTimelineAsync(JsonElement message)
    {
        if (!TryGetSessionPreview(message, out var sessionId, out var sourcePreview)
            || !_loadedSessions.TryGetValue(sessionId, out var entry))
        {
            await SendToWebAsync(new
            {
                type = "lightTimelineRejected",
                message = "Abre una sesión fuente antes de analizar sus luces.",
            });
            return;
        }

        if (!_lightConfigs.TryGetValue(sessionId, out var lightConfig)
            || !_lightCalibrations.ContainsKey(sessionId))
        {
            await SendToWebAsync(new
            {
                type = "lightTimelineRejected",
                sessionId,
                message = "Marca, guarda y calibra las tres luces antes de analizar el video.",
            });
            return;
        }

        if (!TryReadTimelineRange(message, sourcePreview.Metadata.TotalFrames, out var scanRange, out var rangeError))
        {
            await SendToWebAsync(new
            {
                type = "lightTimelineRejected",
                sessionId,
                message = rangeError,
            });
            return;
        }

        var cameraConfig = _cameraConfigs.GetValueOrDefault(sessionId) ?? new VideoTransformConfig();
        try
        {
            var progress = new Progress<LightTimelineScanProgress>(update =>
                Dispatcher.UIThread.Post(async () =>
                {
                    await SendToWebAsync(new
                    {
                        type = "lightTimelineProgress",
                        sessionId,
                        framesProcessed = update.FramesProcessed,
                        totalFrames = update.TotalFrames,
                        percent = update.Percent,
                    });
                }));
            var result = await Task.Run(() => new LightTimelineScanner().Scan(
                entry.Metadata.SourceVideoPath,
                cameraConfig,
                lightConfig,
                scanRange: scanRange,
                progress: progress));
            _lightTimelines[sessionId] = result;
            _lightTimelineRanges[sessionId] = scanRange;
            var intervals = LightEventIntervalBuilder.Build(result.Timeline);

            await SendToWebAsync(new
            {
                type = "lightTimelineReady",
                sessionId,
                samplesAnalyzed = result.Timeline.SamplesAnalyzed,
                intervalCount = intervals.Count,
                startFrame = scanRange.StartFrame,
                endFrame = scanRange.EndFrame,
                transitions = result.Timeline.Transitions.Select(transition => new
                {
                    light = transition.Light.ToString(),
                    wasOn = transition.WasOn,
                    isOn = transition.IsOn,
                    frameIndex = transition.FrameIndex,
                    timeSeconds = transition.TimeSeconds,
                    confirmedAtFrameIndex = transition.ConfirmedAtFrameIndex,
                }),
                message = $"Se analizaron {result.Timeline.SamplesAnalyzed:N0} frames y se detectaron {result.Timeline.Transitions.Count} cambios estables.",
            });
        }
        catch (Exception exception)
        {
            await SendToWebAsync(new
            {
                type = "lightTimelineRejected",
                sessionId,
                message = $"No se pudo analizar este video: {exception.Message}",
            });
        }
    }

    private async Task ExportLightTimelineDiagnosticAsync(JsonElement message)
    {
        if (!TryGetSessionPreview(message, out var sessionId, out _)
            || !_loadedSessions.TryGetValue(sessionId, out var entry)
            || !_lightTimelines.TryGetValue(sessionId, out var scanResult)
            || !_lightTimelineRanges.TryGetValue(sessionId, out var scanRange)
            || !_lightConfigs.TryGetValue(sessionId, out var lightConfig))
        {
            await SendToWebAsync(new
            {
                type = "lightTimelineExportRejected",
                message = "Analiza primero las luces de esta sesión para poder exportar su evidencia.",
            });
            return;
        }

        var suggestedName = $"{Path.GetFileNameWithoutExtension(entry.Metadata.SourceVideoPath)}_diagnostico_luces.xlsx";
        var output = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Guardar diagnóstico de luces",
            SuggestedFileName = suggestedName,
            DefaultExtension = "xlsx",
            FileTypeChoices =
            [
                new FilePickerFileType("Libro de Excel")
                {
                    Patterns = ["*.xlsx"],
                },
            ],
        });

        if (output is null)
            return;

        try
        {
            var behavioralEvidence = ReadBehavioralEvidence(entry.Metadata);
            var cameraConfig = _cameraConfigs.GetValueOrDefault(sessionId) ?? new VideoTransformConfig();
            var report = new LightTimelineDiagnosticReport(
                scanResult.Metadata,
                scanRange,
                cameraConfig,
                scanResult.Timeline,
                LightEventIntervalBuilder.Build(scanResult.Timeline),
                lightConfig,
                behavioralEvidence.Events,
                behavioralEvidence.SourcePath,
                behavioralEvidence.Error);

            await Task.Run(() => new LightTimelineDiagnosticExcelExporter().Export(report, output.Path.LocalPath));
            await SendToWebAsync(new
            {
                type = "lightTimelineExported",
                sessionId,
                filePath = output.Path.LocalPath,
                message = "Se guardó el Excel de diagnóstico. Compáralo con el video y con la hoja MAT conductual.",
            });
        }
        catch (Exception exception)
        {
            await SendToWebAsync(new
            {
                type = "lightTimelineExportRejected",
                sessionId,
                message = $"No se pudo exportar el diagnóstico: {exception.Message}",
            });
        }
    }

    private async Task ProcessBatchAsync(JsonElement message)
    {
        if (string.IsNullOrWhiteSpace(_loadedFolderPath) || !Directory.Exists(_loadedFolderPath))
        {
            await SendToWebAsync(new { type = "batchProcessingRejected", message = "Carga primero una carpeta de sesiones; el procesamiento por lote no usa archivos sueltos." });
            return;
        }

        if (!TryReadBatchSetup(message, out var sessionId, out var initials, out var sex, out var treatment, out var exportMode, out var error) ||
            !_loadedSessions.TryGetValue(sessionId, out var referenceSession) ||
            !_cameraConfigs.TryGetValue(sessionId, out var transform) ||
            !_lightConfigs.TryGetValue(sessionId, out var lightConfig) ||
            !_lightCalibrations.ContainsKey(sessionId))
        {
            await SendToWebAsync(new { type = "batchProcessingRejected", message = error ?? "Abre una sesión, marca las luces y guarda su calibración antes de procesar el lote." });
            return;
        }

        // Cada sesión conserva sus clips junto a sus fuentes, en una carpeta
        // propia con el mismo stem. No se pide una segunda ruta al usuario.
        var outputDirectory = _loadedFolderPath;

        var manifest = new BatchManifest
        {
            Iniciales = initials,
            Sexo = sex,
            Tratamiento = treatment,
        };
        var request = new BatchProcessingRequest(
            _loadedFolderPath,
            outputDirectory,
            transform,
            lightConfig,
            manifest,
            new ClipExportOptions(),
            exportMode);
        var progress = new Progress<BatchProcessingProgress>(update =>
            Dispatcher.UIThread.Post(async () => await SendToWebAsync(new
            {
                type = "batchProcessingProgress",
                completedSessions = update.CompletedSessions,
                totalSessions = update.TotalSessions,
                currentVideo = update.CurrentVideoPath is null ? null : Path.GetFileName(update.CurrentVideoPath),
                percent = update.Percent,
                message = update.Message,
            })));

        try
        {
            await SendToWebAsync(new
            {
                type = "batchProcessingStarted",
                message = $"Procesando sesiones CS con la configuración de {Path.GetFileName(referenceSession.Metadata.SourceVideoPath)}.",
            });
            var report = await Task.Run(() => new BatchOrchestrator().RunAsync(request, progress));
            await SendToWebAsync(new
            {
                type = "batchProcessingCompleted",
                exportedSessions = report.ExportedSessionCount,
                blockedSessions = report.BlockedSessionCount,
                failedSessions = report.FailedSessionCount,
                exportedClips = report.ExportedClipCount,
                outputDirectory,
                sessions = report.Sessions.Select(item => new
                {
                    video = Path.GetFileName(item.VideoPath),
                    status = item.Status.ToString(),
                    message = item.Message,
                    clips = item.Clips.Count(clip => clip.Export.Succeeded),
                    diagnostic = item.DiagnosticPath,
                }),
            });
        }
        catch (Exception exception)
        {
            await SendToWebAsync(new { type = "batchProcessingRejected", message = $"No se pudo completar el lote: {exception.Message}" });
        }
    }

    private static (IReadOnlyList<BehavioralEvent> Events, string? SourcePath, string? Error) ReadBehavioralEvidence(
        SessionMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata.SourceBehavioralPath))
            return ([], null, metadata.BehavioralSourceError ?? "No se encontró un CSV o MAT junto al video.");

        if (!Enum.TryParse<BehavioralSourceKind>(metadata.SourceBehavioralKind, out var sourceKind) ||
            sourceKind == BehavioralSourceKind.None)
        {
            return ([], metadata.SourceBehavioralPath, "La fuente conductual no tiene un formato reconocido.");
        }

        var source = new BehavioralSourceResolution
        {
            SourcePath = metadata.SourceBehavioralPath,
            PressesPath = metadata.SourcePressesPath,
            SourceKind = sourceKind,
            Warnings = metadata.BehavioralSourceWarnings,
            Error = metadata.BehavioralSourceError,
        };

        try
        {
            var reader = sourceKind switch
            {
                BehavioralSourceKind.LegacyMat => (IBehavioralSessionReader)new LegacyMatBehavioralSessionReader(new MatV5MatrixReader()),
                BehavioralSourceKind.CsvV1 => new CsvV1BehavioralSessionReader(),
                _ => throw new BehavioralDataFormatException("La fuente conductual no tiene un lector disponible."),
            };
            return (reader.Read(source).Events, source.SourcePath, null);
        }
        catch (Exception exception)
        {
            return ([], source.SourcePath, exception.Message);
        }
    }

    private bool TryRestoreCameraProfile(
        string videoPath,
        VideoPreview preview,
        out SavedCameraSetupProfile? profile,
        out string? error)
    {
        profile = null;
        error = null;
        if (!_cameraProfileStore.TryLoad(
                videoPath,
                preview.Metadata.Width,
                preview.Metadata.Height,
                out var saved,
                out var loadError))
        {
            error = loadError;
            return false;
        }

        if (saved is null)
            return false;

        if (!saved.Transform.TryValidateFor(preview.Metadata.Width, preview.Metadata.Height, out var transformError))
        {
            error = $"Se ignoró un perfil de cámara que ya no coincide con las dimensiones del video: {transformError}";
            return false;
        }

        var preparedWidth = saved.Transform.Crop?.Width ?? preview.Metadata.Width;
        var preparedHeight = saved.Transform.Crop?.Height ?? preview.Metadata.Height;
        if (!AreRoisFullyInside(saved.Lights, preparedWidth, preparedHeight))
        {
            error = "Se ignoró un perfil de cámara cuyas ROIs ya no caben dentro del video preparado.";
            return false;
        }

        profile = saved;
        return true;
    }

    private static bool TryReadBatchSetup(
        JsonElement message,
        out string sessionId,
        out string initials,
        out string sex,
        out string treatment,
        out BatchExportMode exportMode,
        out string? error)
    {
        sessionId = string.Empty;
        initials = string.Empty;
        sex = string.Empty;
        treatment = string.Empty;
        exportMode = BatchExportMode.ValidationSample;
        error = null;

        if (!message.TryGetProperty("sessionId", out var sessionProperty) ||
            string.IsNullOrWhiteSpace(sessionProperty.GetString()))
        {
            error = "Selecciona la sesión cuya configuración de cámara y luces se aplicará al lote.";
            return false;
        }

        if (!TryReadBatchCode(message, "initials", value =>
                value.Length is >= 2 and <= 5 && value.All(char.IsAsciiLetter), out initials) ||
            !TryReadBatchCode(message, "sex", value => value is "m" or "h", out sex) ||
            !TryReadBatchCode(message, "treatment", value => value.Length > 0 && value.All(char.IsAsciiLetterOrDigit), out treatment))
        {
            error = "Para nombrar clips legacy indica iniciales (2-5 letras), sexo (m/h) y tratamiento como código breve sin espacios.";
            return false;
        }

        sessionId = sessionProperty.GetString()!;
        if (message.TryGetProperty("exportMode", out var exportModeProperty) &&
            exportModeProperty.ValueKind == JsonValueKind.String &&
            string.Equals(exportModeProperty.GetString(), "AllSegments", StringComparison.Ordinal))
        {
            exportMode = BatchExportMode.AllSegments;
        }
        return true;
    }

    private static bool TryReadBatchCode(
        JsonElement message,
        string propertyName,
        Func<string, bool> isValid,
        out string value)
    {
        value = string.Empty;
        if (!message.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            return false;

        value = property.GetString()?.Trim().ToLowerInvariant() ?? string.Empty;
        return isValid(value);
    }

    private static bool TryReadTimelineRange(
        JsonElement message,
        long totalFrames,
        out LightTimelineScanRange range,
        out string error)
    {
        range = null!;
        error = "El intervalo de análisis no es válido.";

        if (totalFrames is <= 0 or > int.MaxValue)
        {
            error = "La duración del video no permite elegir un intervalo de frames válido.";
            return false;
        }

        var availableFrames = (int)totalFrames;
        if (!message.TryGetProperty("scanRange", out var rangeProperty) || rangeProperty.ValueKind == JsonValueKind.Null)
        {
            range = new LightTimelineScanRange(0, availableFrames - 1);
            return true;
        }

        if (rangeProperty.ValueKind != JsonValueKind.Object ||
            !rangeProperty.TryGetProperty("startFrame", out var startProperty) || !startProperty.TryGetInt32(out var startFrame) ||
            !rangeProperty.TryGetProperty("endFrame", out var endProperty) || !endProperty.TryGetInt32(out var endFrame))
        {
            error = "El intervalo debe indicar un frame inicial y un frame final enteros.";
            return false;
        }

        range = new LightTimelineScanRange(startFrame, endFrame);
        try
        {
            range.Validate(availableFrames);
            return true;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static CalibrationFrameReadResult TryMeasureCalibrationFrame(
        string videoPath,
        VideoTransformConfig cameraConfig,
        LightDetectionConfig lightConfig,
        long frameIndex)
    {
        if (!VideoReader.TryOpen(videoPath, out var reader, out var error))
            return CalibrationFrameReadResult.Failed(error);

        using (reader)
        using (var source = reader!.ReadFrameAt(frameIndex))
        {
            if (source is null)
                return CalibrationFrameReadResult.Failed("No se pudo leer ese frame del video.");

            if (!VideoTransformPreviewRenderer.TryTransformFrame(source, reader.Metadata, cameraConfig, out var prepared, out error))
                return CalibrationFrameReadResult.Failed(error);

            using (prepared)
            {
                if (!VideoTransformPreviewRenderer.TryEncodePreview(prepared!, out var preview, out error))
                    return CalibrationFrameReadResult.Failed(error);

                var brightness = new FrameAnalyzerBrightnessSource(prepared!, lightConfig);
                var measurement = new CalibrationFrameMeasurement(
                    SessionId: string.Empty,
                    FrameIndex: reader.CurrentFrameIndex,
                    EstimatedTimestamp: reader.CurrentTimestamp,
                    FoodLeftBrightness: brightness.GetMeanBrightness(lightConfig.FoodLeft),
                    FoodRightBrightness: brightness.GetMeanBrightness(lightConfig.FoodRight),
                    NoiseLedBrightness: brightness.GetMeanBrightness(lightConfig.NoiseLed));
                return CalibrationFrameReadResult.Succeeded(measurement, preview!);
            }
        }
    }

    private static CalibrationRestorationResult TryRestoreCalibration(
        string videoPath,
        VideoTransformConfig cameraConfig,
        LightDetectionConfig lightConfig,
        SessionLightCalibration previousCalibration)
    {
        var directFoodCalibration = previousCalibration.DirectFoodSide == LightId.FoodLeft
            ? previousCalibration.FoodLeft
            : previousCalibration.FoodRight;
        var offFrameIndex = directFoodCalibration.OffReferences.FirstOrDefault()?.FrameIndex;
        var activeFrameIndex = directFoodCalibration.OnReferences.FirstOrDefault()?.FrameIndex;
        if (offFrameIndex is null || activeFrameIndex is null)
            return CalibrationRestorationResult.Failed("faltan las referencias OFF/ON guardadas.");

        var offFrame = TryMeasureCalibrationFrame(videoPath, cameraConfig, lightConfig, offFrameIndex.Value);
        if (!offFrame.Success || offFrame.Measurement is null || offFrame.Preview is null)
            return CalibrationRestorationResult.Failed(offFrame.Error ?? "no se pudo releer el frame OFF.");

        var activeFrame = TryMeasureCalibrationFrame(videoPath, cameraConfig, lightConfig, activeFrameIndex.Value);
        if (!activeFrame.Success || activeFrame.Measurement is null || activeFrame.Preview is null)
            return CalibrationRestorationResult.Failed(activeFrame.Error ?? "no se pudo releer el frame ON.");

        try
        {
            var rebuilt = BuildCalibration(
                lightConfig,
                offFrame.Measurement,
                activeFrame.Measurement,
                previousCalibration.DirectFoodSide,
                previousCalibration.Mode);
            return CalibrationRestorationResult.Succeeded(offFrame, activeFrame, rebuilt);
        }
        catch (ArgumentException exception)
        {
            return CalibrationRestorationResult.Failed(exception.Message);
        }
    }

    private static RebuiltCalibration BuildCalibration(
        LightDetectionConfig lightConfig,
        CalibrationFrameMeasurement offFrame,
        CalibrationFrameMeasurement activeFrame,
        LightId foodSide,
        LightCalibrationMode mode)
    {
        var foodDirect = CreateCalibration(foodSide, offFrame, activeFrame);
        var otherFoodSide = foodSide == LightId.FoodLeft ? LightId.FoodRight : LightId.FoodLeft;
        var sharedFood = CreateSharedFoodCalibration(otherFoodSide, offFrame, activeFrame, foodDirect);
        var noise = mode == LightCalibrationMode.FoodAndNoise
            ? CreateCalibration(LightId.NoiseLed, offFrame, activeFrame)
            : null;
        var left = foodSide == LightId.FoodLeft ? foodDirect : sharedFood;
        var right = foodSide == LightId.FoodRight ? foodDirect : sharedFood;
        var config = new LightDetectionConfig(
            WithThreshold(lightConfig.FoodLeft, left.AcceptedThreshold),
            WithThreshold(lightConfig.FoodRight, right.AcceptedThreshold),
            WithThreshold(lightConfig.NoiseLed, noise?.AcceptedThreshold ?? double.MaxValue));
        return new RebuiltCalibration(config, new SessionLightCalibration(left, right, noise, foodSide, mode));
    }

    private static bool TryReadCalibrationRequest(
        JsonElement message,
        out string sessionId,
        out long frameIndex,
        out long requestId,
        out string error)
    {
        sessionId = string.Empty;
        frameIndex = -1;
        requestId = 0;
        error = "La solicitud de frame para calibración no es válida.";

        if (!message.TryGetProperty("sessionId", out var sessionIdProperty) ||
            string.IsNullOrWhiteSpace(sessionIdProperty.GetString()) ||
            !message.TryGetProperty("frameIndex", out var frameIndexProperty) ||
            !frameIndexProperty.TryGetInt64(out frameIndex) ||
            frameIndex < 0)
            return false;

        if (message.TryGetProperty("requestId", out var requestIdProperty))
            requestIdProperty.TryGetInt64(out requestId);

        sessionId = sessionIdProperty.GetString()!;
        return true;
    }

    private static bool TryReadCalibrationSave(
        JsonElement message,
        out string sessionId,
        out string offToken,
        out string activeToken,
        out LightId foodSide,
        out LightCalibrationMode mode,
        out string error)
    {
        sessionId = string.Empty;
        offToken = string.Empty;
        activeToken = string.Empty;
        foodSide = LightId.FoodLeft;
        mode = LightCalibrationMode.FoodAndNoise;
        error = "La calibración no contiene las referencias necesarias.";

        if (!message.TryGetProperty("sessionId", out var sessionIdProperty) ||
            string.IsNullOrWhiteSpace(sessionIdProperty.GetString()) ||
            !message.TryGetProperty("offFrameToken", out var offTokenProperty) ||
            string.IsNullOrWhiteSpace(offTokenProperty.GetString()) ||
            !message.TryGetProperty("activeFrameToken", out var activeTokenProperty) ||
            string.IsNullOrWhiteSpace(activeTokenProperty.GetString()) ||
            !message.TryGetProperty("foodSide", out var foodSideProperty) ||
            !message.TryGetProperty("mode", out var modeProperty))
            return false;

        if (!Enum.TryParse(foodSideProperty.GetString(), ignoreCase: true, out foodSide) ||
            foodSide is not (LightId.FoodLeft or LightId.FoodRight))
        {
            error = "Selecciona cuál luz de comida está encendida en la referencia activa.";
            return false;
        }

        if (!Enum.TryParse(modeProperty.GetString(), ignoreCase: true, out mode) ||
            !Enum.IsDefined(mode))
        {
            error = "Selecciona un modo de calibración válido.";
            return false;
        }

        sessionId = sessionIdProperty.GetString()!;
        offToken = offTokenProperty.GetString()!;
        activeToken = activeTokenProperty.GetString()!;
        return true;
    }

    private static LightCalibration CreateCalibration(
        LightId light,
        CalibrationFrameMeasurement offFrame,
        CalibrationFrameMeasurement onFrame) =>
        new(
            light,
            [new LightCalibrationReference(offFrame.FrameIndex, offFrame.EstimatedTimestamp, offFrame.BrightnessFor(light))],
            [new LightCalibrationReference(onFrame.FrameIndex, onFrame.EstimatedTimestamp, onFrame.BrightnessFor(light))]);

    private static LightCalibration CreateSharedFoodCalibration(
        LightId light,
        CalibrationFrameMeasurement offFrame,
        CalibrationFrameMeasurement activeFrame,
        LightCalibration directFood) =>
        new(
            light,
            [new LightCalibrationReference(offFrame.FrameIndex, offFrame.EstimatedTimestamp, offFrame.BrightnessFor(light))],
            [new LightCalibrationReference(activeFrame.FrameIndex, activeFrame.EstimatedTimestamp, directFood.OnMedianBrightness)],
            usesSharedFoodReference: true);

    private static LightRoi WithThreshold(LightRoi roi, double threshold) =>
        new(roi.Light, roi.X, roi.Y, roi.Width, roi.Height, threshold, roi.Shape);

    private void ClearCalibrationState(string sessionId)
    {
        _lightCalibrations.Remove(sessionId);
        var tokens = _calibrationFrames
            .Where(pair => pair.Value.SessionId == sessionId)
            .Select(pair => pair.Key)
            .ToArray();
        foreach (var token in tokens)
            _calibrationFrames.Remove(token);
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
            if (!success || preview is null)
                return (success, preview, error, cropEditor: (VideoTransformPreview?)null, cropEditorError: (string?)null);

            var cropEditorConfig = config with { Crop = null };
            var cropEditorSuccess = VideoTransformPreviewRenderer.TryRender(
                sourcePreview,
                cropEditorConfig,
                out var cropEditor,
                out var cropEditorError);
            return (success, preview, error, cropEditor, cropEditorError);
        });

        if (!result.success || result.preview is null || result.cropEditor is null)
        {
            await SendToWebAsync(new
            {
                type = "cameraPreviewFailed",
                sessionId,
                message = result.error ?? result.cropEditorError ?? "No se pudo aplicar la configuración de cámara.",
            });
            return false;
        }

        var preview = result.preview;
        var cropEditor = result.cropEditor;
        await SendToWebAsync(new
        {
            type = "cameraPreviewLoaded",
            sessionId,
            imageDataUrl = $"data:image/jpeg;base64,{Convert.ToBase64String(preview.JpegBytes)}",
            cropEditorImageDataUrl = $"data:image/jpeg;base64,{Convert.ToBase64String(cropEditor.JpegBytes)}",
            width = sourcePreview.Metadata.Width,
            height = sourcePreview.Metadata.Height,
            cropEditorWidth = cropEditor.Width,
            cropEditorHeight = cropEditor.Height,
            previewWidth = preview.Width,
            previewHeight = preview.Height,
            fps = sourcePreview.Metadata.Fps,
            durationSeconds = sourcePreview.Metadata.Duration.TotalSeconds,
            totalFrames = sourcePreview.Metadata.TotalFrames,
            rotation = config.Rotation.ToString(),
            mirrorHorizontally = config.MirrorHorizontally,
            crop = config.Crop,
            lightRois = _lightConfigs.TryGetValue(sessionId, out var lightConfig)
                ? ToWebLightRois(lightConfig)
                : null,
        });
        return true;
    }

    private static bool TryReadLightConfig(JsonElement rois, out LightDetectionConfig config, out string error)
    {
        config = null!;
        error = "Las tres regiones de luz deben tener coordenadas enteras válidas.";

        if (rois.ValueKind != JsonValueKind.Object ||
            !TryReadLightRoi(rois, "foodLeft", LightId.FoodLeft, out var foodLeft) ||
            !TryReadLightRoi(rois, "foodRight", LightId.FoodRight, out var foodRight) ||
            !TryReadLightRoi(rois, "noiseLed", LightId.NoiseLed, out var noiseLed))
            return false;

        try
        {
            config = new LightDetectionConfig(foodLeft, foodRight, noiseLed);
            return true;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static bool TryReadLightRoi(JsonElement rois, string propertyName, LightId light, out LightRoi roi)
    {
        roi = null!;
        if (!rois.TryGetProperty(propertyName, out var roiProperty) ||
            roiProperty.ValueKind != JsonValueKind.Object ||
            !roiProperty.TryGetProperty("x", out var x) || !x.TryGetInt32(out var roiX) ||
            !roiProperty.TryGetProperty("y", out var y) || !y.TryGetInt32(out var roiY) ||
            !roiProperty.TryGetProperty("width", out var width) || !width.TryGetInt32(out var roiWidth) ||
            !roiProperty.TryGetProperty("height", out var height) || !height.TryGetInt32(out var roiHeight))
            return false;

        try
        {
            roi = new LightRoi(light, roiX, roiY, roiWidth, roiHeight, shape: RoiShape.Circle);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static bool AreRoisFullyInside(LightDetectionConfig config, int frameWidth, int frameHeight)
    {
        var definitions = new[]
        {
            ToRoiDefinition(config.FoodLeft, TipoLed.Izquierda, "FoodLeft"),
            ToRoiDefinition(config.FoodRight, TipoLed.Derecha, "FoodRight"),
            ToRoiDefinition(config.NoiseLed, TipoLed.Ruido, "NoiseLed"),
        };

        return FrameAnalyzer.ValidateRois(definitions, frameWidth, frameHeight).Count == 0 &&
               definitions.All(roi =>
                   roi.Region.X + roi.Region.Width <= frameWidth &&
                   roi.Region.Y + roi.Region.Height <= frameHeight);
    }

    private static RoiDefinition ToRoiDefinition(LightRoi roi, TipoLed tipo, string label) => new()
    {
        Tipo = tipo,
        Label = label,
        Region = new OpenCvSharp.Rect(roi.X, roi.Y, roi.Width, roi.Height),
        Shape = roi.Shape,
    };

    private static object ToWebLightRois(LightDetectionConfig config) => new
    {
        foodLeft = ToWebLightRoi(config.FoodLeft),
        foodRight = ToWebLightRoi(config.FoodRight),
        noiseLed = ToWebLightRoi(config.NoiseLed),
    };

    private static object ToWebLightRoi(LightRoi roi) => new
    {
        x = roi.X,
        y = roi.Y,
        width = roi.Width,
        height = roi.Height,
        shape = roi.Shape.ToString(),
    };

    private static object ToWebCalibrationReference(
        CalibrationFrameMeasurement measurement,
        string token,
        VideoTransformPreview preview) => new
    {
        frameToken = token,
        frameIndex = measurement.FrameIndex,
        estimatedTimestampSeconds = measurement.EstimatedTimestamp.TotalSeconds,
        imageDataUrl = $"data:image/jpeg;base64,{Convert.ToBase64String(preview.JpegBytes)}",
        width = preview.Width,
        height = preview.Height,
        brightness = new
        {
            foodLeft = measurement.FoodLeftBrightness,
            foodRight = measurement.FoodRightBrightness,
            noiseLed = measurement.NoiseLedBrightness,
        },
    };

    private static object ToWebThresholds(SessionLightCalibration calibration) => new
    {
        foodLeft = calibration.FoodLeft.AcceptedThreshold,
        foodRight = calibration.FoodRight.AcceptedThreshold,
        noiseLed = calibration.NoiseLed?.AcceptedThreshold,
    };

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

    private sealed record CalibrationFrameMeasurement(
        string SessionId,
        long FrameIndex,
        TimeSpan EstimatedTimestamp,
        double FoodLeftBrightness,
        double FoodRightBrightness,
        double NoiseLedBrightness)
    {
        public double BrightnessFor(LightId light) => light switch
        {
            LightId.FoodLeft => FoodLeftBrightness,
            LightId.FoodRight => FoodRightBrightness,
            LightId.NoiseLed => NoiseLedBrightness,
            _ => throw new ArgumentOutOfRangeException(nameof(light), light, "Luz no reconocida."),
        };
    }

    private sealed record CalibrationFrameReadResult(
        bool Success,
        CalibrationFrameMeasurement? Measurement,
        VideoTransformPreview? Preview,
        string? Error)
    {
        public static CalibrationFrameReadResult Succeeded(
            CalibrationFrameMeasurement measurement,
            VideoTransformPreview preview) => new(true, measurement, preview, null);

        public static CalibrationFrameReadResult Failed(string? error) => new(false, null, null, error);
    }

    private sealed record RebuiltCalibration(
        LightDetectionConfig Config,
        SessionLightCalibration Calibration);

    private sealed record CalibrationRestorationResult(
        bool Success,
        CalibrationFrameReadResult? OffFrame,
        CalibrationFrameReadResult? ActiveFrame,
        RebuiltCalibration? Rebuilt,
        string? Error)
    {
        public static CalibrationRestorationResult Succeeded(
            CalibrationFrameReadResult offFrame,
            CalibrationFrameReadResult activeFrame,
            RebuiltCalibration rebuilt) => new(true, offFrame, activeFrame, rebuilt, null);

        public static CalibrationRestorationResult Failed(string error) => new(false, null, null, null, error);
    }

    private sealed record SessionLightCalibration(
        LightCalibration FoodLeft,
        LightCalibration FoodRight,
        LightCalibration? NoiseLed,
        LightId DirectFoodSide,
        LightCalibrationMode Mode);
}
