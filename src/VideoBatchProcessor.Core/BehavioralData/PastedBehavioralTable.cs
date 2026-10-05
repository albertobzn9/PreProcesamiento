using System.Globalization;
using Microsoft.VisualBasic.FileIO;

namespace VideoBatchProcessor.Core.BehavioralData;

/// <summary>Tab-separated Excel clipboard data, using the historical MAT column order.</summary>
public static class PastedBehavioralTable
{
    public static IReadOnlyList<string> Columns { get; } =
        ["Ensayo", "Lado", "Estim", "Latencia", "TiempoAbs", "PalancasIzq", "PalancasDer", "Desplaz"];

    public static IReadOnlyList<BehavioralEvent> Parse(string text, bool decimalComma = false)
    {
        if (string.IsNullOrWhiteSpace(text)) throw Invalid(1, null, "paste at least one event row");
        if (text.Length > 2_000_000) throw Invalid(1, null, "the pasted table is too large (maximum 2 MB)");
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n').Split('\n');
        var rows = new List<IReadOnlyList<double>>();
        var raw = new List<IReadOnlyList<string>>();
        var culture = decimalComma ? CultureInfo.GetCultureInfo("fr-FR") : CultureInfo.InvariantCulture;
        for (var index = 0; index < lines.Length; index++)
        {
            using var parser = new TextFieldParser(new StringReader(lines[index])) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
            parser.SetDelimiters("\t");
            string[] fields;
            try { fields = parser.ReadFields() ?? []; }
            catch (MalformedLineException) { throw Invalid(index + 1, null, "invalid quoted cell"); }
            if (fields.Length != 8) throw Invalid(index + 1, null, $"expected 8 columns, received {fields.Length}");
            if (index == 0 && fields.Select(NormalizeHeader).SequenceEqual(Columns.Select(NormalizeHeader))) continue;
            var numbers = new double[8];
            for (var column = 0; column < 8; column++)
            {
                if (!double.TryParse(fields[column], NumberStyles.Float, culture, out var value) || !double.IsFinite(value))
                    throw Invalid(index + 1, column, $"expected a number with {(decimalComma ? "comma" : "point")} decimal separator; empty cells and thousands separators are not accepted");
                if ((column is 0 or 1 or 2 or 5 or 6) && (value != Math.Truncate(value) || value < int.MinValue || value > int.MaxValue))
                    throw Invalid(index + 1, column, "expected an integer");
                if (column == 1 && value is not (-2 or 0 or 1)) throw Invalid(index + 1, column, "expected 1, 0 or -2");
                if (column == 2 && value is not (0 or 1)) throw Invalid(index + 1, column, "expected 0 or 1");
                if (column != 1 && value < 0 || column == 0 && value == 0) throw Invalid(index + 1, column, "value is outside the allowed range");
                numbers[column] = value;
            }
            if (rows.Count > 0 && numbers[0] <= rows[^1][0]) throw Invalid(index + 1, 0, "event numbers must increase without duplicates; rows will not be reordered");
            if (rows.Count > 0 && numbers[4] < rows[^1][4]) throw Invalid(index + 1, 4, "absolute time must not go backwards");
            rows.Add(numbers);
            raw.Add(fields);
        }
        if (rows.Count == 0) throw Invalid(lines.Length, null, "headers alone are not a table");
        return LegacyMatEventMapper.MapRows(rows).Select((item, index) => item with { RawValues = raw[index] }).ToArray();
    }

    // Each confirmation gets its own directory, preserving all previous copies.
    public static string Save(string root, string videoPath, string text, bool decimalComma)
    {
        var events = Parse(text, decimalComma);
        var folder = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "clipboard.txt"), text);
        var path = Path.Combine(folder, Path.GetFileNameWithoutExtension(videoPath) + ".pasted.tsv");
        var rows = events.Select(item => string.Join('\t', new double[]
        {
            item.EventNumber, item.Side, item.Stimulus, item.LeverLatencySeconds, item.AbsoluteTimeSeconds,
            item.LeftLeverPresses, item.RightLeverPresses, item.CrossingLatencySeconds,
        }.Select(value => value.ToString("R", CultureInfo.InvariantCulture))));
        File.WriteAllLines(path, new[] { string.Join('\t', Columns) }.Concat(rows));
        return path;
    }

    private static string NormalizeHeader(string value) => value.Trim().Replace(" ", "").Replace("_", "").ToLowerInvariant();
    private static BehavioralDataFormatException Invalid(int row, int? column, string message) =>
        new($"Row {row}{(column is { } c ? $", column {c + 1} ({Columns[c]})" : "")}: {message}.");
}

public sealed class PastedTableSessionReader : IBehavioralSessionReader
{
    public BehavioralSourceKind SourceKind => BehavioralSourceKind.PastedTable;
    public BehavioralSessionData Read(BehavioralSourceResolution source)
    {
        if (!source.IsResolved || source.SourceKind != SourceKind) throw new ArgumentException("Expected a pasted table source.", nameof(source));
        return new(source, PastedBehavioralTable.Parse(File.ReadAllText(source.SourcePath!)), [], source.Warnings);
    }
}
