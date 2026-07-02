using OpenCvSharp;
using VideoBatchProcessor.Core.FrameAnalyzer;
using Xunit;

namespace VideoBatchProcessor.Tests;

public class FrameAnalyzerTests
{
    private readonly FrameAnalyzer _analyzer = new();

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>Crea un frame negro (320x240) con rectángulos blancos donde están los LEDs.</summary>
    private static Mat MakeFrame(bool ledIzq, bool ledDer, bool ledRuido)
    {
        var frame = new Mat(240, 320, MatType.CV_8UC3, new Scalar(0, 0, 0));

        if (ledIzq)
            Cv2.Rectangle(frame, new Rect(10, 10, 30, 30), new Scalar(255, 255, 255), -1);

        if (ledDer)
            Cv2.Rectangle(frame, new Rect(280, 10, 30, 30), new Scalar(255, 255, 255), -1);

        if (ledRuido)
            Cv2.Rectangle(frame, new Rect(145, 10, 30, 30), new Scalar(255, 255, 255), -1);

        return frame;
    }

    private static readonly RoiDefinition RoiIzq = new()
    {
        Tipo = TipoLed.Izquierda,
        Label = "LED izquierdo",
        Region = new Rect(10, 10, 30, 30),
    };

    private static readonly RoiDefinition RoiDer = new()
    {
        Tipo = TipoLed.Derecha,
        Label = "LED derecho",
        Region = new Rect(280, 10, 30, 30),
    };

    private static readonly RoiDefinition RoiRuido = new()
    {
        Tipo = TipoLed.Ruido,
        Label = "LED ruido",
        Region = new Rect(145, 10, 30, 30),
    };

    private static readonly IReadOnlyList<RoiDefinition> TresRois = [RoiIzq, RoiDer, RoiRuido];

    // ── 1. Brillo: LED encendido vs apagado ──────────────────────────────

    [Fact]
    public void LedEncendido_BrilloAlto()
    {
        using var frame = MakeFrame(ledIzq: true, ledDer: false, ledRuido: false);
        var result = _analyzer.Analyze(frame, TresRois, 0, TimeSpan.Zero);

        Assert.True(result.Rois[0].MeanBrightness > 200);  // izq encendido
        Assert.True(result.Rois[1].MeanBrightness < 10);   // der apagado
        Assert.True(result.Rois[2].MeanBrightness < 10);   // ruido apagado
    }

    [Fact]
    public void LedApagado_BrilloBajo()
    {
        using var frame = MakeFrame(ledIzq: false, ledDer: false, ledRuido: false);
        var result = _analyzer.Analyze(frame, TresRois, 0, TimeSpan.Zero);

        foreach (var roi in result.Rois)
            Assert.True(roi.MeanBrightness < 10);
    }

    [Fact]
    public void TodosEncendidos_BrilloAltoEnTodos()
    {
        using var frame = MakeFrame(ledIzq: true, ledDer: true, ledRuido: true);
        var result = _analyzer.Analyze(frame, TresRois, 0, TimeSpan.Zero);

        foreach (var roi in result.Rois)
            Assert.True(roi.MeanBrightness > 200);
    }

    // ── 2. Ensayo seguro: solo LED izquierdo ─────────────────────────────

    [Fact]
    public void EnsayoSeguro_SoloLedIzquierdo()
    {
        using var frame = MakeFrame(ledIzq: true, ledDer: false, ledRuido: false);
        var result = _analyzer.Analyze(frame, TresRois, 0, TimeSpan.Zero);

        Assert.True(result.Rois[0].MeanBrightness > 200);  // izq ON
        Assert.True(result.Rois[1].MeanBrightness < 10);   // der OFF
        Assert.True(result.Rois[2].MeanBrightness < 10);   // ruido OFF
    }

    // ── 3. Ensayo peligroso: LED derecho + ruido ─────────────────────────

    [Fact]
    public void EnsayoPeligroso_LedDerechoYRuido()
    {
        using var frame = MakeFrame(ledIzq: false, ledDer: true, ledRuido: true);
        var result = _analyzer.Analyze(frame, TresRois, 0, TimeSpan.Zero);

        Assert.True(result.Rois[0].MeanBrightness < 10);   // izq OFF
        Assert.True(result.Rois[1].MeanBrightness > 200);  // der ON
        Assert.True(result.Rois[2].MeanBrightness > 200);  // ruido ON
    }

    // ── 4. Crop ──────────────────────────────────────────────────────────

    [Fact]
    public void ConCrop_DevuelveMatValido()
    {
        using var frame = MakeFrame(ledIzq: true, ledDer: false, ledRuido: false);
        var result = _analyzer.Analyze(frame, TresRois, 0, TimeSpan.Zero, includeCrop: true);

        using var crop = result.Rois[0].Crop;
        Assert.NotNull(crop);
        Assert.Equal(30, crop!.Width);
        Assert.Equal(30, crop.Height);
        Assert.False(crop.Empty());
    }

    [Fact]
    public void SinCrop_CropEsNull()
    {
        using var frame = MakeFrame(ledIzq: true, ledDer: false, ledRuido: false);
        var result = _analyzer.Analyze(frame, TresRois, 0, TimeSpan.Zero, includeCrop: false);

        Assert.Null(result.Rois[0].Crop);
    }

