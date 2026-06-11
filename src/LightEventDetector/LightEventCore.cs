// LightEventCore.cs
// Lógica de detección de luces, clasificación de eventos y generación de timeline.
// Sin dependencias de UI — funciona igual en Windows y macOS.

using OpenCvSharp;

namespace LightEventDetector;

public readonly record struct LightState(bool Left, bool Center, bool Right)
{
    public static readonly LightState AllOff = new(false, false, false);

    public override string ToString() =>
        $"L:{(Left ? "ON" : "—")} C:{(Center ? "ON" : "—")} R:{(Right ? "ON" : "—")}";
}

public enum EventType
{
    Idle,
    LeftOnly,
    CenterOnly,
    RightOnly,
    LeftToRight,
    RightToLeft,
    LeftAndCenter,
    CenterAndRight,
    LeftAndRight,
    AllOn
}

public sealed record FrameEvent(int Frame, double Timestamp, EventType Event, LightState State);
public sealed record LightROI(string Name, Rect Region, double Threshold = 180.0);

public sealed class LightDetector
{
    private readonly LightROI[] _rois;
    public IReadOnlyList<LightROI> ROIs => _rois;

    public LightDetector(LightROI left, LightROI center, LightROI right)
        => _rois = [left, center, right];

    public LightState Detect(Mat frame)
    {
        using var gray = new Mat();
        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
        var on = new bool[3];
        for (int i = 0; i < 3; i++)
        {
            using var roi = new Mat(gray, SafeRect(_rois[i].Region, frame.Size()));
            on[i] = Cv2.Mean(roi).Val0 > _rois[i].Threshold;
        }
        return new LightState(on[0], on[1], on[2]);
    }

    public double[] GetBrightnesses(Mat frame)
    {
        using var gray = new Mat();
        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
        var result = new double[_rois.Length];
        for (int i = 0; i < _rois.Length; i++)
        {
            using var roi = new Mat(gray, SafeRect(_rois[i].Region, frame.Size()));
            result[i] = Cv2.Mean(roi).Val0;
        }
        return result;
    }

    private static Rect SafeRect(Rect r, OpenCvSharp.Size s) =>
        new(Math.Max(0, r.X), Math.Max(0, r.Y),
            Math.Min(r.Width,  s.Width  - Math.Max(0, r.X)),
            Math.Min(r.Height, s.Height - Math.Max(0, r.Y)));
}

public static class EventClassifier
{
    public static EventType Classify(LightState cur, LightState prev) =>
        (cur.Left, cur.Center, cur.Right, prev.Left, prev.Center, prev.Right) switch
        {
            (false, false, false, _, _, _) => EventType.Idle,
            (false, false, true,  true,  false, false) => EventType.LeftToRight,
            (true,  false, false, false, false, true ) => EventType.RightToLeft,
            (true,  false, false, _, _, _) => EventType.LeftOnly,
            (false, true,  false, _, _, _) => EventType.CenterOnly,
            (false, false, true,  _, _, _) => EventType.RightOnly,
            (true,  true,  false, _, _, _) => EventType.LeftAndCenter,
            (false, true,  true,  _, _, _) => EventType.CenterAndRight,
            (true,  false, true,  _, _, _) => EventType.LeftAndRight,
            (true,  true,  true,  _, _, _) => EventType.AllOn
        };
}

public sealed class TimelineRenderer : IDisposable
{
    private static readonly (EventType Evt, Scalar Color, string Label)[] EventDefs =
    [
        (EventType.Idle,          new Scalar( 50,  50,  50), "Idle"),
        (EventType.LeftOnly,      new Scalar( 50, 185,  50), "Left"),
        (EventType.CenterOnly,    new Scalar( 50, 155, 230), "Center"),
        (EventType.RightOnly,     new Scalar( 50,  50, 215), "Right"),
        (EventType.LeftToRight,   new Scalar(200,  50, 200), "L→R"),
        (EventType.RightToLeft,   new Scalar(200, 200,  50), "R→L"),
        (EventType.LeftAndCenter, new Scalar( 50, 200, 150), "L+C"),
        (EventType.CenterAndRight,new Scalar(180,  50, 220), "C+R"),
        (EventType.LeftAndRight,  new Scalar(220, 120,  50), "L+R"),
        (EventType.AllOn,         new Scalar(230, 230, 230), "All")
    ];

    private static readonly Dictionary<EventType, Scalar> Colors =
        EventDefs.ToDictionary(e => e.Evt, e => e.Color);

    private const int Height = 80;
    private const int BarTop = 14;
    private const int BarH   = 36;
    private const int LegY   = 68;

    private readonly int _totalFrames;
    private readonly int _width;
    private readonly Mat _canvas;

    public TimelineRenderer(int totalFrames, int width = 1920)
    {
        _totalFrames = Math.Max(totalFrames, 1);
        _width       = width;
        _canvas      = Mat.Zeros(Height, _width, MatType.CV_8UC3);
    }

