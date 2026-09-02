using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VideoBatchProcessor.Core.FrameAnalyzer;
using VideoBatchProcessor.Core.LightDetection;
using VideoBatchProcessor.Core.VideoTransform;

namespace VideoBatchProcessor.Core.CameraProfiles;

/// <summary>
/// Conserva la preparación visual de un video en datos locales de la
/// aplicación. No modifica el archivo fuente ni requiere escribir junto a él.
/// </summary>
public sealed class CameraSetupProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _directory;

    public CameraSetupProfileStore(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VideoBatchProcessor",
            "camera-profiles");
    }

    public bool TryLoad(
        string videoPath,
        int sourceWidth,
        int sourceHeight,
        out SavedCameraSetupProfile? profile,
        out string? error)
    {
        profile = null;
        error = null;
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            error = "El video no tiene dimensiones válidas para recuperar su perfil de cámara.";
            return false;
        }

        var path = GetProfilePath(videoPath);
        if (!File.Exists(path))
            return true;

        try
        {
            var saved = JsonSerializer.Deserialize<PersistedProfile>(File.ReadAllText(path), JsonOptions);
            if (saved is null || !PathsMatch(saved.VideoPath, videoPath))
            {
                error = "El perfil guardado no corresponde a este video.";
                return false;
            }

            var transform = ToTransform(saved);
            var storedLights = ToLightConfig(saved);
            LightDetectionConfig preparedLights;

            if (saved.CoordinateSpace == "source" && saved.SchemaVersion >= 2)
            {
                if (saved.SourceWidth != sourceWidth || saved.SourceHeight != sourceHeight)
                {
                    error = "El perfil guardado usa dimensiones distintas a las del video fuente actual.";
                    return false;
                }

                if (!TryMapLightsToPrepared(storedLights, transform, sourceWidth, sourceHeight, out var mappedLights, out error))
                    return false;

                preparedLights = mappedLights!;
            }
            else
            {
                // Version 1 stored ROIs relative to the prepared frame. Migrate it on load.
                preparedLights = storedLights;
                if (!TryMapLightsToSource(preparedLights, transform, sourceWidth, sourceHeight, out var sourceLights, out error))
                    return false;

                WriteProfile(path, CreatePersistedProfile(videoPath, sourceWidth, sourceHeight, transform, sourceLights!));
            }

            profile = new SavedCameraSetupProfile(transform, preparedLights);
            return true;
        }
        catch (Exception exception) when (exception is IOException or JsonException or ArgumentException or InvalidOperationException)
        {
            error = $"No se pudo recuperar el perfil de cámara: {exception.Message}";
            return false;
        }
    }

    public bool TrySave(
        string videoPath,
        int sourceWidth,
        int sourceHeight,
        VideoTransformConfig transform,
        LightDetectionConfig lights,
        out string? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoPath);
        ArgumentNullException.ThrowIfNull(transform);
        ArgumentNullException.ThrowIfNull(lights);
        error = null;

        try
        {
            if (!TryMapLightsToSource(lights, transform, sourceWidth, sourceHeight, out var sourceLights, out error))
                return false;

            Directory.CreateDirectory(_directory);
            var path = GetProfilePath(videoPath);
            WriteProfile(path, CreatePersistedProfile(videoPath, sourceWidth, sourceHeight, transform, sourceLights!));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = $"No se pudo guardar el perfil de cámara: {exception.Message}";
            return false;
        }
    }

    public void Delete(string videoPath)
    {
        var path = GetProfilePath(videoPath);
        if (File.Exists(path))
            File.Delete(path);
    }

    private string GetProfilePath(string videoPath)
    {
        var canonicalPath = Path.GetFullPath(videoPath);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPath)));
        return Path.Combine(_directory, hash + ".json");
    }

    private static bool PathsMatch(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.Ordinal);

    private static VideoTransformConfig ToTransform(PersistedProfile profile) =>
        new()
        {
            Rotation = profile.Rotation,
            MirrorHorizontally = profile.MirrorHorizontally,
            Crop = profile.Crop is null ? null : new VideoCropRect(profile.Crop.X, profile.Crop.Y, profile.Crop.Width, profile.Crop.Height),
        };

    private static LightDetectionConfig ToLightConfig(PersistedProfile profile) =>
        new(
            ToLightRoi(profile.FoodLeft, LightId.FoodLeft),
            ToLightRoi(profile.FoodRight, LightId.FoodRight),
            ToLightRoi(profile.NoiseLed, LightId.NoiseLed));

    private static PersistedProfile CreatePersistedProfile(
        string videoPath,
        int sourceWidth,
        int sourceHeight,
        VideoTransformConfig transform,
        LightDetectionConfig sourceLights) =>
        new(
            SchemaVersion: 2,
            CoordinateSpace: "source",
            VideoPath: Path.GetFullPath(videoPath),
            SourceWidth: sourceWidth,
            SourceHeight: sourceHeight,
            Rotation: transform.Rotation,
            MirrorHorizontally: transform.MirrorHorizontally,
            Crop: transform.Crop is null ? null : new PersistedCrop(transform.Crop.X, transform.Crop.Y, transform.Crop.Width, transform.Crop.Height),
            FoodLeft: ToPersistedRoi(sourceLights.FoodLeft),
            FoodRight: ToPersistedRoi(sourceLights.FoodRight),
            NoiseLed: ToPersistedRoi(sourceLights.NoiseLed));

    private static bool TryMapLightsToSource(
        LightDetectionConfig preparedLights,
        VideoTransformConfig transform,
        int sourceWidth,
        int sourceHeight,
        out LightDetectionConfig? sourceLights,
        out string? error) =>
        TryMapLights(
            preparedLights,
            (LightRoi roi, out LightRoi? mapped, out string? mapError) => TryMapPreparedRoiToSource(roi, transform, sourceWidth, sourceHeight, out mapped, out mapError),
            out sourceLights,
            out error);

    private static bool TryMapLightsToPrepared(
        LightDetectionConfig sourceLights,
        VideoTransformConfig transform,
        int sourceWidth,
        int sourceHeight,
        out LightDetectionConfig? preparedLights,
        out string? error) =>
        TryMapLights(
            sourceLights,
            (LightRoi roi, out LightRoi? mapped, out string? mapError) => TryMapSourceRoiToPrepared(roi, transform, sourceWidth, sourceHeight, out mapped, out mapError),
            out preparedLights,
            out error);

    private delegate bool RoiMapper(LightRoi roi, out LightRoi? mapped, out string? error);

    private static bool TryMapLights(
        LightDetectionConfig lights,
        RoiMapper mapper,
        out LightDetectionConfig? mappedLights,
        out string? error)
    {
        mappedLights = null;
        error = null;
        if (!mapper(lights.FoodLeft, out var foodLeft, out error) ||
            !mapper(lights.FoodRight, out var foodRight, out error) ||
            !mapper(lights.NoiseLed, out var noiseLed, out error))
        {
            return false;
        }

        mappedLights = new LightDetectionConfig(foodLeft!, foodRight!, noiseLed!);
        return true;
    }

    private static bool TryMapPreparedRoiToSource(
        LightRoi prepared,
        VideoTransformConfig transform,
        int sourceWidth,
        int sourceHeight,
        out LightRoi? source,
        out string? error)
    {
        source = null;
        if (!VideoCoordinateMapper.TryMapPreparedToSource(
                new VideoCropRect(prepared.X, prepared.Y, prepared.Width, prepared.Height),
                transform,
                sourceWidth,
                sourceHeight,
                out var mapped,
                out error))
        {
            return false;
        }

        source = new LightRoi(prepared.Light, mapped!.X, mapped.Y, mapped.Width, mapped.Height, prepared.Threshold, prepared.Shape);
        return true;
    }

    private static bool TryMapSourceRoiToPrepared(
        LightRoi source,
        VideoTransformConfig transform,
        int sourceWidth,
        int sourceHeight,
        out LightRoi? prepared,
        out string? error)
    {
        prepared = null;
        if (!VideoCoordinateMapper.TryMapSourceToPrepared(
                new VideoCropRect(source.X, source.Y, source.Width, source.Height),
                transform,
                sourceWidth,
                sourceHeight,
                out var mapped,
                out error))
        {
            return false;
        }

        prepared = new LightRoi(source.Light, mapped!.X, mapped.Y, mapped.Width, mapped.Height, source.Threshold, source.Shape);
        return true;
    }

    private static void WriteProfile(string path, PersistedProfile profile)
    {
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(profile, JsonOptions));
        File.Move(temporaryPath, path, overwrite: true);
    }

    private static PersistedRoi ToPersistedRoi(LightRoi roi) => new(roi.X, roi.Y, roi.Width, roi.Height, roi.Threshold, roi.Shape);

    private static LightRoi ToLightRoi(PersistedRoi roi, LightId light) =>
        new(light, roi.X, roi.Y, roi.Width, roi.Height, roi.Threshold, roi.Shape);

    private sealed record PersistedProfile(
        int SchemaVersion,
        string? CoordinateSpace,
        string VideoPath,
        int SourceWidth,
        int SourceHeight,
        VideoRotation Rotation,
        bool MirrorHorizontally,
        PersistedCrop? Crop,
        PersistedRoi FoodLeft,
        PersistedRoi FoodRight,
        PersistedRoi NoiseLed);

    private sealed record PersistedCrop(int X, int Y, int Width, int Height);

    private sealed record PersistedRoi(int X, int Y, int Width, int Height, double Threshold, RoiShape Shape);
}

public sealed record SavedCameraSetupProfile(
    VideoTransformConfig Transform,
    LightDetectionConfig Lights);
