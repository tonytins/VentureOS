using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using NLog;

namespace Venture.Roundhouse.Vdb;

/// <summary>
/// A run of the app being edited on a running VentureOS: what Roundhouse is doing, or why it stopped, along the top,
/// and what the app prints below, until Stop or the window closes, which ends the connection.
/// </summary>
public sealed class VentureOSRunWindow : Window
{
    private static readonly Logger Log = LogManager.GetLogger("VentureOS");

    private readonly CancellationTokenSource stop = new();
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBox output = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        FontFamily = new FontFamily("monospace"),
    };
    private readonly Button stopButton = new() { Content = "Stop", HorizontalAlignment = HorizontalAlignment.Right };

    /// <param name="app">The app's folder name, such as <c>Dice.app</c>, for the title.</param>
    /// <param name="start">Starts the run on a thread of its own, reporting through the action given, until the token is cancelled.</param>
    public VentureOSRunWindow(string app, Func<Action<VentureOSRunLine>, CancellationToken, Task> start)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(start);
        Title = $"{app} on VentureOS";
        Width = 520;
        Height = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        stopButton.Click += (_, _) => Stop();
        DockPanel.SetDock(status, Dock.Top);
        DockPanel.SetDock(stopButton, Dock.Bottom);
        Content = new DockPanel
        {
            Margin = new(12),
            LastChildFill = true,
            Children = { status, stopButton, output },
        };
        output.Margin = new(0, 8);

        Closed += (_, _) => Stop();
        Running = start(line => Dispatcher.UIThread.Post(() => Show(line)), stop.Token)
            .ContinueWith(_ => Dispatcher.UIThread.Post(() => stopButton.IsEnabled = false), TaskScheduler.Default);
    }

    /// <summary>The run, which ends once it stops; tests wait on it.</summary>
    public Task Running { get; }

    /// <summary>What the app has printed so far.</summary>
    public string Output => output.Text ?? "";

    /// <summary>What Roundhouse last said about the run.</summary>
    public string Status => status.Text ?? "";

    /// <summary>Ends the run, closing the connection; the window stays, with what was printed.</summary>
    public void Stop()
    {
        if (!stop.IsCancellationRequested)
            stop.Cancel();
    }

    private void Show(VentureOSRunLine line)
    {
        if (line.IsOutput)
        {
            output.Text = output.Text is { Length: > 0 } before ? before + "\n" + line.Text : line.Text;
            output.CaretIndex = output.Text.Length;
            Log.Info(line.Text);
            return;
        }

        status.Text = line.Text;
        if (line.IsProblem)
            status.Foreground = Brushes.IndianRed;
        else
            status.ClearValue(TextBlock.ForegroundProperty);
        if (line.IsProblem)
            Log.Warn(line.Text);
        else
            Log.Info(line.Text);
    }
}
