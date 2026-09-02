using MathNet.Numerics.Data.Matlab;

namespace VideoBatchProcessor.Core.BehavioralData;

/// <summary>
/// Lee la matriz numérica de resultados de archivos MAT Level-5 históricos sin
/// requerir MATLAB. Acepta <c>Resultados</c> o una única matriz N×8/N×9.
/// </summary>
public sealed class MatV5MatrixReader : ILegacyMatMatrixReader
{
    public IReadOnlyList<IReadOnlyList<double>> ReadRows(string matPath)
    {
        if (string.IsNullOrWhiteSpace(matPath))
            throw new ArgumentException("Se requiere una ruta MAT.", nameof(matPath));
        if (!File.Exists(matPath))
            throw new FileNotFoundException("No se encontró el archivo MAT.", matPath);

        try
        {
            var matrices = MatlabReader.List(matPath);
            var candidates = matrices
                .Select(matrix => new { Matrix = matrix, Values = MatlabReader.Unpack<double>(matrix) })
                .Where(item => item.Values.RowCount > 0 && item.Values.ColumnCount is 8 or 9)
                .ToList();

            var selected = candidates.FirstOrDefault(item =>
                    string.Equals(item.Matrix.Name, "Resultados", StringComparison.OrdinalIgnoreCase))
                ?? candidates.SingleOrDefault();

            if (selected is null)
            {
                var names = matrices.Count == 0
                    ? "ninguna matriz"
                    : string.Join(", ", matrices.Select(matrix => matrix.Name));
                throw new BehavioralDataFormatException(
                    $"'{Path.GetFileName(matPath)}' no contiene una matriz de resultados N×8/N×9. Matrices encontradas: {names}.");
            }

            return Enumerable.Range(0, selected.Values.RowCount)
                .Select(row => (IReadOnlyList<double>)Enumerable.Range(0, selected.Values.ColumnCount)
                    .Select(column => selected.Values[row, column])
                    .ToArray())
                .ToArray();
        }
        catch (BehavioralDataFormatException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new BehavioralDataFormatException(
                $"No se pudo leer '{Path.GetFileName(matPath)}' como MAT Level-5: {exception.Message}");
        }
    }
}
