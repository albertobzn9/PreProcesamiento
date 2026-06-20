using VideoBatchProcessor.Core.Nomenclature;

namespace VideoBatchProcessor.Core.SessionResolver;

public sealed record BatchManifest
{
    public string? Iniciales   { get; init; }
    public string? Sexo        { get; init; }
    public string? Tratamiento { get; init; }
    public IReadOnlyDictionary<string, FileOverride> Overrides { get; init; }
        = new Dictionary<string, FileOverride>(StringComparer.OrdinalIgnoreCase);
    public static readonly BatchManifest Empty = new();
}

public sealed record FileOverride
{
    public string? Iniciales   { get; init; }
    public string? Sexo        { get; init; }
    public string? Tratamiento { get; init; }
    public string? MatPath     { get; init; }
}

public sealed record UserFieldValues
{
    public string? Iniciales   { get; init; }
    public string? Sexo        { get; init; }
    public string? Tratamiento { get; init; }
    public string? MatPath     { get; init; }
    public string? Fecha       { get; init; }
    public string? Fase        { get; init; }
    public int?    Dia         { get; init; }
    public int?    Rata        { get; init; }
}

public sealed record SessionMetadata
{
    public NamingScheme Scheme          { get; init; }
    public string       Iniciales       { get; init; } = string.Empty;
    public string       Fecha           { get; init; } = string.Empty;
    public string       Fase            { get; init; } = string.Empty;
    public int          Dia             { get; init; }
    public int          Rata            { get; init; }
    public string       Sexo            { get; init; } = string.Empty;
    public string       Tratamiento     { get; init; } = string.Empty;
    public string       SourceVideoPath { get; init; } = string.Empty;
    public string?      SourceMatPath   { get; init; }
    public bool         IsComplete      => MissingFields.Count == 0;
    public bool         FormatoNoReconocido => Scheme == NamingScheme.Unknown;
    public IReadOnlyList<string> MissingFields { get; init; } = [];
}
