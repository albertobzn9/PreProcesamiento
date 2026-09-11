using System.Globalization;
using System.Text;

namespace VideoBatchProcessor.Core.SessionPairing;

/// <summary>
/// Exporta el inventario previo al recorte para que cada decisión automática
/// pueda auditarse sin abrir los archivos binarios.
/// </summary>
public static class SessionPairingReportCsvWriter
{
    public static void Write(string outputPath, BatchSessionPairingAnalysis analysis)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(analysis);

        var directory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("La ruta del reporte debe incluir una carpeta.", nameof(outputPath));

        Directory.CreateDirectory(directory);
        using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        writer.WriteLine("video_original,fuente_conductual,estado,eventos_mat,eventos_empatados,cobertura,desfase_s,residuo_mediano_s,residuo_maximo_s,alternativas,observaciones");

        foreach (var resolution in analysis.Report.Resolutions)
        {
            var candidate = resolution.SelectedCandidate ?? resolution.Alternatives.FirstOrDefault();
            var duplicateNote = candidate is null
                ? null
                : DuplicateNote(candidate.BehavioralSourcePath, analysis.Report);
            writer.WriteLine(string.Join(',',
                Csv(resolution.VideoPath),
                Csv(resolution.BehavioralSourcePath ?? candidate?.BehavioralSourcePath ?? string.Empty),
                Csv(StatusText(resolution.Status)),
                Number(candidate?.Synchronization.Comparison.Rows.Count(row => row.BehavioralEvent is not null)),
                Number(candidate?.Synchronization.Comparison.MatchedCount),
                Number(candidate?.MatchedFraction),
                Number(candidate?.Synchronization.Comparison.EstimatedStartOffsetSeconds),
                Number(candidate?.MedianAbsoluteResidualSeconds),
                Number(candidate?.MaximumAbsoluteResidualSeconds),
                Csv(string.Join(" | ", resolution.Alternatives.Select(item => Path.GetFileName(item.BehavioralSourcePath)))),
                Csv(JoinNotes(resolution.Message, duplicateNote))));
        }

        foreach (var issue in analysis.InputIssues)
        {
            writer.WriteLine(string.Join(',',
                issue.Kind == SessionPairingInputKind.Video ? Csv(issue.Path) : string.Empty,
                issue.Kind == SessionPairingInputKind.BehavioralSource ? Csv(issue.Path) : string.Empty,
                Csv("Error de lectura"),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                Csv(issue.Message)));
        }

        foreach (var source in analysis.Report.UnassignedBehavioralSources)
        {
            var duplicateNote = DuplicateNote(source, analysis.Report);
            writer.WriteLine(string.Join(',',
                string.Empty,
                Csv(source),
                Csv("Tabla sin video"),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                Csv(JoinNotes("La fuente conductual no quedó asignada automáticamente.", duplicateNote))));
        }
    }

    private static string? DuplicateNote(string sourcePath, SessionPairingReport report)
    {
        var group = report.DuplicateBehavioralEvidence.FirstOrDefault(item =>
            item.SourcePaths.Any(path => string.Equals(
                Path.GetFullPath(path),
                Path.GetFullPath(sourcePath),
                StringComparison.OrdinalIgnoreCase)));
        if (group is null)
            return null;

        return $"Comparte evidencia conductual con: {string.Join(", ", group.SourcePaths.Select(Path.GetFileName))}.";
    }

    private static string JoinNotes(string primary, string? secondary) =>
        string.IsNullOrWhiteSpace(secondary) ? primary : $"{primary} {secondary}";

    private static string StatusText(SessionPairingStatus status) => status switch
    {
        SessionPairingStatus.Confirmed => "Confirmado",
        SessionPairingStatus.Ambiguous => "Ambiguo",
        SessionPairingStatus.NoMatch => "Sin coincidencia",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    private static string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Number(double? value) => value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value)
        ? string.Empty
        : value.Value.ToString("0.######", CultureInfo.InvariantCulture);

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
