using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace Venture.Roundhouse;

/// <summary>Roundhouse's application: its theme, and its main window.</summary>
public sealed class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Roundhouse document.uix opens it. With nothing named, the welcome window shows in the main one's place,
            // if it's to, and Roundhouse runs until the main window closes, or the welcome window with nothing else.
            MainWindow window = new();
            if (desktop.Args is [var path, ..])
            {
                window.Open(path);
                desktop.MainWindow = window;
            }
            else if (window.ShowsWelcomeAtStartup)
            {
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                window.Closed += (_, _) => desktop.Shutdown();
                window.ShowWelcome();
            }
            else
            {
                desktop.MainWindow = window;
            }

            // An error in the window's work that nothing caught is logged and reported, and Roundhouse carries on, so
            // unsaved work isn't lost with it.
            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                window.ReportError(e.Exception);
                e.Handled = true;
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
