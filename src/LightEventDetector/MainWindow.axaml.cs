using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using OpenCvSharp;

namespace LightEventDetector;

public partial class MainWindow : Avalonia.Controls.Window
{
    private VideoCapture? _cap;
    private string        _videoPath   = "";
    private int           _frameIdx;
    private int           _totalFrames;
    private double        _fps         = 30.0;
    private bool          _isProcessing;

    //dimensiones reales del video para el calculo del mouse
    private int           _vidWidth    = 1;
    private int           _vidHeight   = 1;

    //variables de estado para dibujar
    private bool           _isDragging;
    private Avalonia.Point _dragStartUi;
    private bool           _isUpdatingFromMouse;

    private readonly Mat _rawFrame  = new();
    private readonly Mat _display   = new();

    private static readonly Scalar[] RoiColors =
    [
        new Scalar( 50, 210,  50),
        new Scalar( 50, 150, 230),
        new Scalar( 50,  50, 220),
    ];

    public MainWindow()
    {
        InitializeComponent();

        KeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Avalonia.Input.Key.Left:  ShowFrame(_frameIdx - 1);              break;
                case Avalonia.Input.Key.Right: ShowFrame(_frameIdx + 1);              break;
                case Avalonia.Input.Key.Down:  ShowFrame(_frameIdx - (int)_fps);      break;
                case Avalonia.Input.Key.Up:    ShowFrame(_frameIdx + (int)_fps);      break;
            }
        };
    }

    private LightDetector BuildDetector() => new LightDetector(
        left:   new LightROI("Izquierda",
            new Rect((int)(RoiLX.Value ?? 0), (int)(RoiLY.Value ?? 0),
                     Math.Max(1,(int)(RoiLW.Value ?? 1)), Math.Max(1,(int)(RoiLH.Value ?? 1))),
            (double)(RoiLT.Value ?? 180m)),
        center: new LightROI("Centro",
            new Rect((int)(RoiCX.Value ?? 0), (int)(RoiCY.Value ?? 0),
                     Math.Max(1,(int)(RoiCW.Value ?? 1)), Math.Max(1,(int)(RoiCH.Value ?? 1))),
            (double)(RoiCT.Value ?? 180m)),
        right:  new LightROI("Derecha",
            new Rect((int)(RoiRX.Value ?? 0), (int)(RoiRY.Value ?? 0),
                     Math.Max(1,(int)(RoiRW.Value ?? 1)), Math.Max(1,(int)(RoiRH.Value ?? 1))),
            (double)(RoiRT.Value ?? 180m))
    );

    private void ShowFrame(int idx)
    {
        if (_cap is null || !_cap.IsOpened()) return;

        _frameIdx = Math.Clamp(idx, 0, Math.Max(0, _totalFrames - 1));
        _cap.Set(VideoCaptureProperties.PosFrames, _frameIdx);
        if (!_cap.Read(_rawFrame) || _rawFrame.Empty()) return;

        _rawFrame.CopyTo(_display);

        var detector     = BuildDetector();
        var brightnesses = detector.GetBrightnesses(_rawFrame);
        var state        = detector.Detect(_rawFrame);

        for (int i = 0; i < detector.ROIs.Count; i++)
        {
            bool isOn = brightnesses[i] > detector.ROIs[i].Threshold;
            var  roi  = detector.ROIs[i];
            Cv2.Rectangle(_display, roi.Region, RoiColors[i], isOn ? 2 : 1);
            Cv2.PutText(_display,
                $"{roi.Name}: {brightnesses[i]:F0} {(isOn ? "ON" : "OFF")}",
                new Point(roi.Region.X, Math.Max(roi.Region.Y - 5, 14)),
                HersheyFonts.HersheySimplex, 0.48, RoiColors[i], 1);
        }

        ImgFrame.Source = MatToBitmap(_display);
        TxtFrame.Text   = $"Frame {_frameIdx} / {_totalFrames - 1}  ({_frameIdx / _fps:F1} s)";

        TxtLBright.Text = FormatBrightness(brightnesses[0], detector.ROIs[0].Threshold);
        TxtCBright.Text = FormatBrightness(brightnesses[1], detector.ROIs[1].Threshold);
        TxtRBright.Text = FormatBrightness(brightnesses[2], detector.ROIs[2].Threshold);

        IndLeft.Background   = state.Left   ? Brushes.LimeGreen               : Brushes.Gray;
        IndCenter.Background = state.Center ? new SolidColorBrush(Color.Parse("#E07020")) : Brushes.Gray;
        IndRight.Background  = state.Right  ? Brushes.OrangeRed                : Brushes.Gray;

        TxtEvent.Text = EventClassifier.Classify(state, LightState.AllOff).ToString();
    }

    private static string FormatBrightness(double brightness, double threshold)
        => $"Brillo: {brightness:F1}  (umbral {threshold:F0})  {(brightness > threshold ? "→ ON" : "→ OFF")}";

    private static Bitmap MatToBitmap(Mat mat)
    {
        Cv2.ImEncode(".png", mat, out var bytes);
        using var ms = new MemoryStream(bytes);
        return new Bitmap(ms);
    }

    private void AppendLog(string line) =>
        Dispatcher.UIThread.Post(() =>
        {
            TxtLog.Text += line + "\n";
            TxtLog.CaretIndex = TxtLog.Text?.Length ?? 0;
        });

    private async void OpenVideo_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this)!;
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title         = "Abrir video",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Video")
                    {
                        Patterns = ["*.mp4", "*.avi", "*.mov", "*.mkv", "*.m4v"]
                    }
                ]
            });

        if (files.Count == 0) return;

        _videoPath = files[0].Path.LocalPath;
        TxtVideoPath.Text = Path.GetFileName(_videoPath);

        _cap?.Dispose();
        _cap = new VideoCapture(_videoPath);

        if (!_cap.IsOpened())
        {
            AppendLog($"ERROR: no se pudo abrir {_videoPath}");
            return;
        }

        _totalFrames = (int)_cap.Get(VideoCaptureProperties.FrameCount);
        _fps         = _cap.Get(VideoCaptureProperties.Fps);
        _vidWidth    = (int)_cap.Get(VideoCaptureProperties.FrameWidth);
        _vidHeight   = (int)_cap.Get(VideoCaptureProperties.FrameHeight);

        if (_fps <= 0) _fps = 30.0;

        AppendLog($"Abierto: {_videoPath}");
        AppendLog($"Resolucion: {_vidWidth}x{_vidHeight}");
        AppendLog($"Frames: {_totalFrames}   FPS: {_fps:F2}   Duración: {_totalFrames / _fps:F1}s");

        ShowFrame(0);
    }

    private void Prev_Click(object? sender, RoutedEventArgs e)    => ShowFrame(_frameIdx - 1);
    private void Next_Click(object? sender, RoutedEventArgs e)    => ShowFrame(_frameIdx + 1);
    private void PrevBig_Click(object? sender, RoutedEventArgs e) => ShowFrame(_frameIdx - (int)_fps);
    private void NextBig_Click(object? sender, RoutedEventArgs e) => ShowFrame(_frameIdx + (int)_fps);

    private void RoiChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        //evitamos que el numericupdown redibuje el frame si estamos arrastrando el mouse
        if (!_isUpdatingFromMouse) ShowFrame(_frameIdx);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  LOGICA DE DIBUJO CON MOUSE
    // ══════════════════════════════════════════════════════════════════════════

    private void ImgFrame_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (_cap is null || !_cap.IsOpened()) return;
        var point = e.GetCurrentPoint(ImgFrame);
        if (!point.Properties.IsLeftButtonPressed) return;

        _isDragging = true;
        _dragStartUi = point.Position;
        e.Handled = true;
    }

    private void ImgFrame_PointerMoved(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (!_isDragging) return;
        var currentUi = e.GetCurrentPoint(ImgFrame).Position;
        UpdateSelectedRoi(_dragStartUi, currentUi);
    }

    private void ImgFrame_PointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        var currentUi = e.GetCurrentPoint(ImgFrame).Position;
        UpdateSelectedRoi(_dragStartUi, currentUi);
    }

    //traduce coordenadas de la interfaz de usuario al tamaño real del fotograma de video
    private (int X, int Y) TranslateUiToVideo(Avalonia.Point uiPoint)
    {
        var imgBounds = ImgFrame.Bounds;
        if (imgBounds.Width <= 0 || imgBounds.Height <= 0 || _vidWidth <= 0) return (0, 0);

        double scaleX = imgBounds.Width / _vidWidth;
        double scaleY = imgBounds.Height / _vidHeight;
        double scale = Math.Min(scaleX, scaleY);

        double renderW = _vidWidth * scale;
        double renderH = _vidHeight * scale;

        double offsetX = (imgBounds.Width - renderW) / 2.0;
        double offsetY = (imgBounds.Height - renderH) / 2.0;

        int vidX = (int)((uiPoint.X - offsetX) / scale);
        int vidY = (int)((uiPoint.Y - offsetY) / scale);

        vidX = Math.Clamp(vidX, 0, _vidWidth - 1);
        vidY = Math.Clamp(vidY, 0, _vidHeight - 1);

        return (vidX, vidY);
    }

    private void UpdateSelectedRoi(Avalonia.Point startUi, Avalonia.Point endUi)
    {
        var p1 = TranslateUiToVideo(startUi);
        var p2 = TranslateUiToVideo(endUi);

        int x = Math.Min(p1.X, p2.X);
        int y = Math.Min(p1.Y, p2.Y);
        int w = Math.Max(1, Math.Abs(p1.X - p2.X));
        int h = Math.Max(1, Math.Abs(p1.Y - p2.Y));

        _isUpdatingFromMouse = true;

        if (RadLeft.IsChecked == true)
        {
            RoiLX.Value = x; RoiLY.Value = y; RoiLW.Value = w; RoiLH.Value = h;
        }
        else if (RadCenter.IsChecked == true)
        {
            RoiCX.Value = x; RoiCY.Value = y; RoiCW.Value = w; RoiCH.Value = h;
        }
        else if (RadRight.IsChecked == true)
        {
            RoiRX.Value = x; RoiRY.Value = y; RoiRW.Value = w; RoiRH.Value = h;
        }

        _isUpdatingFromMouse = false;
        ShowFrame(_frameIdx);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  PROCESAMIENTO ASINCRONO
    // ══════════════════════════════════════════════════════════════════════════

    private async void Process_Click(object? sender, RoutedEventArgs e)
    {
        if (_cap is null || _isProcessing || string.IsNullOrEmpty(_videoPath)) return;

        _isProcessing      = true;
        BtnProcess.IsEnabled = false;
        TxtLog.Text        = "";
        PrgProcess.Value   = 0;
        TxtStatus.Text     = "Procesando…";

        var detector  = BuildDetector();
        var processor = new VideoProcessor(_videoPath, detector, smoothWindow: 3);

        processor.OnProgress = (pct, msg) =>
            Dispatcher.UIThread.Post(() =>
            {
                PrgProcess.Value = pct;
                TxtStatus.Text   = $"{pct}%";
                if (!string.IsNullOrEmpty(msg)) AppendLog(msg);
            });

        await Task.Run(processor.Process);

        TxtStatus.Text       = "¡Listo!";
        BtnProcess.IsEnabled = true;
        _isProcessing        = false;

        AppendLog($"\nArchivos generados junto al video:");
        AppendLog($"  → {Path.GetFileNameWithoutExtension(_videoPath)}_timeline.png");
        AppendLog($"  → {Path.GetFileNameWithoutExtension(_videoPath)}_events.csv");
    }
}