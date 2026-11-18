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

    /// <summary>Roundhouse's Avalonia app.</summary>
    /// <remarks>
    /// On X11, Avalonia leaves the window manager to draw the title bar unless it's asked to draw the frame itself,
    /// which the coloured bar needs to be the title bar; Passenger asks the same. The option is marked experimental
    /// in Avalonia 12, so it's on only where <see cref="TitleBars.Merges()"/> would merge the bar.
    /// </remarks>
    public static AppBuilder BuildAvaloniaApp()
    {
#pragma warning disable AVALONIA_X11_CSD // Experimental; Passenger draws its merged title bar on X11 the same way.
        X11PlatformOptions x11 = new() { EnableDrawnDecorations = TitleBars.Merges() };
#pragma warning restore AVALONIA_X11_CSD
        return AppBuilder.Configure<App>().UsePlatformDetect().With(x11).LogToTrace();
    }
}
