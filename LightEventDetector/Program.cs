using Avalonia;
using LightEventDetector;

// Punto de entrada de la aplicación Avalonia.
// [STAThread] es necesario para COM interop en Windows (file picker, etc.).

class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()    // detecta Windows / macOS / Linux automáticamente
            .LogToTrace();
}