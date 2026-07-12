using System.Globalization;

namespace VideoBatchProcessor.Core.BehavioralData;

/// <summary>
/// Punto de integración para el lector binario MAT que se implemente después.
/// El contrato que entrega al resto del producto ya es neutral y estable.
/// </summary>
public interface ILegacyMatMatrixReader
{
    IReadOnlyList<IReadOnlyList<double>> ReadRows(string matPath);
}

public sealed class LegacyMatBehavioralSessionReader(ILegacyMatMatrixReader matrixReader) : IBehavioralSessionReader
{
    public BehavioralSourceKind SourceKind => BehavioralSourceKind.LegacyMat;

    public BehavioralSessionData Read(BehavioralSourceResolution source)
    {
        if (!source.IsResolved || source.SourceKind != SourceKind || source.SourcePath is null)
            throw new ArgumentException("La resolución debe apuntar a un MAT legacy válido.", nameof(source));

        var events = LegacyMatEventMapper.MapRows(matrixReader.ReadRows(source.SourcePath));
        return new BehavioralSessionData(source, events, [], source.Warnings);
    }
}

/// <summary>
/// Normaliza las matrices N×8 y N×9 históricas. N×8 deriva el tipo a partir de
/// Estim; N×9 usa TipoEvento explícitamente.
/// </summary>
public static class LegacyMatEventMapper
{
    public static IReadOnlyList<BehavioralEvent> MapRows(IEnumerable<IReadOnlyList<double>> rows)
    {
        var events = new List<BehavioralEvent>();
        var expectedColumns = 0;
        var rowNumber = 0;

        foreach (var row in rows)
        {
            rowNumber++;
            if (row.Count is not 8 and not 9)
                throw new BehavioralDataFormatException($"MAT legacy, fila {rowNumber}: se esperaban 8 o 9 columnas.");

            if (expectedColumns == 0)
                expectedColumns = row.Count;
            else if (row.Count != expectedColumns)
                throw new BehavioralDataFormatException($"MAT legacy mezcla filas de {expectedColumns} y {row.Count} columnas.");

            var side = ReadSide(row[1], rowNumber);
            var stimulus = ReadBinary(row[2], "Estim", rowNumber);
            var eventType = row.Count == 9
                ? ReadEventType(row[8], rowNumber)
                : stimulus == 0 ? BehavioralEventType.SafeFood : BehavioralEventType.ConflictWithFood;

            events.Add(new BehavioralEvent(
                ReadWhole(row[0], "Ensayo", rowNumber),
                side,
                stimulus,
                ReadFinite(row[3], "Latencia", rowNumber),
                ReadFinite(row[4], "TiempoAbs", rowNumber),
                ReadWhole(row[5], "PalancasIzq", rowNumber),
                ReadWhole(row[6], "PalancasDer", rowNumber),
                ReadFinite(row[7], "Desplaz", rowNumber),
                eventType,
                row.Select(value => value.ToString("R", CultureInfo.InvariantCulture)).ToArray()));
        }

        return events;
    }

    private static int ReadSide(double value, int rowNumber)
    {
        var side = ReadWhole(value, "Lado", rowNumber);
        return side is -2 or 0 or 1
            ? side
            : throw new BehavioralDataFormatException($"MAT legacy, fila {rowNumber}: Lado debe ser 1, 0 o -2.");
    }

    private static int ReadBinary(double value, string field, int rowNumber)
    {
        var number = ReadWhole(value, field, rowNumber);
        return number is 0 or 1
            ? number
            : throw new BehavioralDataFormatException($"MAT legacy, fila {rowNumber}: {field} debe ser 0 o 1.");
    }

    private static BehavioralEventType ReadEventType(double value, int rowNumber) =>
        ReadWhole(value, "TipoEvento", rowNumber) switch
        {
            0 => BehavioralEventType.SafeFood,
            1 => BehavioralEventType.ConflictWithFood,
            2 => BehavioralEventType.SoundOnly,
            _ => throw new BehavioralDataFormatException($"MAT legacy, fila {rowNumber}: TipoEvento debe ser 0, 1 o 2."),
        };

    private static int ReadWhole(double value, string field, int rowNumber)
    {
        var number = ReadFinite(value, field, rowNumber);
        if (number < int.MinValue || number > int.MaxValue || Math.Truncate(number) != number)
            throw new BehavioralDataFormatException($"MAT legacy, fila {rowNumber}: {field} debe ser un entero.");

        return (int)number;
    }

    private static double ReadFinite(double value, string field, int rowNumber) =>
        !double.IsNaN(value) && !double.IsInfinity(value)
            ? value
            : throw new BehavioralDataFormatException($"MAT legacy, fila {rowNumber}: {field} debe ser finito.");
}
