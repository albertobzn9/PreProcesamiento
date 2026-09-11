namespace VideoBatchProcessor.Core.SessionFiles;

/// <summary>
/// Renombra el video principal de una sesión y sus contenedores alternativos
/// sin sobrescribir archivos existentes. Si un movimiento falla, intenta
/// regresar los archivos ya movidos a sus nombres originales.
/// </summary>
public sealed class SessionSourceRenamer
{
    public bool TryRename(
        string primaryVideoPath,
        IReadOnlyList<string>? alternateVideoPaths,
        string correctedStem,
        out SessionSourceRenameResult? result,
        out string? error)
    {
        result = null;
        error = null;

        if (string.IsNullOrWhiteSpace(primaryVideoPath) || !File.Exists(primaryVideoPath))
        {
            error = "El video que quieres renombrar ya no está disponible.";
            return false;
        }

        correctedStem = correctedStem.Trim();
        if (string.IsNullOrWhiteSpace(correctedStem) ||
            !string.Equals(Path.GetFileName(correctedStem), correctedStem, StringComparison.Ordinal) ||
            !string.IsNullOrEmpty(Path.GetExtension(correctedStem)) ||
            correctedStem.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            error = "Escribe solo el nombre base, sin carpeta ni extensión.";
            return false;
        }

        string[] sourcePaths;
        try
        {
            var primaryFullPath = Path.GetFullPath(primaryVideoPath);
            var primaryDirectory = Path.GetDirectoryName(primaryFullPath)!;
            var primaryStem = Path.GetFileNameWithoutExtension(primaryFullPath);
            var siblingContainers = Directory.EnumerateFiles(primaryDirectory)
                .Where(path => SupportedVideoExtensions.Contains(Path.GetExtension(path)) &&
                               string.Equals(Path.GetFileNameWithoutExtension(path), primaryStem, StringComparison.OrdinalIgnoreCase));
            sourcePaths = new[] { primaryVideoPath }
                .Concat(alternateVideoPaths ?? [])
                .Concat(siblingContainers)
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = $"No se pudo revisar la carpeta del video: {exception.Message}";
            return false;
        }
        var moves = sourcePaths
            .Select(source => new FileMove(
                Path.GetFullPath(source),
                Path.Combine(Path.GetDirectoryName(Path.GetFullPath(source))!, correctedStem + Path.GetExtension(source))))
            .ToArray();

        foreach (var move in moves)
        {
            if (!string.Equals(move.Source, move.Target, StringComparison.OrdinalIgnoreCase) && File.Exists(move.Target))
            {
                error = $"Ya existe un archivo llamado {Path.GetFileName(move.Target)}. No se cambió nada.";
                return false;
            }
        }

        var completed = new List<FileMove>();
        try
        {
            foreach (var move in moves.Where(move =>
                         !string.Equals(move.Source, move.Target, StringComparison.Ordinal)))
            {
                File.Move(move.Source, move.Target);
                completed.Add(move);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            foreach (var move in completed.AsEnumerable().Reverse())
            {
                try
                {
                    if (File.Exists(move.Target) && !File.Exists(move.Source))
                        File.Move(move.Target, move.Source);
                }
                catch (Exception rollbackException) when (rollbackException is IOException or UnauthorizedAccessException)
                {
                    // El mensaje final indica que se debe revisar la carpeta manualmente.
                }
            }

            error = completed.Count == 0
                ? $"No se pudo cambiar el nombre: {exception.Message}"
                : $"No se pudo completar el cambio de nombre. Revisa la carpeta antes de continuar: {exception.Message}";
            return false;
        }

        var primaryTarget = moves[0].Target;
        result = new SessionSourceRenameResult(primaryTarget, moves.Skip(1).Select(move => move.Target).ToArray());
        return true;
    }

    private sealed record FileMove(string Source, string Target);

    private static readonly HashSet<string> SupportedVideoExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mkv", ".mov", ".avi", ".m4v" };
}

public sealed record SessionSourceRenameResult(
    string PrimaryVideoPath,
    IReadOnlyList<string> AlternateVideoPaths)
{
    public IReadOnlyList<string> AllVideoPaths => [PrimaryVideoPath, .. AlternateVideoPaths];
}
