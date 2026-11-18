using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Venture.Roundhouse;

/// <summary>Roundhouse's application: its theme, and its main window.</summary>
public sealed class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Roundhouse document.uix opens it.
            MainWindow window = new();
            if (desktop.Args is [var path, ..])
                window.Open(path);
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
