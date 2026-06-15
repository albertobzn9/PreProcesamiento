using System.Text.RegularExpressions;

namespace VideoBatchProcessor.Core.Nomenclature;

/// <summary>
/// Parsea nombres de archivos CMC reconociendo dos formatos de entrada:
/// <list type="bullet">
///   <item><b>LegacySession</b>  — exp_MMYY_fase_dNrN  (datos históricos del lab)</item>
///   <item><b>LabStandard</b>    — ini_YYMM_fN_dNrN_sexo_seg_tipo_trat</item>
/// </list>
/// El formato de salida (<b>VideoBatchOutput</b>) lo genera <see cref="BuildVbpOutputName"/>.
///
/// Solo opera sobre strings. Thread-safe: los Regex son campos estáticos compilados.
/// </summary>
public sealed class NomenclatureParser
{
    // ── Patrones compilados ───────────────────────────────────────────────
    // El orden de prueba importa: VBP Output (9 tokens) ANTES que LabStandard (8),
    // para evitar que LabStandard absorba un VBP Output válido.

    /// <summary>
    /// VBP Output — 9 tokens: ini_YYMM_fN_dNrN_sexo_seg_tipo_res_trat
    /// Ejemplo: abs_2601_f5_d1r3_m_e1_p_cr_stx
    /// </summary>
    private static readonly Regex s_vbpOutput = new(
        @"^(?<ini>[a-z]{2,5})_(?<fecha>\d{4})_(?<fase>f\d+)_d(?<dia>\d+)r(?<rata>\d+)" +
        @"_(?<sexo>m|h)" +
        @"_(?<seg>habini|habfin|hab|iti\d+|e\d+)" +
        @"_(?<tipo>s|p|na)" +
        @"_(?<res>cr|nc|to|na)" +
        @"_(?<trat>[a-z0-9]+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Lab Standard — 8 tokens: ini_YYMM_fN_dNrN_sexo_seg_tipo_trat
    /// Ejemplo: abs_2601_f5_d1r3_m_e1_p_stx
    /// </summary>
    private static readonly Regex s_labStandard = new(
        @"^(?<ini>[a-z]{2,5})_(?<fecha>\d{4})_(?<fase>f\d+)_d(?<dia>\d+)r(?<rata>\d+)" +
        @"_(?<sexo>m|h)" +
        @"_(?<seg>habini|habfin|hab|iti\d+|e\d+)" +
        @"_(?<tipo>s|p|na)" +
        @"_(?<trat>[a-z0-9]+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Legacy — exp_MMYY_fase_dNrN
    /// Ejemplo: exp_0126_dis_d1r3
    /// Fases: cs, cm, cp, dis, pb  (ver <see cref="ParsedFileName.FaseEstandar"/>)
    /// </summary>
    private static readonly Regex s_legacy = new(
        @"^exp_(?<fecha>\d{4})_(?<fase>[a-z]+)_d(?<dia>\d+)r(?<rata>\d+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ── API pública ───────────────────────────────────────────────────────

    /// <summary>
    /// Intenta parsear el nombre de un archivo (o ruta completa).
    /// Devuelve <c>true</c> si reconoció el esquema.
    /// Devuelve <c>false</c> si no encaja; <paramref name="result"/>.Scheme queda Unknown.
    /// Nunca lanza excepciones.
    /// </summary>
    public bool TryParse(string filePath, out ParsedFileName result)
    {
        var name = Path.GetFileNameWithoutExtension(filePath ?? string.Empty)
                       .ToLowerInvariant();
        var ext  = Path.GetExtension(filePath ?? string.Empty);

        var seed = new ParsedFileName
        {
            OriginalPath = filePath ?? string.Empty,
            Extension    = ext
        };

        Match m;

        // 1. VBP Output — más específico, siempre primero
        m = s_vbpOutput.Match(name);
        if (m.Success) { result = ApplyVbpOutput(seed, m);   return true; }

        // 2. Lab Standard
        m = s_labStandard.Match(name);
        if (m.Success) { result = ApplyLabStandard(seed, m); return true; }

        // 3. Legacy
        m = s_legacy.Match(name);
        if (m.Success) { result = ApplyLegacy(seed, m);      return true; }

        result = seed;
        return false;
    }

    /// <summary>
    /// Construye el nombre de archivo de salida en nomenclatura VBP Output.
    /// <para>
    /// <b>Nota sobre fecha:</b> este método espera <paramref name="fecha"/> en formato
    /// YYMM (ej. "2601" = enero 2026). Si la fuente es un archivo legacy (MMYY),
    /// convertir primero con <see cref="ConvertirFechaAYYMM"/>.
    /// </para>
    /// Ejemplo: BuildVbpOutputName("abs","2601","f5",1,3,"m","e1","p","cr","stx")
    ///          → "abs_2601_f5_d1r3_m_e1_p_cr_stx.mp4"
    /// </summary>
    public static string BuildVbpOutputName(
        string iniciales,
        string fecha,
        string fase,
        int    dia,
        int    rata,
        string sexo,
        string segmento,
        string tipo,
        string resultado,
        string tratamiento,
        string extension = ".mp4")
    {
        return string
            .Join("_", iniciales, fecha, fase, $"d{dia}r{rata}",
                  sexo, segmento, tipo, resultado, tratamiento)
            .ToLowerInvariant()
            + extension.ToLowerInvariant();
    }

    /// <summary>
    /// Convierte una fecha de formato MMYY (legacy) a YYMM (lab/VBP).
    /// Ejemplo: "0126" → "2601"  (enero 2026).
    /// </summary>
    /// <exception cref="ArgumentException">Si la fecha no tiene exactamente 4 caracteres.</exception>
    public static string ConvertirFechaAYYMM(string fechaMmYy)
    {
        if (string.IsNullOrEmpty(fechaMmYy) || fechaMmYy.Length != 4)
            throw new ArgumentException(
                "La fecha debe tener exactamente 4 caracteres en formato MMYY.",
                nameof(fechaMmYy));

        return fechaMmYy[2..] + fechaMmYy[..2];   // "0126" → "26" + "01" = "2601"
    }

    // ── Builders privados ─────────────────────────────────────────────────

    private static ParsedFileName ApplyVbpOutput(ParsedFileName seed, Match m) =>
        seed with
        {
            Scheme         = NamingScheme.VideoBatchOutput,
            FechaFormato   = FechaFormato.YYMM,
            Iniciales      = m.Groups["ini"].Value,
            Fecha          = m.Groups["fecha"].Value,
            Fase           = m.Groups["fase"].Value,
            Dia            = int.Parse(m.Groups["dia"].Value),
            Rata           = int.Parse(m.Groups["rata"].Value),
            Sexo           = m.Groups["sexo"].Value,
            Segmento       = m.Groups["seg"].Value,
            SegmentoTipo   = ResolveSegmentoTipo(m.Groups["seg"].Value),
            SegmentoNumero = ResolveSegmentoNumero(m.Groups["seg"].Value),
            Tipo           = ResolveTipoEnsayo(m.Groups["tipo"].Value),
            Resultado      = ResolveResultado(m.Groups["res"].Value),
            Tratamiento    = m.Groups["trat"].Value,
        };

    private static ParsedFileName ApplyLabStandard(ParsedFileName seed, Match m) =>
        seed with
        {
            Scheme         = NamingScheme.LabStandard,
            FechaFormato   = FechaFormato.YYMM,
            Iniciales      = m.Groups["ini"].Value,
            Fecha          = m.Groups["fecha"].Value,
            Fase           = m.Groups["fase"].Value,
            Dia            = int.Parse(m.Groups["dia"].Value),
            Rata           = int.Parse(m.Groups["rata"].Value),
            Sexo           = m.Groups["sexo"].Value,
            Segmento       = m.Groups["seg"].Value,
            SegmentoTipo   = ResolveSegmentoTipo(m.Groups["seg"].Value),
            SegmentoNumero = ResolveSegmentoNumero(m.Groups["seg"].Value),
            Tipo           = ResolveTipoEnsayo(m.Groups["tipo"].Value),
            Tratamiento    = m.Groups["trat"].Value,
        };

    private static ParsedFileName ApplyLegacy(ParsedFileName seed, Match m) =>
        seed with
        {
            Scheme       = NamingScheme.LegacySession,
            FechaFormato = FechaFormato.MMYY,
            Fecha        = m.Groups["fecha"].Value,
            Fase         = m.Groups["fase"].Value,   // código verbal: cs, cm, cp, dis, pb
            Dia          = int.Parse(m.Groups["dia"].Value),
            Rata         = int.Parse(m.Groups["rata"].Value),
        };

    // ── Helpers de mapeo ──────────────────────────────────────────────────

    private static TipoSegmento? ResolveSegmentoTipo(string seg) => seg switch
    {
        "habini" => TipoSegmento.HabituacionInicial,
        "habfin" => TipoSegmento.HabituacionFinal,
        "hab"    => TipoSegmento.Habituacion,
        _ when seg.StartsWith("iti", StringComparison.Ordinal) => TipoSegmento.ITI,
        _ when seg.StartsWith("e",   StringComparison.Ordinal) => TipoSegmento.Evento,
        _ => null
    };

    private static int? ResolveSegmentoNumero(string seg)
    {
        if (seg.StartsWith("e",   StringComparison.Ordinal) &&
            int.TryParse(seg.AsSpan(1), out var ne)) return ne;
        if (seg.StartsWith("iti", StringComparison.Ordinal) &&
            int.TryParse(seg.AsSpan(3), out var ni)) return ni;
        return null;
    }

    private static TipoEnsayo? ResolveTipoEnsayo(string tipo) => tipo switch
    {
        "s"  => TipoEnsayo.Seguro,
        "p"  => TipoEnsayo.Peligroso,
        "na" => TipoEnsayo.NoAplica,
        _    => null
    };

    private static ResultadoConductual? ResolveResultado(string res) => res switch
    {
        "cr" => ResultadoConductual.Cruce,
        "nc" => ResultadoConductual.NoCruce,
        "to" => ResultadoConductual.Timeout,
        "na" => ResultadoConductual.NoAplica,
        _    => null
    };
}