using OpenCvSharp;
using VideoBatchProcessor.Core.FrameAnalyzer;

namespace VideoBatchProcessor.Core.LightDetection;

/// <summary>
/// Mide las tres ROIs reutilizando buffers y máscaras entre frames. Mantiene
/// la misma medición de brillo que <see cref="FrameAnalyzerBrightnessSource"/>,
/// pero evita reconstruir analizadores, listas y máscaras en cada frame.
/// </summary>
public sealed class ReusableFrameBrightnessSource : IFrameBrightnessSource, IDisposable
{
    private readonly RoiSampler _foodLeft;
    private readonly RoiSampler _foodRight;
    private readonly RoiSampler _noiseLed;
    private bool _hasMeasurement;

    public ReusableFrameBrightnessSource(LightDetectionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _foodLeft = new RoiSampler(config.FoodLeft);
        _foodRight = new RoiSampler(config.FoodRight);
        _noiseLed = new RoiSampler(config.NoiseLed);
    }

    public void Update(Mat frame)
    {
        if (frame is null || frame.Empty())
            throw new ArgumentException("El frame está vacío o es nulo.", nameof(frame));

        _foodLeft.Update(frame);
        _foodRight.Update(frame);
        _noiseLed.Update(frame);
        _hasMeasurement = true;
    }

    public double GetMeanBrightness(LightRoi roi)
    {
        ArgumentNullException.ThrowIfNull(roi);
        if (!_hasMeasurement)
            throw new InvalidOperationException("Debe medir un frame antes de consultar su brillo.");

        return roi.Light switch
        {
            LightId.FoodLeft => _foodLeft.Brightness,
            LightId.FoodRight => _foodRight.Brightness,
            LightId.NoiseLed => _noiseLed.Brightness,
            _ => throw new ArgumentException("La ROI no pertenece a esta fuente de brillo.", nameof(roi)),
        };
    }

    public void Dispose()
    {
        _foodLeft.Dispose();
        _foodRight.Dispose();
        _noiseLed.Dispose();
    }

    private sealed class RoiSampler : IDisposable
    {
        private readonly LightRoi _roi;
        private readonly Mat _gray = new();
        private Mat? _mask;
        private Rect _clipped;
        private int _frameWidth = -1;
        private int _frameHeight = -1;

        public RoiSampler(LightRoi roi)
        {
            _roi = roi;
        }

        public double Brightness { get; private set; }

        public void Update(Mat frame)
        {
            EnsureGeometry(frame.Width, frame.Height);
            if (_clipped.Width <= 0 || _clipped.Height <= 0)
            {
                Brightness = 0;
                return;
            }

            using var subMat = frame[_clipped];
            if (subMat.Channels() == 1)
                subMat.CopyTo(_gray);
            else
                Cv2.CvtColor(subMat, _gray, ColorConversionCodes.BGR2GRAY);

            Brightness = _mask is null
                ? Cv2.Mean(_gray).Val0
                : Cv2.Mean(_gray, _mask).Val0;
        }

        public void Dispose()
        {
            _mask?.Dispose();
            _gray.Dispose();
        }

        private void EnsureGeometry(int frameWidth, int frameHeight)
        {
            if (_frameWidth == frameWidth && _frameHeight == frameHeight)
                return;

            _frameWidth = frameWidth;
            _frameHeight = frameHeight;
            var x1 = Math.Max(0, _roi.X);
            var y1 = Math.Max(0, _roi.Y);
            var x2 = Math.Min(frameWidth, _roi.X + _roi.Width);
            var y2 = Math.Min(frameHeight, _roi.Y + _roi.Height);
            _clipped = x2 <= x1 || y2 <= y1
                ? new Rect(0, 0, 0, 0)
                : new Rect(x1, y1, x2 - x1, y2 - y1);

            _mask?.Dispose();
            _mask = null;
            if (_roi.Shape != RoiShape.Circle || _clipped.Width <= 0 || _clipped.Height <= 0)
                return;

            _mask = new Mat(_clipped.Height, _clipped.Width, MatType.CV_8UC1, Scalar.Black);
            var radius = Math.Max(1, Math.Min(_clipped.Width, _clipped.Height) / 2);
            Cv2.Circle(
                _mask,
                new Point(_clipped.Width / 2, _clipped.Height / 2),
                radius,
                Scalar.White,
                -1);
        }
    }
}
