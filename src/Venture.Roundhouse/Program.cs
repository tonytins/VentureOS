using Avalonia;

namespace Venture.Roundhouse;

/// <summary>Starts Roundhouse.</summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // A render needs no window, so it runs before Avalonia starts, and works without a display.
        if (args.Length > 0 && args[0] == RenderCommand.Flag)
            return RenderCommand.Run(args[1..], Console.Error);

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>Roundhouse's Avalonia app, which the headless tests build too.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