    public void PaintFrame(int frameIndex, EventType evt)
    {
        int x0 = (int)((long)frameIndex       * _width / _totalFrames);
        int x1 = (int)((long)(frameIndex + 1) * _width / _totalFrames);
        x1 = Math.Max(x1, x0 + 1);
        Cv2.Rectangle(_canvas,
            new Rect(x0, BarTop, x1 - x0, BarH),
            Colors.GetValueOrDefault(evt, new Scalar(80, 80, 80)),
            thickness: -1);
    }

    public void Save(string path, double fps)
    {
        DrawTimestamps(fps);
        DrawLegend();
        Cv2.ImWrite(path, _canvas);
    }

    private void DrawTimestamps(double fps, double intervalSecs = 5.0)
    {
        int interval = (int)(fps * intervalSecs);
        if (interval < 1) return;
        for (int f = 0; f < _totalFrames; f += interval)
        {
            int x = (int)((long)f * _width / _totalFrames);
            Cv2.Line(_canvas, new Point(x, BarTop - 7), new Point(x, BarTop), Scalar.White, 1);
            Cv2.PutText(_canvas, $"{f / fps:F0}s", new Point(x + 2, BarTop - 1),
                HersheyFonts.HersheySimplex, 0.28, Scalar.White, 1);
        }
    }

    private void DrawLegend()
    {
        int x = 8;
        var gray = new Scalar(190, 190, 190);
        foreach (var (_, color, label) in EventDefs)
        {
            Cv2.Rectangle(_canvas, new Rect(x, LegY - 8, 9, 9), color, thickness: -1);
            Cv2.PutText(_canvas, label, new Point(x + 12, LegY + 1),
                HersheyFonts.HersheySimplex, 0.30, gray, 1);
            x += 12 + label.Length * 7 + 10;
        }
    }

    public void Dispose() => _canvas.Dispose();
}

public sealed class VideoProcessor
{
    private readonly string        _inputPath;
    private readonly LightDetector _detector;
    private readonly int           _smoothWindow;

    public Action<int, string>? OnProgress;

    public VideoProcessor(string inputPath, LightDetector detector, int smoothWindow = 3)
    {
        _inputPath    = inputPath;
        _detector     = detector;
        _smoothWindow = smoothWindow;
    }

    public List<FrameEvent> Process()
    {
        using var cap = new VideoCapture(_inputPath);
        if (!cap.IsOpened())
            throw new IOException($"No se pudo abrir: {_inputPath}");

        int    total = (int)cap.Get(VideoCaptureProperties.FrameCount);
        double fps   = cap.Get(VideoCaptureProperties.Fps);
        if (fps <= 0) fps = 30.0;

        using var timeline = new TimelineRenderer(total);
        var events = new List<FrameEvent>(total);
        var buffer = new Queue<LightState>(_smoothWindow + 1);
        var frame  = new Mat();

        var prevState = LightState.AllOff;
        var prevEvent = EventType.Idle;
        int idx = 0;

        while (cap.Read(frame) && !frame.Empty())
        {
            buffer.Enqueue(_detector.Detect(frame));
            if (buffer.Count > _smoothWindow) buffer.Dequeue();
            var smoothed = MajorityVote(buffer);

            var evt = EventClassifier.Classify(smoothed, prevState);
            events.Add(new FrameEvent(idx, idx / fps, evt, smoothed));
            timeline.PaintFrame(idx, evt);

            if (evt != prevEvent)
            {
                string msg =
                    $"  {idx / fps,7:F2}s  f{idx,-6}  {prevEvent,-18} → {evt,-18}  {smoothed}";
                OnProgress?.Invoke(100 * idx / Math.Max(total, 1), msg);
            }
            else if (idx % 300 == 0)
            {
                OnProgress?.Invoke(100 * idx / Math.Max(total, 1), "");
            }

            prevState = smoothed;
            prevEvent = evt;
            idx++;
        }

        OnProgress?.Invoke(100, "");

        var dir  = Path.GetDirectoryName(Path.GetFullPath(_inputPath)) ?? ".";
        var stem = Path.GetFileNameWithoutExtension(_inputPath);

        timeline.Save(Path.Combine(dir, stem + "_timeline.png"), fps);
        ExportCsv(events, Path.Combine(dir, stem + "_events.csv"));
        frame.Dispose();
        return events;
    }

    private static LightState MajorityVote(Queue<LightState> q)
    {
        int half = q.Count / 2;
        return new LightState(
            q.Count(s => s.Left)   > half,
            q.Count(s => s.Center) > half,
            q.Count(s => s.Right)  > half);
    }

    private static void ExportCsv(List<FrameEvent> events, string path)
    {
        using var w = new StreamWriter(path);
        w.WriteLine("frame,timestamp_s,event,left,center,right");
        foreach (var e in events)
            w.WriteLine(
                $"{e.Frame},{e.Timestamp:F4},{e.Event}," +
                $"{e.State.Left},{e.State.Center},{e.State.Right}");
    }
}