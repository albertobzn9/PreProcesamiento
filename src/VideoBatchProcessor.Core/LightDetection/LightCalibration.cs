namespace VideoBatchProcessor.Core.LightDetection;

/// <summary>
/// Una evidencia visual escogida por el investigador para calibrar una luz.
/// El brillo siempre se mide sobre el frame preparado, no sobre el JPEG que se
/// muestra en la interfaz.
/// </summary>
public sealed record LightCalibrationReference(
    long FrameIndex,
    TimeSpan EstimatedTimestamp,
    double Brightness);

/// <summary>
/// Referencias OFF/ON y umbral derivado para una luz. La primera versión usa
/// una referencia de cada tipo, pero el modelo permite añadir más y calcula
/// medianas para no tener que rediseñarlo después.
/// </summary>
public sealed record LightCalibration
{
    public LightCalibration(
        LightId light,
        IReadOnlyList<LightCalibrationReference> offReferences,
        IReadOnlyList<LightCalibrationReference> onReferences,
        bool usesSharedFoodReference = false)
    {
        ArgumentNullException.ThrowIfNull(offReferences);
        ArgumentNullException.ThrowIfNull(onReferences);
        if (offReferences.Count == 0)
            throw new ArgumentException("Se requiere al menos una referencia OFF.", nameof(offReferences));
        if (onReferences.Count == 0)
            throw new ArgumentException("Se requiere al menos una referencia ON.", nameof(onReferences));

        Light = light;
        OffReferences = offReferences.ToArray();
        OnReferences = onReferences.ToArray();
        OffMedianBrightness = Median(OffReferences.Select(reference => reference.Brightness));
        OnMedianBrightness = Median(OnReferences.Select(reference => reference.Brightness));
        if (OnMedianBrightness <= OffMedianBrightness)
            throw new ArgumentException("La referencia ON debe ser más brillante que la referencia OFF.", nameof(onReferences));

        SuggestedThreshold = (OffMedianBrightness + OnMedianBrightness) / 2.0;
        AcceptedThreshold = SuggestedThreshold;
        UsesSharedFoodReference = usesSharedFoodReference;
    }

    public LightId Light { get; }
    public IReadOnlyList<LightCalibrationReference> OffReferences { get; }
    public IReadOnlyList<LightCalibrationReference> OnReferences { get; }
    public double OffMedianBrightness { get; }
    public double OnMedianBrightness { get; }
    public double SuggestedThreshold { get; }
    public double AcceptedThreshold { get; init; }
    public bool UsesSharedFoodReference { get; }

    private static double Median(IEnumerable<double> values)
    {
        var ordered = values.OrderBy(value => value).ToArray();
        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2.0
            : ordered[middle];
    }
}
