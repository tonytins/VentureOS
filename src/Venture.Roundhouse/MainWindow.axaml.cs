using Avalonia.Controls;

namespace Venture.Roundhouse;

/// <summary>Roundhouse's window, titled with the version it shares with VentureOS.</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>The window's title: Roundhouse and its version, such as <c>Roundhouse 0.6.101</c>.</summary>
    public static string ProductTitle => $"Roundhouse {ThisAssembly.Git.SemVer.Major}.{ThisAssembly.Git.SemVer.Minor}.{ThisAssembly.Git.SemVer.Patch}";

    public MainWindow()
    {
        InitializeComponent();
        Title = ProductTitle;
    }
}
