using System.ComponentModel;
using System.Runtime.CompilerServices;
using VideoBatchProcessor.Core.SessionResolver;

namespace VideoBatchProcessor.App;

public sealed class SessionRow : INotifyPropertyChanged
{
    public SessionRow(SessionSetupEntry entry) => Entry = entry;

    public SessionSetupEntry Entry { get; private set; }
    public string FileName => Path.GetFileName(Entry.Metadata.SourceVideoPath);
    public string Scheme => Entry.Metadata.Scheme.ToString();
    public string Date => Entry.Metadata.Fecha;
    public string Phase => Entry.Metadata.Fase;
    public string Day => Entry.Metadata.Dia == 0 ? "-" : Entry.Metadata.Dia.ToString();
    public string Rat => Entry.Metadata.Rata == 0 ? "-" : Entry.Metadata.Rata.ToString();
    public string Sex => Entry.Metadata.Sexo;
    public string Treatment => Entry.Metadata.Tratamiento;
    public string BehavioralSource => Entry.Metadata.SourceBehavioralPath is { } path
        ? $"{DisplaySourceKind(Entry.Metadata.SourceBehavioralKind)} · {Path.GetFileName(path)}"
        : "Sin fuente";
    public string MissingFields => Entry.Metadata.IsComplete
        ? "-"
        : string.Join(", ", Entry.Metadata.MissingFields);
    public string Status => Entry.Metadata.FormatoNoReconocido
        ? Entry.Metadata.IsComplete ? "Completado manualmente" : "Formato no reconocido"
        : Entry.Metadata.IsComplete ? "Listo" : "Datos pendientes";

    public void Update(SessionSetupEntry entry)
    {
        Entry = entry;
        foreach (var property in DisplayProperties)
            OnPropertyChanged(property);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static readonly string[] DisplayProperties =
    [
        nameof(FileName), nameof(Scheme), nameof(Date), nameof(Phase), nameof(Day),
        nameof(Rat), nameof(Sex), nameof(Treatment), nameof(BehavioralSource),
        nameof(MissingFields), nameof(Status),
    ];

    private static string DisplaySourceKind(string sourceKind) => sourceKind switch
    {
        "CsvV1" => "CSV V1",
        "LegacyMat" => "MAT histórico",
        _ => "Sin fuente",
    };
}
