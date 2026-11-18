using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Venture.Roundhouse.Simulator;

/// <summary>
/// The window VentureOS runs in, beside the designer: a 1024x768 screen, as the kernel asks Limine for, over a
/// fresh copy of VentureOS's files that's deleted when the window closes.
/// </summary>
public sealed class SimulatorWindow : Window
{
    /// <summary>The simulated screen's size, the kernel's.</summary>
    public const int ScreenWidth = 1024;

    /// <inheritdoc cref="ScreenWidth"/>
    public const int ScreenHeight = 768;

    private readonly TextBlock status = new()
    {
        Foreground = Brushes.White,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        Margin = new(40),
    };

    public SimulatorWindow()
    {
        Title = $"VentureOS — {MainWindow.ProductTitle}";
        Width = ScreenWidth;
        Height = ScreenHeight;
        CanResize = false;
        Content = new Border { Background = new SolidColorBrush(Color.FromRgb(0x24, 0x24, 0x24)), Child = status };
        Closed += (_, _) => Disk?.Dispose();

        if (SimulatedDisk.FindRootfs() is not { } rootfs)
        {
            status.Text = "VentureOS's files (rootfs) aren't beside Roundhouse, so it can't run.";
            return;
        }

        try
        {
            Disk = SimulatedDisk.Create(rootfs);
            System = new SimulatedSystem(Disk);
            status.Text = $"{System.Name} {System.Version} will run here, from a fresh copy of its files.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            status.Text = $"VentureOS's files couldn't be copied to run from: {exception.Message}";
        }
    }

    /// <summary>The run's copy of VentureOS's files, or <see langword="null"/> when it couldn't be made.</summary>
    public SimulatedDisk? Disk { get; }

    /// <summary>What the run tells its scripts about itself, or <see langword="null"/> without a disk.</summary>
    public SimulatedSystem? System { get; }

    /// <summary>What the window says: what will run, or why it can't.</summary>
    public string Status => status.Text ?? "";
}
