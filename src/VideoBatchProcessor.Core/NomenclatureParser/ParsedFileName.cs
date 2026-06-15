namespace VideoBatchProcessor.Core.Nomenclature;

/// <summary>
/// Esquema de nomenclatura detectado en el nombre del archivo.
/// </summary>
public enum NamingScheme
{
    Unknown,
    LegacySession,    // exp_MMYY_fase_dNrN
    LabStandard,      // ini_YYMM_fN_dNrN_sexo_seg_tipo_trat
    VideoBatchOutput  // ini_YYMM_fN_dNrN_sexo_seg_tipo_res_trat
}

/// <summary>
/// Formato del campo Fecha en el nombre del archivo.
/// Legacy usa MMYY: "0126" = enero 2026 (mes primero).
/// Lab Standard y VBP Output usan YYMM: "2601" = enero 2026 (año primero).
/// </summary>
public enum FechaFormato { MMYY, YYMM }

/// <summary>
/// Tipo de segmento dentro de una sesión CMC.
/// </summary>
public enum TipoSegmento
{
    Evento,             // eN  — ensayo registrado en el .mat
    ITI,                // itiN — intervalo entre ensayos
    Habituacion,        // hab  — habituación completa
    HabituacionInicial, // habini
    HabituacionFinal    // habfin
}

/// <summary>
/// Tipo de ensayo: solo luz de comida (Seguro) o luz + LED de ruido (Peligroso).
/// </summary>
public enum TipoEnsayo { Seguro, Peligroso, NoAplica }

/// <summary>
/// Resultado conductual registrado en el .mat o inferido del video.
/// </summary>
public enum ResultadoConductual { Cruce, NoCruce, Timeout, NoAplica }

/// <summary>
/// Metadata extraída del nombre de un archivo CMC (legacy o lab standard).
/// Los campos no presentes en el esquema quedan en null o en 0.
/// Este record es inmutable; el parser nunca lanza excepciones.
/// </summary>
public sealed record ParsedFileName
{
    public NamingScheme Scheme       { get; init; } = NamingScheme.Unknown;
    public string       OriginalPath { get; init; } = string.Empty;
    public string       Extension    { get; init; } = string.Empty;

    // ── Presentes en los tres esquemas ───────────────────────────────────

    /// <summary>
    /// Fecha de inicio del experimento en el formato propio del esquema:
    /// <list type="bullet">
    ///   <item>Legacy   → MMYY  ("0126" = enero 2026)</item>
    ///   <item>Lab/VBP  → YYMM  ("2601" = enero 2026)</item>
    /// </list>
    /// Usar <see cref="FechaFormato"/> para saber qué formato aplica,
    /// y <see cref="NomenclatureParser.ConvertirFechaAYYMM"/> para normalizar.
    /// </summary>
    public string?      Fecha        { get; init; }

    /// <summary>Indica si Fecha está en MMYY (legacy) o YYMM (lab/VBP).</summary>
    public FechaFormato FechaFormato { get; init; }

    /// <summary>
    /// Fase del protocolo en el código propio del esquema:
    /// <list type="bullet">
    ///   <item>Legacy  → código verbal: cs, cm, cp, dis, pb</item>
    ///   <item>Lab/VBP → código numérico: f2, f3, f4, f5, f6</item>
    /// </list>
    /// Usar <see cref="FaseEstandar"/> para el código normalizado.
    /// </summary>
    public string? Fase { get; init; }

    /// <summary>
    /// Fase del protocolo en el código numérico estándar del lab (f1…f6).
    /// Traduce automáticamente los códigos legacy:
    /// cs→f2, cm→f3, cp→f4, dis→f5, pb→f6.
    /// Para Lab/VBP devuelve el valor de <see cref="Fase"/> directamente.
    /// Null si el esquema es Unknown o el código no se reconoce.
    /// </summary>
    public string? FaseEstandar => Fase switch
    {
        "cs"  => "f2",
        "cm"  => "f3",
        "cp"  => "f4",
        "dis" => "f5",
        "pb"  => "f6",
        _     => Fase?.StartsWith('f') == true ? Fase : null
    };

    public int Dia  { get; init; }
    public int Rata { get; init; }

    // ── LabStandard y VideoBatchOutput ────────────────────────────────────

    /// <summary>Iniciales del experimentador (ej. "abs").</summary>
    public string? Iniciales { get; init; }

    /// <summary>"m" = macho, "h" = hembra.</summary>
    public string? Sexo { get; init; }

    /// <summary>
    /// Segmento en bruto tal como aparece en el nombre:
    /// "e1", "e12", "iti1", "hab", "habini", "habfin".
    /// </summary>
    public string? Segmento { get; init; }

    public TipoSegmento? SegmentoTipo   { get; init; }

    /// <summary>N en "eN" o "itiN". Null para hab / habini / habfin.</summary>
    public int? SegmentoNumero { get; init; }

    public TipoEnsayo? Tipo        { get; init; }
    public string?     Tratamiento { get; init; }

    // ── Solo en VideoBatchOutput ──────────────────────────────────────────

    public ResultadoConductual? Resultado { get; init; }
}