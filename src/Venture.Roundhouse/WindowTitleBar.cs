using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Venture.OS.Desktop;

namespace Venture.Roundhouse;

/// <summary>
/// Roundhouse's coloured bar, after Passenger's and other .NET IDEs': VentureOS's blue running into its teal, laid out
/// as VentureOS's own title bar is, with the window's buttons and the <see cref="Start"/> items at the left, the
/// <see cref="Title"/> centred, and the <see cref="End"/> items at the right. Where the desktop allows
/// (<see cref="TitleBars.Merges()"/>), it's the window's title bar; otherwise it sits under the window manager's.
/// </summary>
/// <remarks>
/// Every Roundhouse window with a bar uses this one, so they look and behave alike: put the items in it, then call
/// <see cref="Attach"/> from the window once its content is built. Its look is in <c>TitleBarStyles.axaml</c>.
/// </remarks>
public sealed class WindowTitleBar : Border
{
    /// <summary>The bar's height, which is the title bar's where they merge.</summary>
    public const double BarHeight = 44;

    private readonly TitleBarPanel content = new() { Margin = new(10, 0) };
    private readonly StackPanel startItems = new() { Orientation = Orientation.Horizontal, Spacing = 2 };
    private readonly StackPanel endItems = new() { Orientation = Orientation.Horizontal, Spacing = 2 };
    private readonly ContentControl title = new() { VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
    private Window? host;

    public WindowTitleBar()
    {
        Height = BarHeight;
        Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#3584E4"), 0), new GradientStop(Color.Parse("#2190A4"), 1) },
        };

        // VentureOS's red close, orange minimize, and green zoom, drawn where Roundhouse draws the frame.
        var palette = DesktopPalette.Light;
        CloseButton = WindowButton("CloseButton", "Close", palette.CloseButton, out var closeDot);
        MinimizeButton = WindowButton("MinimizeButton", "Minimize", palette.MinimizeButton, out _);
        MaximizeButton = WindowButton("MaximizeButton", "Zoom", palette.ZoomButton, out _);
        CloseDot = closeDot;
        CloseButton.Click += (_, _) => host?.Close();
        MinimizeButton.Click += (_, _) =>
        {
            if (host is not null)
                host.WindowState = WindowState.Minimized;
        };
        MaximizeButton.Click += (_, _) => ToggleMaximized();
        WindowButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
            Children = { CloseButton, MinimizeButton, MaximizeButton },
        };

        // VentureOS's order: the window's buttons first, at the left end, then the start items.
        startItems.Children.Add(WindowButtons);
        content.Children.Add(startItems);
        content.Children.Add(title);
        content.Children.Add(endItems);
        Child = content;
        DoubleTapped += OnDoubleTapped;
    }

    /// <summary>The items at the bar's left end, after the window's buttons.</summary>
    public Avalonia.Controls.Controls Start => startItems.Children;

    /// <summary>The items at the bar's right end.</summary>
    public Avalonia.Controls.Controls End => endItems.Children;

    /// <summary>What's centred in the bar, such as the document's name; it takes no clicks, so the bar drags under it.</summary>
    public Control? Title
    {
        get => title.Content as Control;
        set => title.Content = value;
    }

    /// <summary>The panel that holds the window's buttons and then the <see cref="Start"/> items.</summary>
    public StackPanel StartItems => startItems;

    /// <summary>The window's buttons, close, minimize, and zoom, shown where Roundhouse draws them (<see cref="DrawsWindowButtons"/>).</summary>
    public StackPanel WindowButtons { get; }

    public Button CloseButton { get; }

    public Button MinimizeButton { get; }

    public Button MaximizeButton { get; }

    /// <summary>The close button's dot, in VentureOS's red.</summary>
    public Ellipse CloseDot { get; }

    /// <summary>Whether the bar is the window's title bar.</summary>
    public bool IsMerged { get; private set; }

    /// <summary>Whether Roundhouse draws the window's buttons in the bar: where the bar is the title bar on Linux.</summary>
    public bool DrawsWindowButtons =>
        IsMerged && OperatingSystem.IsLinux() && host is { WindowState: not WindowState.FullScreen };

    /// <summary>
    /// Makes the bar <paramref name="window"/>'s title bar, where <paramref name="merged"/> says it can be, with the
    /// window buttons in it on Linux and beside it, the system's own, on Windows and macOS; or leaves the window
    /// manager's title bar above it. Call it once, after the bar's items are in place.
    /// </summary>
    /// <exception cref="InvalidOperationException">The bar is already attached to a window.</exception>
    public void Attach(Window window, bool merged)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (host is not null)
            throw new InvalidOperationException("The title bar already belongs to a window.");

        host = window;
        IsMerged = merged;
        window.ExtendClientAreaToDecorationsHint = merged;
        window.ExtendClientAreaTitleBarHeightHint = merged ? Height : -1;

        // The bar drags the window; what's on it takes clicks.
        WindowDecorationProperties.SetElementRole(this, merged ? WindowDecorationsElementRole.TitleBar : WindowDecorationsElementRole.None);
        foreach (var control in this.GetLogicalDescendants().OfType<Button>())
            WindowDecorationProperties.SetElementRole(control, merged ? WindowDecorationsElementRole.User : WindowDecorationsElementRole.None);

        // On Linux, Avalonia draws the frame and Roundhouse the buttons; the frame draws nothing else (see FrameOnly
        // in TitleBarStyles.axaml). Windows and macOS keep their own buttons beside the bar.
        if (merged && OperatingSystem.IsLinux()
            && Application.Current?.TryFindResource("FrameOnly", out var theme) == true && theme is ControlTheme frameOnly)
            window.WindowDecorationsTheme = frameOnly;

        window.PropertyChanged += OnHostPropertyChanged;
        ShowWindowButtons();
        PlaceContent();
    }

    private static Button WindowButton(string name, string tip, OS.Interface.UIColor color, out Ellipse dot)
    {
        dot = new Ellipse { Width = 12, Height = 12, Fill = new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue)) };
        Button button = new() { Name = name, Content = dot };
        button.Classes.Add("light");
        ToolTip.SetTip(button, tip);
        return button;
    }

    private void OnHostPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == Window.WindowDecorationMarginProperty)
            PlaceContent();
        else if (change.Property == Window.WindowStateProperty || change.Property == Window.CanResizeProperty)
            ShowWindowButtons();
    }

    private void ShowWindowButtons()
    {
        WindowButtons.IsVisible = DrawsWindowButtons;

        // A window that can't be resized can't be zoomed either, as VentureOS has it.
        MaximizeButton.IsEnabled = host?.CanResize ?? true;
        ToolTip.SetTip(MaximizeButton, host?.WindowState == WindowState.Maximized ? "Restore" : "Zoom");
        PlaceContent();
    }

    /// <summary>Keeps the bar's items clear of any window buttons the system draws beside it, as macOS's on the left.</summary>
    private void PlaceContent()
    {
        var system = IsMerged && !DrawsWindowButtons && host is not null ? host.WindowDecorationMargin : default;
        content.Margin = new Thickness(10 + system.Left, 0, 10 + system.Right, 0);
    }

    private void ToggleMaximized()
    {
        if (host is { CanResize: true })
            host.WindowState = host.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        // Double-clicking the bar itself, not a button on it, maximizes or restores, as a title bar does.
        if (IsMerged && e.Source is Visual source && source.FindAncestorOfType<Button>(includeSelf: true) is null)
            ToggleMaximized();
    }
}
