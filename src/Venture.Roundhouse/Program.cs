using Avalonia;

namespace Venture.Roundhouse;

/// <summary>Starts Roundhouse.</summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>Roundhouse's Avalonia app, which the headless tests build too.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
