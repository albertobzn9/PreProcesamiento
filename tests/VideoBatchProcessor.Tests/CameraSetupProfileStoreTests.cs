using VideoBatchProcessor.Core.CameraProfiles;
using VideoBatchProcessor.Core.FrameAnalyzer;
using VideoBatchProcessor.Core.LightDetection;
using VideoBatchProcessor.Core.VideoTransform;

namespace VideoBatchProcessor.Tests;

public sealed class CameraSetupProfileStoreTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("vbp-camera-profile-").FullName;

    [Fact]
    public void TrySaveYTryLoad_ConservanTransformacionYLasTresRois()
    {
        var store = new CameraSetupProfileStore(_directory);
        var video = Path.Combine(_directory, "exp_0526_cs_d4r4.mp4");
        var transform = new VideoTransformConfig
        {
            Rotation = VideoRotation.UpsideDown,
            MirrorHorizontally = true,
            Crop = new VideoCropRect(100, 200, 1200, 700),
        };
        var lights = new LightDetectionConfig(
            new LightRoi(LightId.FoodLeft, 20, 30, 16, 16, 111, RoiShape.Circle),
            new LightRoi(LightId.FoodRight, 40, 50, 18, 18, 112, RoiShape.Circle),
            new LightRoi(LightId.NoiseLed, 60, 70, 20, 20, 113, RoiShape.Circle));

        var saved = store.TrySave(video, 1920, 1080, transform, lights, out var saveError);
        var loaded = store.TryLoad(video, 1920, 1080, out var profile, out var loadError);

        Assert.True(saved, saveError);
        Assert.True(loaded, loadError);
        Assert.NotNull(profile);
        Assert.Equal(transform, profile.Transform);
        Assert.Equal(lights.FoodLeft, profile.Lights.FoodLeft);
        Assert.Equal(lights.FoodRight, profile.Lights.FoodRight);
        Assert.Equal(lights.NoiseLed, profile.Lights.NoiseLed);
        var storedJson = File.ReadAllText(Directory.EnumerateFiles(_directory, "*.json").Single());
        Assert.Contains("\"coordinateSpace\": \"source\"", storedJson);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