    [Fact]
    public void Crop_EsBrillanteCuandoLedEncendido()
    {
        using var frame = MakeFrame(ledIzq: true, ledDer: false, ledRuido: false);
        var result = _analyzer.Analyze(frame, [RoiIzq], 0, TimeSpan.Zero, includeCrop: true);

        using var crop = result.Rois[0].Crop;
        Assert.NotNull(crop);

        // Verificar que el crop es blanco (LED encendido)
        using var gray = new Mat();
        Cv2.CvtColor(crop!, gray, ColorConversionCodes.BGR2GRAY);
        var mean = Cv2.Mean(gray);
        Assert.True(mean.Val0 > 200);
    }

    // ── 5. Trazabilidad: índice y timestamp ──────────────────────────────

    [Fact]
    public void FrameIndex_SeConserva()
    {
        using var frame = MakeFrame(false, false, false);
        var result = _analyzer.Analyze(frame, TresRois, 42, TimeSpan.FromSeconds(1.4));

        Assert.Equal(42, result.FrameIndex);
        Assert.Equal(TimeSpan.FromSeconds(1.4), result.Timestamp);
    }

    // ── 6. Orden de resultados ───────────────────────────────────────────

    [Fact]
    public void Resultados_MismoOrdenQueRois()
    {
        using var frame = MakeFrame(false, false, false);
        var result = _analyzer.Analyze(frame, TresRois, 0, TimeSpan.Zero);

        Assert.Equal(3, result.Rois.Count);
        Assert.Equal(TipoLed.Izquierda, result.Rois[0].Definition.Tipo);
        Assert.Equal(TipoLed.Derecha,   result.Rois[1].Definition.Tipo);
        Assert.Equal(TipoLed.Ruido,     result.Rois[2].Definition.Tipo);
    }

    // ── 7. AnalyzeSingle ─────────────────────────────────────────────────

    [Fact]
    public void AnalyzeSingle_DevuelveUnResultado()
    {
        using var frame = MakeFrame(ledIzq: true, ledDer: false, ledRuido: false);
        var result = _analyzer.AnalyzeSingle(frame, RoiIzq);

        Assert.True(result.MeanBrightness > 200);
        Assert.Equal(TipoLed.Izquierda, result.Definition.Tipo);
    }

    // ── 8. ROI fuera de rango ────────────────────────────────────────────

    [Fact]
    public void RoiFueraDeRango_BrilloCero()
    {
        using var frame = MakeFrame(false, false, false);
        var roiFuera = new RoiDefinition
        {
            Tipo = TipoLed.Izquierda,
            Label = "fuera",
            Region = new Rect(999, 999, 30, 30),
        };

        var result = _analyzer.Analyze(frame, [roiFuera], 0, TimeSpan.Zero);

        Assert.Equal(0, result.Rois[0].MeanBrightness);
        Assert.Null(result.Rois[0].Crop);
    }

    [Fact]
    public void RoiParcialmenteFuera_SeRecorta()
    {
        using var frame = MakeFrame(false, false, false);
        // ROI que sale del borde derecho (320px de ancho, ROI empieza en 310)
        var roiParcial = new RoiDefinition
        {
            Tipo = TipoLed.Derecha,
            Label = "parcial",
            Region = new Rect(310, 10, 30, 30),
        };

        var result = _analyzer.Analyze(frame, [roiParcial], 0, TimeSpan.Zero, includeCrop: true);

        using var crop = result.Rois[0].Crop;
        Assert.NotNull(crop);
        Assert.Equal(10, crop!.Width);  // recortado a 10px (320-310)
        Assert.Equal(30, crop.Height);  // alto sin cambio
    }

    // ── 9. ValidateRois ──────────────────────────────────────────────────

    [Fact]
    public void ValidateRois_TodosDentro_ListaVacia()
    {
        var invalid = FrameAnalyzer.ValidateRois(TresRois, 320, 240);
        Assert.Empty(invalid);
    }

    [Fact]
    public void ValidateRois_UnoFuera_LoReporta()
    {
        var rois = new List<RoiDefinition>
        {
            RoiIzq,
            new() { Tipo = TipoLed.Derecha, Label = "fuera", Region = new Rect(999, 999, 30, 30) },
        };

        var invalid = FrameAnalyzer.ValidateRois(rois, 320, 240);
        Assert.Single(invalid);
        Assert.Equal(TipoLed.Derecha, invalid[0].Tipo);
    }

    // ── 10. Validación de entrada ────────────────────────────────────────

    [Fact]
    public void FrameNulo_LanzaExcepcion()
    {
        Assert.Throws<ArgumentException>(() =>
            _analyzer.Analyze(null!, TresRois, 0, TimeSpan.Zero));
    }

    [Fact]
    public void FrameVacio_LanzaExcepcion()
    {
        using var empty = new Mat();
        Assert.Throws<ArgumentException>(() =>
            _analyzer.Analyze(empty, TresRois, 0, TimeSpan.Zero));
    }

    [Fact]
    public void RoisVacios_LanzaExcepcion()
    {
        using var frame = MakeFrame(false, false, false);
        Assert.Throws<ArgumentException>(() =>
            _analyzer.Analyze(frame, [], 0, TimeSpan.Zero));
    }
}