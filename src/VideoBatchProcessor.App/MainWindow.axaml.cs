using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using VideoBatchProcessor.Core.SessionResolver;

namespace VideoBatchProcessor.App;

public sealed partial class MainWindow : Window
{
    private readonly SessionSetupService _sessionSetup = new();
    private readonly ObservableCollection<SessionRow> _rows = [];
    private readonly Dictionary<string, TextBox> _missingFieldInputs = new();

    public MainWindow()
    {
        InitializeComponent();
        SessionsGrid.ItemsSource = _rows;
        ResetSelectionDetails();
        UpdateSummary();
    }

    private async void OpenFolder_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Abrir carpeta con videos de sesión",
            AllowMultiple = false,
        });

        var folderPath = folders.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        var files = Directory.EnumerateFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
            .Where(SessionSetupService.IsSupportedVideo);
        LoadSessions(files);
    }

    private async void AddFiles_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Agregar videos de sesión",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Video")
                {
                    Patterns = ["*.mp4", "*.avi", "*.mov", "*.mkv", "*.m4v"],
                },
            ],
        });

        LoadSessions(files.Select(file => file.Path.LocalPath));
    }

    private void LoadSessions(IEnumerable<string> paths)
    {
        _rows.Clear();
        foreach (var entry in _sessionSetup.AnalyzeFiles(paths))
            _rows.Add(new SessionRow(entry));

        SessionsGrid.SelectedItem = null;
        ResetSelectionDetails();
        UpdateSummary();
    }

    private void NewBatch_Click(object? sender, RoutedEventArgs e)
    {
        _rows.Clear();
        SessionsGrid.SelectedItem = null;
        ResetSelectionDetails();
        UpdateSummary();
    }

    private void SessionsGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SessionsGrid.SelectedItem is not SessionRow row)
        {
            ResetSelectionDetails();
            return;
        }

        SelectionPanel.IsVisible = true;
        ShowSelectedSession(row);
    }

    private void ShowSelectedSession(SessionRow row)
    {
        var metadata = row.Entry.Metadata;
        SelectedFileText.Text = row.FileName;
        SelectedSourceText.Text = DescribeBehavioralSource(metadata);
        SourceNoticeText.Text = metadata.BehavioralSourceError
            ?? (metadata.BehavioralSourceWarnings.Count == 0
                ? "Sin advertencias de fuente."
                : string.Join(" ", metadata.BehavioralSourceWarnings));
        SelectedStatusText.Text = metadata.FormatoNoReconocido && !metadata.IsComplete
            ? "El nombre no coincide con una nomenclatura conocida. Completa los datos para poder continuar."
            : metadata.FormatoNoReconocido
                ? "La metadata fue completada manualmente. Conservamos que el nombre fuente no seguía una nomenclatura conocida."
                : metadata.IsComplete
                    ? "La metadata está completa. Esta sesión estará lista para el siguiente paso."
                    : $"Faltan: {string.Join(", ", metadata.MissingFields)}.";

        MetadataFieldsPanel.Children.Clear();
        _missingFieldInputs.Clear();

        foreach (var field in metadata.MissingFields)
            AddMissingField(field);

        ApplyMetadataButton.IsVisible = metadata.MissingFields.Count > 0;
        FormStatusText.Text = string.Empty;
    }

    private void ResetSelectionDetails()
    {
        SelectionPanel.IsVisible = false;
        ApplyMetadataButton.IsVisible = false;
        SelectedFileText.Text = "Selecciona una sesión de la tabla.";
        SelectedSourceText.Text = "Aún no hay una sesión seleccionada.";
        SourceNoticeText.Text = string.Empty;
        FormStatusText.Text = string.Empty;
        MetadataFieldsPanel.Children.Clear();
        _missingFieldInputs.Clear();
    }

    private void AddMissingField(string field)
    {
        var input = new TextBox { Watermark = PlaceholderFor(field) };
        _missingFieldInputs[field] = input;
        MetadataFieldsPanel.Children.Add(new TextBlock { Text = DisplayNameFor(field) });
        MetadataFieldsPanel.Children.Add(input);
    }

    private void CompleteMetadata_Click(object? sender, RoutedEventArgs e)
    {
        if (SessionsGrid.SelectedItem is not SessionRow row)
            return;

        var values = new UserFieldValues
        {
            Iniciales = ReadText(nameof(SessionMetadata.Iniciales)),
            Sexo = ReadText(nameof(SessionMetadata.Sexo)),
            Tratamiento = ReadText(nameof(SessionMetadata.Tratamiento)),
            Fecha = ReadText(nameof(SessionMetadata.Fecha)),
            Fase = ReadText(nameof(SessionMetadata.Fase)),
            Dia = ReadPositiveInt(nameof(SessionMetadata.Dia)),
            Rata = ReadPositiveInt(nameof(SessionMetadata.Rata)),
        };

        row.Update(_sessionSetup.Complete(row.Entry, values));
        ShowSelectedSession(row);
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var ready = _rows.Count(row => row.Entry.Metadata.IsComplete);
        var unknown = _rows.Count(row => row.Entry.Metadata.FormatoNoReconocido);
        SummaryText.Text = _rows.Count == 0
            ? "Selecciona una carpeta o agrega videos para comenzar."
            : $"{_rows.Count} video(s): {ready} listos, {_rows.Count - ready} con datos pendientes, {unknown} con formato no reconocido.";
    }

    private static string DescribeBehavioralSource(SessionMetadata metadata)
    {
        if (metadata.SourceBehavioralPath is null)
            return "Sin fuente conductual asociada.";

        var kind = metadata.SourceBehavioralKind switch
        {
            "CsvV1" => "CSV V1",
            "LegacyMat" => "MAT histórico",
            _ => "Fuente conductual",
        };

        return $"{kind}: {Path.GetFileName(metadata.SourceBehavioralPath)}";
    }

    private string? ReadText(string field) =>
        _missingFieldInputs.TryGetValue(field, out var input)
            ? input.Text?.Trim()
            : null;

    private int? ReadPositiveInt(string field) =>
        int.TryParse(ReadText(field), out var number) && number > 0 ? number : null;

    private static string DisplayNameFor(string field) => field switch
    {
        nameof(SessionMetadata.Iniciales) => "Iniciales",
        nameof(SessionMetadata.Sexo) => "Sexo",
        nameof(SessionMetadata.Tratamiento) => "Tratamiento",
        nameof(SessionMetadata.Fecha) => "Fecha",
        nameof(SessionMetadata.Fase) => "Fase",
        nameof(SessionMetadata.Dia) => "Día",
        nameof(SessionMetadata.Rata) => "Rata",
        _ => field,
    };

    private static string PlaceholderFor(string field) => field switch
    {
        nameof(SessionMetadata.Iniciales) => "Ejemplo: abs",
        nameof(SessionMetadata.Sexo) => "m o h",
        nameof(SessionMetadata.Tratamiento) => "Ejemplo: stx o dzp",
        nameof(SessionMetadata.Fecha) => "YYMM, por ejemplo 2601",
        nameof(SessionMetadata.Fase) => "Ejemplo: f5",
        nameof(SessionMetadata.Dia) => "Número de día",
        nameof(SessionMetadata.Rata) => "Número de rata",
        _ => field,
    };
}
