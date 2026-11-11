namespace Venture.OS.Desktop;

/// <summary>Something that happened to a window this frame: one of its view's events, or its closing.</summary>
public readonly record struct WindowEvent(DesktopWindow Window, UIEvent? Event, bool Closed = false);

/// <summary>
/// The desktop's windows: their stacking order, which one has focus, and where each frame's input goes.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>A press on a window brings it to the front and gives it focus. A press on its title bar
/// starts dragging it; a press and release on its close button closes it.</description></item>
/// <item><description>Once a press lands in a window's content, that window gets the pointer until the button comes
/// up, so a drag that leaves it still ends where it started.</description></item>
/// <item><description>Keys go to the window in front; the wheel goes to the window under the pointer.</description></item>
/// </list>
/// </remarks>
public sealed class WindowManager(int screenWidth, int screenHeight)
{
    // How far each new window sits from the last, so they don't open exactly on top of each other.
    private const int Cascade = 24;

    private readonly List<DesktopWindow> windows = [];
    private bool wasDown;
    private DesktopWindow? captured;
    private DesktopWindow? closing;
    private (DesktopWindow Window, int OffsetX, int OffsetY)? drag;

    /// <summary>The windows from back to front.</summary>
    public IReadOnlyList<DesktopWindow> Windows => windows;

    /// <summary>The window in front, which has focus, or <see langword="null"/>.</summary>
    public DesktopWindow? Focused => windows.Count == 0 ? null : windows[^1];

    /// <summary>The top of the area windows may use, below anything along the screen's top edge.</summary>
    public int TopInset { get; set; }

    /// <summary>Opens a window in front, centered, a little down and right of the last one opened.</summary>
    public void Open(DesktopWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (windows.Contains(window))
        {
            BringToFront(window);
            return;
        }

        var offset = Cascade * (windows.Count % 6);
        window.X = Math.Max(0, ((screenWidth - window.Width) / 2) + offset);
        window.Y = Math.Max(TopInset, ((screenHeight - window.Height) / 2) + offset - Cascade);
        Focused?.View.ClearFocus();
        windows.Add(window);
    }

    /// <summary>Closes a window, if it's open.</summary>
    /// <returns>Whether it was open.</returns>
    public bool Close(DesktopWindow window)
    {
        if (!windows.Remove(window))
            return false;

        if (captured == window)
            captured = null;
        if (closing == window)
            closing = null;
        if (drag?.Window == window)
            drag = null;
        return true;
    }

    /// <summary>Handles a frame's input.</summary>
    /// <returns>What happened to windows, in order.</returns>
    public IReadOnlyList<WindowEvent> Update(DesktopInput input)
    {
        List<WindowEvent> happened = [];
        var (x, y) = (input.PointerX, input.PointerY);
        var pressed = input.PointerDown && !wasDown;
        var released = !input.PointerDown && wasDown;
        wasDown = input.PointerDown;

        if (pressed && WindowAt(x, y) is { } target)
        {
            BringToFront(target);
            if (target.IsClosable && target.CloseButton.Contains(x, y))
                closing = target;
            else if (target.TitleBar.Contains(x, y))
                drag = (target, x - target.X, y - target.Y);
            else
                captured = target;
        }

        if (drag is { } moving && input.PointerDown)
        {
            // Keep the title bar on the screen, where it can be grabbed again.
            moving.Window.X = Math.Clamp(x - moving.OffsetX, 40 - moving.Window.Width, screenWidth - 40);
            moving.Window.Y = Math.Clamp(y - moving.OffsetY, TopInset, screenHeight - DesktopTheme.TitleBarHeight);
        }

        if (released && closing is { } window)
        {
            if (window.CloseButton.Contains(x, y))
            {
                Close(window);
                happened.Add(new WindowEvent(window, null, Closed: true));
            }

            closing = null;
        }

        // The view gets the frame when it holds the pointer, or for keys and the wheel.
        var receiver = captured ?? (input.Scroll != 0 ? WindowAt(x, y) : null) ?? Focused;
        if (receiver is not null && windows.Contains(receiver))
        {
            var keys = receiver == Focused ? input.KeysPressed : [];
            var scroll = WindowAt(x, y) == receiver ? input.Scroll : 0;
            var viewPressed = pressed && captured == receiver;
            var (originX, originY) = receiver.ContentOrigin;
            UIViewInput viewInput = new(x, y, captured == receiver && input.PointerDown, viewPressed, released && captured == receiver, scroll, keys);

            foreach (var uiEvent in receiver.View.Update(viewInput, originX, originY))
            {
                happened.Add(new WindowEvent(receiver, uiEvent));
                receiver.EventHandler?.Invoke(receiver, uiEvent);
            }
        }

        if (released)
        {
            captured = null;
            drag = null;
        }

        return happened;
    }

    /// <summary>Draws the windows from back to front; only the front one draws as active.</summary>
    public void Draw(IPainter painter)
    {
        foreach (var window in windows)
            window.Draw(painter, window == Focused);
    }

    /// <summary>The frontmost window under a point, or <see langword="null"/>.</summary>
    public DesktopWindow? WindowAt(int x, int y)
    {
        for (var index = windows.Count - 1; index >= 0; index--)
        {
            if (windows[index].Bounds.Contains(x, y))
                return windows[index];
        }

        return null;
    }

    private void BringToFront(DesktopWindow window)
    {
        if (Focused == window)
            return;

        Focused?.View.ClearFocus();
        windows.Remove(window);
        windows.Add(window);
    }
}
