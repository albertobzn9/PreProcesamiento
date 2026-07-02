using OpenCvSharp;

namespace VideoBatchProcessor.Core.FrameAnalyzer;

/// <summary>
/// Tipo de LED en la caja CMC.
/// <list type="bullet">
///   <item><b>Izquierda</b> — luz de comida del lado izquierdo.
///         Indica que la rata puede palanquear de ese lado.</item>
///   <item><b>Derecha</b> — luz de comida del lado derecho.
///         Indica que la rata puede palanquear de ese lado.</item>
///   <item><b>Ruido</b> — LED de ruido blanco. Se enciende junto con
///         Izquierda o Derecha para señalar un ensayo peligroso/de riesgo.</item>
/// </list>
/// Regla: Izquierda y Derecha nunca están encendidas al mismo tiempo.
/// Ruido puede encenderse junto con cualquiera de las dos.
/// </summary>
public enum TipoLed
{
    Izquierda,
    Derecha,
    Ruido
}

/// <summary>
/// Define una región de interés (ROI) sobre el video, asociada a un LED.
/// El rectángulo lo marca el usuario en la UI sobre un frame de referencia.
/// Una vez definido, se aplica igual a todos los frames del video.
/// </summary>
public sealed record RoiDefinition
{
    /// <summary>Tipo de LED que esta ROI monitorea.</summary>
    public TipoLed Tipo { get; init; }

    /// <summary>
    /// Etiqueta legible. Ejemplo: "LED izquierdo", "LED ruido".
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// Rectángulo en coordenadas del frame (píxeles).
    /// X,Y = esquina superior izquierda. Width,Height = dimensiones.
    /// </summary>
    public Rect Region { get; init; }
}

/// <summary>
/// Resultado del análisis de un ROI sobre un frame específico.
/// </summary>
public sealed record RoiAnalysisResult
{
    /// <summary>Definición del ROI que se analizó.</summary>
    public RoiDefinition Definition { get; init; } = null!;

    /// <summary>
    /// Brillo promedio del ROI en escala de grises (0–255).
    /// 0 = completamente oscuro, 255 = completamente brillante.
    /// </summary>
    public double MeanBrightness { get; init; }

    /// <summary>
    /// Crop del ROI como imagen independiente (BGR, misma profundidad que el frame original).
    /// El caller es dueño de este Mat y debe hacer Dispose cuando ya no lo necesite.
    /// Null si se solicitó análisis sin crop.
    /// </summary>
    public Mat? Crop { get; init; }
}

/// <summary>
/// Resultado completo del análisis de un frame: métricas de todos los ROIs.
/// </summary>
public sealed record FrameAnalysisResult
{
    /// <summary>Índice del frame analizado (0-based).</summary>
    public long FrameIndex { get; init; }

    /// <summary>Timestamp del frame en el video.</summary>
    public TimeSpan Timestamp { get; init; }

    /// <summary>Resultados por cada ROI, en el mismo orden que se pasaron las definiciones.</summary>
    public IReadOnlyList<RoiAnalysisResult> Rois { get; init; } = [];
}