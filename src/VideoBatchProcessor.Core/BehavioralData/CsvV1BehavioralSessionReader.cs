using System.Globalization;
using System.Text;

namespace VideoBatchProcessor.Core.BehavioralData;

/// <summary>
/// Lector del contrato CSV V1 producido por CajaValentia. El CSV principal y
/// el de palanqueos se validan por separado porque cumplen funciones distintas.
/// </summary>
public sealed class CsvV1BehavioralSessionReader : IBehavioralSessionReader
{
    public const string MainHeader =
        "ensayo,lado,estimulo,latencia_s,tiempo_absoluto_s,palancas_izq,palancas_der,desplazamiento_s,tipo_evento";

    public const string PressesHeader =
        "evento_sesion,tiempo_s,fase,ensayo,tipo_evento,lado,contador_lado_sesion,contador_hardware";

    public BehavioralSourceKind SourceKind => BehavioralSourceKind.CsvV1;

    public BehavioralSessionData Read(BehavioralSourceResolution source)
    {
        if (!source.IsResolved || source.SourceKind != SourceKind || source.SourcePath is null)
            throw new ArgumentException("La resolución debe apuntar a un CSV V1 principal válido.", nameof(source));

        var events = ReadMain(source.SourcePath);
        var presses = source.PressesPath is null ? [] : ReadPresses(source.PressesPath);

        return new BehavioralSessionData(source, events, presses, source.Warnings);
    }

    private static IReadOnlyList<BehavioralEvent> ReadMain(string path)
    {
        using var reader = new StreamReader(path);
        RequireHeader(reader.ReadLine(), MainHeader, path);

        var events = new List<BehavioralEvent>();
        var lineNumber = 1;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (line.Length == 0)
                throw Invalid(path, lineNumber, "la fila está vacía");

            var values = CsvLine.Parse(line, path, lineNumber);
            RequireColumnCount(values, 9, path, lineNumber);

            var side = ReadSide(values[1], path, lineNumber);
            var stimulus = ReadBinary(values[2], "estimulo", path, lineNumber);
            var type = ReadEventType(values[8], path, lineNumber);

            events.Add(new BehavioralEvent(
                ReadWhole(values[0], "ensayo", path, lineNumber),
                side,
                stimulus,
                ReadNumber(values[3], "latencia_s", path, lineNumber),
                ReadNumber(values[4], "tiempo_absoluto_s", path, lineNumber),
                ReadWhole(values[5], "palancas_izq", path, lineNumber),
                ReadWhole(values[6], "palancas_der", path, lineNumber),
                ReadNumber(values[7], "desplazamiento_s", path, lineNumber),
                type,
                values));
        }

        return events;
    }

    private static IReadOnlyList<BehavioralPressEvent> ReadPresses(string path)
    {
        using var reader = new StreamReader(path);
        RequireHeader(reader.ReadLine(), PressesHeader, path);

        var presses = new List<BehavioralPressEvent>();
        var lineNumber = 1;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (line.Length == 0)
                throw Invalid(path, lineNumber, "la fila está vacía");

            var values = CsvLine.Parse(line, path, lineNumber);
            RequireColumnCount(values, 8, path, lineNumber);

            presses.Add(new BehavioralPressEvent(
                ReadWhole(values[0], "evento_sesion", path, lineNumber),
                ReadNumber(values[1], "tiempo_s", path, lineNumber),
                RequireText(values[2], "fase", path, lineNumber),
                ReadOptionalWhole(values[3], "ensayo", path, lineNumber),
                RequireText(values[4], "tipo_evento", path, lineNumber),
                ReadSide(values[5], path, lineNumber),
                ReadWhole(values[6], "contador_lado_sesion", path, lineNumber),
                ReadWhole(values[7], "contador_hardware", path, lineNumber),
                values));
        }

        return presses;
    }

    private static void RequireHeader(string? actual, string expected, string path)
    {
        if (actual != expected)
            throw new BehavioralDataFormatException($"'{Path.GetFileName(path)}' no tiene el encabezado CSV esperado.");
    }

    private static void RequireColumnCount(IReadOnlyList<string> values, int expected, string path, int lineNumber)
    {
        if (values.Count != expected)
            throw Invalid(path, lineNumber, $"se esperaban exactamente {expected} columnas y llegaron {values.Count}");
    }

    private static int ReadSide(string value, string path, int lineNumber)
    {
        var side = ReadWhole(value, "lado", path, lineNumber);
        if (side is -2 or 0 or 1)
            return side;

        throw Invalid(path, lineNumber, "lado debe ser 1 (izquierda), 0 (derecha) o -2 (timeout)");
    }

    private static int ReadBinary(string value, string field, string path, int lineNumber)
    {
        var number = ReadWhole(value, field, path, lineNumber);
        if (number is 0 or 1)
            return number;

        throw Invalid(path, lineNumber, $"{field} debe ser 0 o 1");
    }

    private static BehavioralEventType ReadEventType(string value, string path, int lineNumber)
    {
        var eventType = ReadWhole(value, "tipo_evento", path, lineNumber);
        return eventType switch
        {
            0 => BehavioralEventType.SafeFood,
            1 => BehavioralEventType.ConflictWithFood,
            2 => BehavioralEventType.SoundOnly,
            _ => throw Invalid(path, lineNumber, "tipo_evento debe ser 0, 1 o 2"),
        };
    }

    private static int ReadWhole(string value, string field, string path, int lineNumber)
    {
        var number = ReadNumber(value, field, path, lineNumber);
        if (number < int.MinValue || number > int.MaxValue || Math.Truncate(number) != number)
            throw Invalid(path, lineNumber, $"{field} debe ser un entero");

        return (int)number;
    }

    private static int? ReadOptionalWhole(string value, string field, string path, int lineNumber)
    {
        if (value == "NA")
            return null;

        return ReadWhole(value, field, path, lineNumber);
    }

    private static double ReadNumber(string value, string field, string path, int lineNumber)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
            !double.IsNaN(number) && !double.IsInfinity(number))
            return number;

        throw Invalid(path, lineNumber, $"{field} debe ser numérico con punto decimal cuando aplique");
    }

    private static string RequireText(string value, string field, string path, int lineNumber) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw Invalid(path, lineNumber, $"{field} no puede estar vacío");

    private static BehavioralDataFormatException Invalid(string path, int lineNumber, string detail) =>
        new($"'{Path.GetFileName(path)}', fila {lineNumber}: {detail}.");

    private static class CsvLine
    {
        public static IReadOnlyList<string> Parse(string line, string path, int lineNumber)
        {
            var values = new List<string>();
            var field = new StringBuilder();
            var quoted = false;

            for (var index = 0; index < line.Length; index++)
            {
                var character = line[index];
                if (character == '"')
                {
                    if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (character == ',' && !quoted)
                {
                    values.Add(field.ToString());
                    field.Clear();
                }
                else
                {
                    field.Append(character);
                }
            }

            if (quoted)
                throw Invalid(path, lineNumber, "hay una comilla CSV sin cerrar");

            values.Add(field.ToString());
            return values;
        }
    }
}
