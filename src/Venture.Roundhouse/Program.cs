using Avalonia;
using Venture.Roundhouse.Logging;

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

        // The log starts first, so even a failure to start leaves a trail; errors no one catches are logged before
        // Roundhouse goes down.
        RoundhouseLog.Configure(RoundhouseLog.DefaultFolder);
        var log = RoundhouseLog.For("Roundhouse");
        log.Info("{Title} starting", MainWindow.ProductTitle);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            log.Fatal(e.ExceptionObject as Exception, "Roundhouse stopped on an error nothing caught");
            RoundhouseLog.Flush();
        };

        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception exception)
        {
            log.Fatal(exception, "Roundhouse stopped on an error nothing caught");
            throw;
        }
        finally
        {
            RoundhouseLog.Flush();
        }
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
