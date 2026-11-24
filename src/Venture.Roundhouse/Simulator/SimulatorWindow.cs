using Venture.Airo.Simulation;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Venture.OS.Desktop;
using Venture.Roundhouse.Drawing;

namespace Venture.Roundhouse.Simulator;

/// <summary>
/// The window VentureOS runs in, beside the designer: a 1024x768 screen, the kernel's, showing a
/// <see cref="SimulatedRun"/>, with the pointer, the wheel, and the keys handed to it each frame. VentureOS draws its
/// own pointer, so the computer's is hidden over the screen. Closing the window, or Return to Shell, ends the run and
/// deletes its copy of VentureOS's files.
/// </summary>
public sealed class SimulatorWindow : Window
{
    /// <summary>The simulated screen's size, the kernel's.</summary>
    public const int ScreenWidth = 1024;

    /// <inheritdoc cref="ScreenWidth"/>
    public const int ScreenHeight = 768;

    /// <summary>How often a frame runs: about sixty a second, though an idle desktop doesn't redraw.</summary>
    public static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(16);

    private readonly SkiaFonts fonts = SkiaFonts.LoadShipped();
    private readonly WriteableBitmap screen = new(
        new PixelSize(ScreenWidth, ScreenHeight), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
    private readonly Image view;
    private readonly TextBlock status = new()
    {
        Foreground = Brushes.White,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        Margin = new(40),
    };
    private readonly DispatcherTimer frames = new() { Interval = FrameInterval };
    private readonly List<KeyPress> keys = [];
    private (int X, int Y) pointer = (ScreenWidth / 2, ScreenHeight / 2);
    private bool pointerDown;
    private double scroll;

    /// <summary>Starts VentureOS with what's being designed open in it.</summary>
    /// <param name="document">The document being designed, by its path on this computer, or <see langword="null"/>.</param>
    /// <param name="edits">Files' edited text, by their paths, which run in place of what's saved.</param>
    public SimulatorWindow(string? document = null, IReadOnlyDictionary<string, string>? edits = null)
    {
        Title = $"VentureOS — {MainWindow.ProductTitle}";
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        view = new Image
        {
            Source = screen,
            Width = ScreenWidth,
            Height = ScreenHeight,
            Stretch = Stretch.None,
            Cursor = new Cursor(StandardCursorType.None),
        };
        RenderOptions.SetBitmapInterpolationMode(view, BitmapInterpolationMode.None);
        Content = new Grid
        {
            Background = new SolidColorBrush(Color.FromRgb(0x24, 0x24, 0x24)),
            Children = { view, status },
        };

        view.PointerMoved += (_, e) => Point(e.GetPosition(view));
        view.PointerPressed += (_, e) =>
        {
            Point(e.GetPosition(view));
            pointerDown = e.GetCurrentPoint(view).Properties.IsLeftButtonPressed || pointerDown;
            e.Pointer.Capture(view);
        };
        view.PointerReleased += (_, e) =>
        {
            Point(e.GetPosition(view));
            pointerDown = false;
            e.Pointer.Capture(null);
        };
        view.PointerWheelChanged += (_, e) => scroll -= e.Delta.Y;
        KeyDown += OnKeyDown;
        TextInput += (_, e) =>
        {
            foreach (var character in e.Text ?? "")
            {
                if (!char.IsControl(character))
                    keys.Add(KeyPress.Typed(character));
            }
        };
        frames.Tick += (_, _) => Frame();
        Closed += (_, _) =>
        {
            frames.Stop();
            Run?.Dispose();
            fonts.Dispose();
        };

        if (SimulatedDisk.FindRootfs() is not { } rootfs)
        {
            status.Text = "VentureOS's files (rootfs) aren't beside Roundhouse, so it can't run.";
            return;
        }

        try
        {
            Run = SimulatedRun.Start(rootfs, fonts, document, edits);
            Show(Run.Pixels);
            status.IsVisible = Run.Messages.Count > 0;
            status.Text = string.Join("\n", Run.Messages);
            frames.Start();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            status.Text = $"VentureOS's files couldn't be copied to run from: {exception.Message}";
        }
    }

    /// <summary>The run, or <see langword="null"/> when it couldn't start; <see cref="Status"/> says why.</summary>
    public SimulatedRun? Run { get; }

    /// <summary>What the window says over the screen: why the run couldn't start, or what went wrong opening the document.</summary>
    public string Status => status.Text ?? "";

    /// <summary>Runs one frame now, as the timer does, with the input gathered since the last.</summary>
    public void Frame()
    {
        if (Run is null)
            return;

        var wheel = (int)Math.Truncate(scroll);
        scroll -= wheel;
        KeyPress[] pressed = [.. keys];
        keys.Clear();
        if (Run.Step(pointer.X, pointer.Y, pointerDown, wheel, pressed))
            Show(Run.Pixels);
        if (Run.IsFinished)
            Close();
    }

    /// <summary>Hands the desktop a pointer position, as VentureOS's mouse would, on the screen.</summary>
    public void PointAt(int x, int y, bool down)
    {
        pointer = (Math.Clamp(x, 0, ScreenWidth - 1), Math.Clamp(y, 0, ScreenHeight - 1));
        pointerDown = down;
    }

    /// <summary>Hands the desktop a key, as VentureOS's keyboard would.</summary>
    public void Press(KeyPress key) => keys.Add(key);

    private void Point(Point position) => PointAt((int)position.X, (int)position.Y, pointerDown);

    private void Show(int[] pixels)
    {
        using (var locked = screen.Lock())
        {
            for (var row = 0; row < ScreenHeight; row++)
                Marshal.Copy(pixels, row * ScreenWidth, locked.Address + (row * locked.RowBytes), ScreenWidth);
        }

        view.InvalidateVisual();
    }

    /// <summary>
    /// The keys VentureOS treats specially, and letters with Ctrl or Alt for its shortcuts; plain typing arrives as
    /// text instead, so it's typed as the computer's keyboard layout has it.
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        DesktopKey? special = e.Key switch
        {
            Key.Back => DesktopKey.Backspace,
            Key.Delete => DesktopKey.Delete,
            Key.Enter => DesktopKey.Enter,
            Key.Escape => DesktopKey.Escape,
            Key.Tab => DesktopKey.Tab,
            Key.Left => DesktopKey.Left,
            Key.Right => DesktopKey.Right,
            Key.Up => DesktopKey.Up,
            Key.Down => DesktopKey.Down,
            Key.Home => DesktopKey.Home,
            Key.End => DesktopKey.End,
            _ => null,
        };

        if (special is { } key)
            keys.Add(new KeyPress(key, '\0', control, shift, alt));
        else if ((control || alt) && Letter(e.Key) is { } letter)
            keys.Add(new KeyPress(DesktopKey.Character, shift ? char.ToUpperInvariant(letter) : letter, control, shift, alt));
        else
            return;

        e.Handled = true;
    }

    private static char? Letter(Key key) => key switch
    {
        >= Key.A and <= Key.Z => (char)('a' + (key - Key.A)),
        >= Key.D0 and <= Key.D9 => (char)('0' + (key - Key.D0)),
        _ => null,
    };
}
