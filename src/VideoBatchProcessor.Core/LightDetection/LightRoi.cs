using VideoBatchProcessor.Core.FrameAnalyzer;

namespace VideoBatchProcessor.Core.LightDetection;

public sealed record LightRoi
{
    public LightRoi(
        LightId light,
        int x,
        int y,
        int width,
        int height,
        double threshold = 180.0,
        RoiShape shape = RoiShape.Rectangle)
    {
        if (x < 0)
            throw new ArgumentOutOfRangeException(nameof(x), "ROI X must be greater than or equal to zero.");
        if (y < 0)
            throw new ArgumentOutOfRangeException(nameof(y), "ROI Y must be greater than or equal to zero.");
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "ROI width must be greater than zero.");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "ROI height must be greater than zero.");
        if (threshold < 0)
            throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold must be greater than or equal to zero.");
        if (shape == RoiShape.Circle && width != height)
            throw new ArgumentException("Circular ROI width and height must be equal.", nameof(shape));

        Light = light;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Threshold = threshold;
        Shape = shape;
    }

    public LightId Light { get; }
    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }
    public double Threshold { get; }
    public RoiShape Shape { get; }
}
