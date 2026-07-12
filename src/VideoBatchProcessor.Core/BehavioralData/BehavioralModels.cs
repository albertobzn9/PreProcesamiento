namespace VideoBatchProcessor.Core.BehavioralData;

/// <summary>
/// Formato de la fuente principal de datos conductuales de una sesión.
/// </summary>
public enum BehavioralSourceKind
{
    None,
    LegacyMat,
    CsvV1,
}

public enum BehavioralSourceOrigin
{
    None,
    ExplicitOverride,
    CsvSibling,
    MatSibling,
}

public enum BehavioralEventType
{
    SafeFood = 0,
    ConflictWithFood = 1,
    SoundOnly = 2,
}

/// <summary>
/// Resultado de decidir qué archivo conductual acompaña a un video.
/// Los avisos se conservan para que la UI pueda enseñarlos al investigador.
/// </summary>
public sealed record BehavioralSourceResolution
{
    public string? SourcePath { get; init; }
    public BehavioralSourceKind SourceKind { get; init; }
    public BehavioralSourceOrigin Origin { get; init; }
    public string? PressesPath { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public bool IsResolved => SourcePath is not null && Error is null;
}

/// <summary>
/// Fila normalizada de la tabla conductual principal. RawValues conserva la
/// representación original del CSV para auditoría y reportes.
/// </summary>
public sealed record BehavioralEvent(
    int EventNumber,
    int Side,
    int Stimulus,
    double LeverLatencySeconds,
    double AbsoluteTimeSeconds,
    int LeftLeverPresses,
    int RightLeverPresses,
    double CrossingLatencySeconds,
    BehavioralEventType EventType,
    IReadOnlyList<string> RawValues);

/// <summary>
/// Evidencia opcional de una presión individual. TrialNumber es nulo cuando
/// CajaValentia escribe NA, por ejemplo durante habituación o ITI.
/// </summary>
public sealed record BehavioralPressEvent(
    int SessionEventNumber,
    double TimeSeconds,
    string Phase,
    int? TrialNumber,
    string EventTypeRaw,
    int Side,
    int SideSessionCounter,
    int HardwareCounter,
    IReadOnlyList<string> RawValues);

public sealed record BehavioralSessionData(
    BehavioralSourceResolution Source,
    IReadOnlyList<BehavioralEvent> Events,
    IReadOnlyList<BehavioralPressEvent> Presses,
    IReadOnlyList<string> Warnings);

public sealed class BehavioralDataFormatException : FormatException
{
    public BehavioralDataFormatException(string message)
        : base(message)
    {
    }
}

public interface IBehavioralSessionReader
{
    BehavioralSourceKind SourceKind { get; }

    BehavioralSessionData Read(BehavioralSourceResolution source);
}
