using OpenCvSharp;
using VideoBatchProcessor.Core.VideoReader;
using VideoBatchProcessor.Core.VideoTransform;

namespace VideoBatchProcessor.Tests;

public class VideoTransformFrameTests
{
    [Fact]
    public void TryTransformFrame_AplicaElMismoEspejoQueElPreview()
    {
        const int width = 80;
        const int height = 40;
        using var source = new Mat(height, width, MatType.CV_8UC3, Scalar.Black);
        using (var leftHalf = source[new Rect(0, 0, width / 2, height)])
            leftHalf.SetTo(new Scalar(0, 0, 255));
        using (var rightHalf = source[new Rect(width / 2, 0, width / 2, height)])
            rightHalf.SetTo(new Scalar(0, 255, 0));

        var metadata = new VideoMetadata { Width = width, Height = height };
        var ok = VideoTransformPreviewRenderer.TryTransformFrame(
            source,
            metadata,
            new VideoTransformConfig { MirrorHorizontally = true },
            out var transformed,
            out var error);

        Assert.True(ok);
        Assert.Null(error);
        using (transformed)
        {
            Assert.NotNull(transformed);
            using var outputLeft = transformed![new Rect(0, 0, width / 2, height)];
            using var outputRight = transformed[new Rect(width / 2, 0, width / 2, height)];
            Assert.True(Cv2.Mean(outputLeft).Val1 > 180);
            Assert.True(Cv2.Mean(outputRight).Val2 > 180);
        }
    }

    [Fact]
    public void TryEncodePreview_ReduceSinModificarLasDimensionesDelFrameOrigen()
    {
        using var source = new Mat(200, 400, MatType.CV_8UC3, Scalar.White);

        var ok = VideoTransformPreviewRenderer.TryEncodePreview(source, out var preview, out var error, maxDimension: 100);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(preview);
        Assert.Equal(100, preview!.Width);
        Assert.Equal(50, preview.Height);
        Assert.Equal(400, source.Width);
        Assert.Equal(200, source.Height);
    }
}
