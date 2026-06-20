using VideoBatchProcessor.Core.Nomenclature;

namespace VideoBatchProcessor.Core.SessionResolver;

/// <summary>
/// Combina el resultado del NomenclatureParser con el BatchManifest y la ruta
/// real del video para producir una SessionMetadata con todos los campos normalizados.
///
/// Flujo para la UI:
/// <code>
/// var meta = resolver.Resolve(parsed, manifest, videoPath);
///
/// if (meta.FormatoNoReconocido)
///     // Mostrar SessionMetadataResolver.MensajeFormatoNoReconocido
///     // Pedir al usuario TODOS los campos en meta.MissingFields
/// else if (!meta.IsComplete)
///     // Pedir al usuario solo los campos en meta.MissingFields
///     // (Iniciales, Sexo, Tratamiento en archivos legacy)
///
/// // El usuario llena el formulario → llamar Complete()
/// var completa = resolver.Complete(meta, userValues);
/// </code>
/// </summary>
public sealed class SessionMetadataResolver
{
    /// <summary>
    /// Mensaje que la UI debe mostrar cuando el nombre del archivo no coincide
    /// con ningún esquema conocido (Legacy ni Lab Standard).
    /// Incluye ejemplos de ambos formatos aceptados.
    /// </summary>
    public static readonly string MensajeFormatoNoReconocido =
        "Formato de Entrada No Reconocido.\n" +
        "El nombre del archivo no corresponde a ningún esquema conocido.\n\n" +
        "Formatos aceptados:\n" +
        "  Legacy        →  exp_0126_dis_d1r3.mp4\n" +
        "  Lab standard  →  abs_2601_f5_d1r3_m_e1_p_stx.mp4\n\n" +
        "Por favor ingresa los datos faltantes manualmente.";

    // ── Resolve ───────────────────────────────────────────────────────────

    /// <summary>
    /// Primera resolución automática a partir del nombre del archivo y el manifest.
    /// Si el resultado tiene IsComplete = false, llamar a Complete() con los
    /// valores que el usuario ingresó en la UI.
    /// </summary>
    public SessionMetadata Resolve(
        ParsedFileName parsed,
        BatchManifest? manifest,
        string         videoPath)
    {
        manifest ??= BatchManifest.Empty;

        var stem = Path.GetFileNameWithoutExtension(videoPath ?? string.Empty);
        manifest.Overrides.TryGetValue(stem, out var over);

        var iniciales   = over?.Iniciales   ?? parsed.Iniciales   ?? manifest.Iniciales;
        var sexo        = over?.Sexo        ?? parsed.Sexo        ?? manifest.Sexo;
        var tratamiento = over?.Tratamiento ?? parsed.Tratamiento ?? manifest.Tratamiento;

        var fecha = parsed.Fecha is null ? null
            : parsed.FechaFormato == FechaFormato.MMYY
                ? NomenclatureParser.ConvertirFechaAYYMM(parsed.Fecha)
                : parsed.Fecha;

        var fase    = parsed.FaseEstandar;
        var matPath = over?.MatPath ?? ResolveMatPath(videoPath, stem);

        var missing = ValidarCampos(iniciales, fecha, fase, parsed.Dia, parsed.Rata, sexo, tratamiento);

        return new SessionMetadata
        {
            Scheme          = parsed.Scheme,
            Iniciales       = iniciales   ?? string.Empty,
            Fecha           = fecha       ?? string.Empty,
            Fase            = fase        ?? string.Empty,
            Dia             = parsed.Dia,
            Rata            = parsed.Rata,
            Sexo            = sexo        ?? string.Empty,
            Tratamiento     = tratamiento ?? string.Empty,
            SourceVideoPath = videoPath   ?? string.Empty,
            SourceMatPath   = matPath,
            MissingFields   = missing,
        };
    }

    // ── Complete ──────────────────────────────────────────────────────────

    /// <summary>
    /// Completa una SessionMetadata parcial con los valores que el usuario
    /// ingresó en la UI. Solo aplica los valores del usuario en campos que
    /// aún están vacíos; nunca sobreescribe lo que ya se resolvió.
    /// <para>
    /// Para archivos legacy: el usuario normalmente provee Iniciales, Sexo
    /// y Tratamiento.
    /// </para>
    /// <para>
    /// Para formato no reconocido (FormatoNoReconocido = true): el usuario
    /// debe proveer también Fecha, Fase, Dia y Rata.
    /// </para>
    /// </summary>
    public SessionMetadata Complete(SessionMetadata partial, UserFieldValues userValues)
    {
        var iniciales   = FirstNonEmpty(partial.Iniciales,   userValues.Iniciales);
        var sexo        = FirstNonEmpty(partial.Sexo,        userValues.Sexo);
        var tratamiento = FirstNonEmpty(partial.Tratamiento, userValues.Tratamiento);
        var fecha       = FirstNonEmpty(partial.Fecha,       userValues.Fecha);
        var fase        = FirstNonEmpty(partial.Fase,        userValues.Fase);
        var dia         = partial.Dia  != 0 ? partial.Dia  : (userValues.Dia  ?? 0);
        var rata        = partial.Rata != 0 ? partial.Rata : (userValues.Rata ?? 0);
        var matPath     = userValues.MatPath ?? partial.SourceMatPath;

        var missing = ValidarCampos(iniciales, fecha, fase, dia, rata, sexo, tratamiento);

        return partial with
        {
            Iniciales     = iniciales   ?? string.Empty,
            Fecha         = fecha       ?? string.Empty,
            Fase          = fase        ?? string.Empty,
            Dia           = dia,
            Rata          = rata,
            Sexo          = sexo        ?? string.Empty,
            Tratamiento   = tratamiento ?? string.Empty,
            SourceMatPath = matPath,
            MissingFields = missing,
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static List<string> ValidarCampos(
        string? iniciales, string? fecha, string? fase,
        int dia, int rata, string? sexo, string? tratamiento)
    {
        var missing = new List<string>();
        if (string.IsNullOrEmpty(iniciales))   missing.Add(nameof(SessionMetadata.Iniciales));
        if (string.IsNullOrEmpty(fecha))        missing.Add(nameof(SessionMetadata.Fecha));
        if (string.IsNullOrEmpty(fase))         missing.Add(nameof(SessionMetadata.Fase));
        if (dia  == 0)                          missing.Add(nameof(SessionMetadata.Dia));
        if (rata == 0)                          missing.Add(nameof(SessionMetadata.Rata));
        if (string.IsNullOrEmpty(sexo))         missing.Add(nameof(SessionMetadata.Sexo));
        if (string.IsNullOrEmpty(tratamiento))  missing.Add(nameof(SessionMetadata.Tratamiento));
        return missing;
    }

    private static string? FirstNonEmpty(string existing, string? userValue)
        => !string.IsNullOrEmpty(existing) ? existing : userValue;

    private static string? ResolveMatPath(string? videoPath, string stem)
    {
        if (string.IsNullOrEmpty(videoPath)) return null;
        var dir = Path.GetDirectoryName(videoPath);
        return string.IsNullOrEmpty(dir)
            ? stem + ".mat"
            : Path.Combine(dir, stem + ".mat");
    }
}