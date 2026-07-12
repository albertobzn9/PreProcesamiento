namespace VideoBatchProcessor.Core.BehavioralData;

/// <summary>
/// Encuentra la fuente conductual de un video sin asumir que siempre será MAT.
/// No lee filas: solo aplica la prioridad de rutas y valida la identidad del
/// CSV V1 mediante su encabezado.
/// </summary>
public sealed class BehavioralSourceResolver
{
    public BehavioralSourceResolution Resolve(string videoPath, string? explicitSourcePath = null)
    {
        if (string.IsNullOrWhiteSpace(videoPath))
        {
            return new BehavioralSourceResolution
            {
                Error = "No se puede resolver una fuente conductual sin una ruta de video.",
            };
        }

        if (!string.IsNullOrWhiteSpace(explicitSourcePath))
            return ResolveExplicit(explicitSourcePath);

        var directory = Path.GetDirectoryName(videoPath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(videoPath);
        var csvPath = Path.Combine(directory, stem + ".csv");
        var matPath = Path.Combine(directory, stem + ".mat");
        var warnings = new List<string>();

        if (File.Exists(csvPath))
        {
            if (HasCsvV1Header(csvPath, out var csvError))
                return Resolved(csvPath, BehavioralSourceKind.CsvV1, BehavioralSourceOrigin.CsvSibling, warnings);

            warnings.Add($"Se ignoró '{Path.GetFileName(csvPath)}': {csvError}");
        }

        if (File.Exists(matPath))
            return Resolved(matPath, BehavioralSourceKind.LegacyMat, BehavioralSourceOrigin.MatSibling, warnings);

        return new BehavioralSourceResolution
        {
            Warnings = warnings,
            Error = $"No se encontró una fuente conductual para '{stem}'.",
        };
    }

    public static bool HasCsvV1Header(string csvPath, out string? error)
    {
        error = null;

        try
        {
            using var reader = new StreamReader(csvPath);
            var header = reader.ReadLine();
            if (header == CsvV1BehavioralSessionReader.MainHeader)
                return true;

            error = "el encabezado no coincide exactamente con CSV V1";
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = $"no se pudo leer ({exception.Message})";
            return false;
        }
    }

    private static BehavioralSourceResolution ResolveExplicit(string explicitSourcePath)
    {
        if (!File.Exists(explicitSourcePath))
        {
            return new BehavioralSourceResolution
            {
                Error = $"La fuente conductual indicada no existe: '{explicitSourcePath}'.",
            };
        }

        var extension = Path.GetExtension(explicitSourcePath);
        if (extension.Equals(".mat", StringComparison.OrdinalIgnoreCase))
            return Resolved(explicitSourcePath, BehavioralSourceKind.LegacyMat, BehavioralSourceOrigin.ExplicitOverride, []);

        if (!extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return new BehavioralSourceResolution
            {
                Error = "La fuente conductual indicada debe ser un archivo .mat o un CSV V1 principal.",
            };
        }

        if (IsPressesFile(explicitSourcePath))
        {
            return new BehavioralSourceResolution
            {
                Error = "Un archivo _palanqueos.csv es evidencia complementaria, no una fuente principal de sesión.",
            };
        }

        if (!HasCsvV1Header(explicitSourcePath, out var csvError))
        {
            return new BehavioralSourceResolution
            {
                Error = $"El CSV indicado no cumple CSV V1: {csvError}.",
            };
        }

        return Resolved(explicitSourcePath, BehavioralSourceKind.CsvV1, BehavioralSourceOrigin.ExplicitOverride, []);
    }

    private static BehavioralSourceResolution Resolved(
        string sourcePath,
        BehavioralSourceKind sourceKind,
        BehavioralSourceOrigin origin,
        IReadOnlyList<string> warnings)
    {
        var pressesPath = FindPressesSibling(sourcePath);
        var allWarnings = warnings.ToList();

        if (sourceKind == BehavioralSourceKind.CsvV1 && pressesPath is null)
            allWarnings.Add("No se encontró el CSV opcional de palanqueos para esta sesión.");

        return new BehavioralSourceResolution
        {
            SourcePath = sourcePath,
            SourceKind = sourceKind,
            Origin = origin,
            PressesPath = pressesPath,
            Warnings = allWarnings,
        };
    }

    private static string? FindPressesSibling(string sourcePath)
    {
        var directory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(sourcePath);
        var pressesPath = Path.Combine(directory, stem + "_palanqueos.csv");
        return File.Exists(pressesPath) ? pressesPath : null;
    }

    private static bool IsPressesFile(string path) =>
        Path.GetFileNameWithoutExtension(path).EndsWith("_palanqueos", StringComparison.OrdinalIgnoreCase);
}
