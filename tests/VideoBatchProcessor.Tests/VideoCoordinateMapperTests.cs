using VideoBatchProcessor.Core.VideoTransform;

namespace VideoBatchProcessor.Tests;

public sealed class VideoCoordinateMapperTests
{
    [Theory]
    [InlineData(VideoRotation.None, false)]
    [InlineData(VideoRotation.None, true)]
    [InlineData(VideoRotation.UpsideDown, false)]
    [InlineData(VideoRotation.UpsideDown, true)]
    public void SourceYPreparado_SeConviertenSinPerderLaRegion(VideoRotation rotation, bool mirror)
    {
        var transform = new VideoTransformConfig
        {
            Crop = new VideoCropRect(100, 200, 1_200, 700),
            Rotation = rotation,
            MirrorHorizontally = mirror,
        };
        var source = new VideoCropRect(240, 430, 30, 30);

        var mapped = VideoCoordinateMapper.TryMapSourceToPrepared(
            source,
            transform,
            1_920,
            1_080,
            out var prepared,
            out var mapError);
        var restored = VideoCoordinateMapper.TryMapPreparedToSource(
            prepared!,
            transform,
            1_920,
            1_080,
            out var restoredSource,
            out var restoreError);

        Assert.True(mapped, mapError);
        Assert.True(restored, restoreError);
        Assert.Equal(source, restoredSource);
    }

    [Fact]
    public void PreparadoAFuente_ConCropSinTransformacionSumaElOrigenDelCrop()
    {
        var mapped = VideoCoordinateMapper.TryMapPreparedToSource(
            new VideoCropRect(139, 419, 30, 30),
            new VideoTransformConfig { Crop = new VideoCropRect(0, 135, 1_920, 830) },
            1_920,
            1_080,
            out var source,
            out var error);

        Assert.True(mapped, error);
        Assert.Equal(new VideoCropRect(139, 554, 30, 30), source);
    }
}
