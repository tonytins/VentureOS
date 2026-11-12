namespace Venture.OS.Desktop;

/// <summary>Something that happened to a window this frame: one of its events, or its closing.</summary>
public readonly record struct WindowEvent(DesktopWindow Window, UIEvent? Event, bool Closed = false);

/// <summary>
/// The desktop's windows: their stacking order, which one has focus, and where each frame's input goes.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>A press on a window brings it to the front and gives it focus. On the title bar, a press
/// drags the window, except on its buttons, toolbar items, and title field. The close and minimize buttons, and a
/// toolbar item, act when the button comes up over them.</description></item>
/// <item><description>Once a press lands in a window's content, that window gets the pointer until the button comes
/// up, so a drag that leaves it still ends where it started.</description></item>
/// <item><description>Keys go to the window in front: to its title while that's being edited, otherwise to its view.
/// The wheel goes to the window under the pointer.</description></item>
/// <item><description>A minimized window stays in the list, in the dock rather than on the desktop: it isn't drawn,
/// takes no input, and can't be in front. Opening it again restores it.</description></item>
/// </list>
/// </remarks>
public sealed class WindowManager(int screenWidth, int screenHeight)
{
    // How far each new window sits from the last, so they don't open exactly on top of each other.
    private const int Cascade = 24;

    private readonly List<DesktopWindow> windows = [];

    // Reused every frame, since the desktop runs its frame loop without pause; see Update.
    private readonly List<WindowEvent> frameEvents = [];
    private bool wasDown;
    private DesktopWindow? captured;
    private (DesktopWindow Window, FramePart Part, int Index)? armed;
    private (DesktopWindow Window, int OffsetX, int OffsetY)? drag;

    // Where the pointer was in the last update, and whether its button was down, for hover.
    private (int X, int Y) pointerAt;

    // Where the pointer came to rest and when, for hover text.
    private (int X, int Y) restingAt = (int.MinValue, int.MinValue);
    private TimeSpan restingSince;

    // A window being resized by its grip: where the press started, and the frame's size then.
    private (DesktopWindow Window, int StartX, int StartY, int Width, int Height)? resizing;

    /// <summary>The windows from back to front, minimized ones included.</summary>
    public IReadOnlyList<DesktopWindow> Windows => windows;

    /// <summary>The window in front, which has focus, or <see langword="null"/>. Never a minimized one.</summary>
    public DesktopWindow? Focused
    {
        get
        {
            for (var index = windows.Count - 1; index >= 0; index--)
            {
                if (!windows[index].IsMinimized)
                    return windows[index];
            }

            return null;
        }
    }

    /// <summary>The top of the area windows may use, below anything along the screen's top edge.</summary>
    public int TopInset { get; set; }

    /// <summary>
    /// Changes the screen the windows are on. Windows outlive a visit to the desktop, since apps open them from the
    /// shell too, so the screen is only known once the desktop shows; windows already open stay where they are.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A size isn't positive.</exception>
    public void SetScreenSize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        screenWidth = width;
        screenHeight = height;
    }

    /// <summary>
    /// Opens a window in front, centered, a little down and right of the last one opened. A window that's already
    /// open comes to the front, restored from the dock if it was minimized.
    /// </summary>
    public void Open(DesktopWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.ClearCloseRequest();
        if (windows.Contains(window))
        {
            window.IsMinimized = false;
            BringToFront(window);
            return;
        }

        var offset = Cascade * (windows.Count % 6);
        window.X = Math.Max(0, ((screenWidth - window.Width) / 2) + offset);
        window.Y = Math.Max(TopInset, ((screenHeight - window.Height) / 2) + offset - Cascade);
        Unfocus(Focused);
        windows.Add(window);
    }

    /// <summary>Sends a window to the dock.</summary>
    /// <returns>Whether it was open and not already minimized.</returns>
    public bool Minimize(DesktopWindow window)
    {
        if (!windows.Contains(window) || window.IsMinimized)
            return false;

        Unfocus(window);
        window.IsMinimized = true;
        Release(window);
        return true;
    }

    /// <summary>Closes a window, if it's open.</summary>
    /// <returns>Whether it was open.</returns>
    public bool Close(DesktopWindow window)
    {
        if (!windows.Remove(window))
            return false;

        Release(window);
        return true;
    }

    /// <summary>Whether a window is open, minimized or not.</summary>
    public bool Contains(DesktopWindow window) => windows.Contains(window);

    /// <summary>Handles a frame's input.</summary>
    /// <returns>What happened to windows, in order. A frame where nothing happens allocates nothing.</returns>
    public IReadOnlyList<WindowEvent> Update(DesktopInput input)
    {
        var happened = frameEvents;
        happened.Clear();
        var (x, y) = (input.PointerX, input.PointerY);
        pointerAt = (x, y);
        var pressed = input.PointerDown && !wasDown;

        // Hover text waits for the pointer to rest, and goes as soon as it moves or presses.
        if ((x, y) != restingAt || input.PointerDown || !ShowsHover)
        {
            restingAt = (x, y);
            restingSince = input.Time;
            Tooltip = null;
        }
        else if (Tooltip is null && input.Time - restingSince >= TooltipDelay)
        {
            Tooltip = WindowAt(x, y)?.TooltipAt(x, y);
        }
        var released = !input.PointerDown && wasDown;
        wasDown = input.PointerDown;

        if (pressed)
            Press(x, y);

        if (drag is { } moving && input.PointerDown)
        {
            // Keep the title bar on the screen, where it can be grabbed again.
            moving.Window.X = Math.Clamp(x - moving.OffsetX, 40 - moving.Window.Width, screenWidth - 40);
            moving.Window.Y = Math.Clamp(y - moving.OffsetY, TopInset, screenHeight - DesktopTheme.TitleBarHeight);
        }

        // Resizing keeps the frame on the screen; the window keeps itself no smaller than its document allows.
        if (resizing is { } sizing && input.PointerDown)
        {
            var window = sizing.Window;
            window.Resize(
                Math.Min(sizing.Width + x - sizing.StartX, screenWidth - window.X),
                Math.Min(sizing.Height + y - sizing.StartY, screenHeight - window.Y));
        }

        if (released && armed is { } button)
        {
            armed = null;
            var (part, index) = button.Window.Hit(x, y);
            if (part == button.Part && index == button.Index && windows.Contains(button.Window))
                Act(button.Window, part, index, happened);
            else
                button.Window.CancelTitleEdit();
        }

        // The title takes the keys while it's being edited.
        var focused = Focused;
        if (focused is { IsEditingTitle: true })
        {
            foreach (var uiEvent in focused.TitleKeys(input.KeysPressed))
                Dispatch(focused, uiEvent, happened);
        }

        // The view gets the frame when it holds the pointer, or for keys and the wheel.
        var receiver = captured ?? (input.Scroll != 0 ? WindowAt(x, y) : null) ?? Focused;
        if (receiver is not null && windows.Contains(receiver) && !receiver.IsMinimized)
        {
            var keys = receiver == Focused && !receiver.IsEditingTitle ? input.KeysPressed : [];
            var scroll = WindowAt(x, y) == receiver ? input.Scroll : 0;
            var viewPressed = pressed && captured == receiver;
            var (originX, originY) = receiver.ContentOrigin;
            UIViewInput viewInput = new(x, y, captured == receiver && input.PointerDown, viewPressed, released && captured == receiver, scroll, keys, input.Time);

            var viewEvents = receiver.View.Update(viewInput, originX, originY);
            for (var index = 0; index < viewEvents.Count; index++)
                Dispatch(receiver, viewEvents[index], happened);
        }

        if (released)
        {
            captured = null;
            drag = null;
            resizing = null;
        }

        happened.AddRange(CloseRequested());
        return happened.Count == 0 ? [] : [.. happened];
    }

    /// <summary>Closes the windows that asked to close.</summary>
    /// <returns>A closing event for each.</returns>
    public IReadOnlyList<WindowEvent> CloseRequested()
    {
        var any = false;
        for (var index = 0; index < windows.Count; index++)
            any |= windows[index].IsCloseRequested;
        if (!any)
            return [];

        List<WindowEvent> closed = [];
        for (var index = windows.Count - 1; index >= 0; index--)
        {
            var window = windows[index];
            if (window.IsCloseRequested && Close(window))
                closed.Add(new WindowEvent(window, null, Closed: true));
        }

        return closed;
    }

    /// <summary>How long the pointer rests on a control before its hover text shows.</summary>
    public static readonly TimeSpan TooltipDelay = TimeSpan.FromSeconds(0.5);

    /// <summary>The hover text showing by the pointer, or <see langword="null"/>.</summary>
    public string? Tooltip { get; private set; }

    /// <summary>
    /// Draws the hover text in a small box below and right of the pointer, kept on the screen, over everything but
    /// the pointer, so the desktop calls it after the menu bar and dock.
    /// </summary>
    public void DrawTooltip(IPainter painter)
    {
        ArgumentNullException.ThrowIfNull(painter);
        if (Tooltip is not { } text)
            return;

        const int Size = DesktopTheme.TextSize;
        const int Padding = 6;
        var lineHeight = (int)MathF.Ceiling(Size * 1.35f);
        var (width, height) = (painter.MeasureText(text, Size) + (2 * Padding), lineHeight + Padding);
        var (x, y) = (pointerAt.X + 12, pointerAt.Y + 20);
        if (x + width > screenWidth)
            x = Math.Max(0, screenWidth - width);
        if (y + height > screenHeight)
            y = Math.Max(0, pointerAt.Y - height - 4);

        painter.FillRectangle(x, y, width, height, DesktopTheme.MenuBackground);
        DesktopTheme.Outline(painter, x, y, width, height, DesktopTheme.Border);
        painter.DrawText(text, x + Padding, y + (Padding / 2), DesktopTheme.Text, Size);
    }

    /// <summary>
    /// Whether the window under the pointer shows what it rests on. The desktop turns it off while the pointer is over
    /// the menu bar or the dock, which sit above the windows.
    /// </summary>
    public bool ShowsHover { get; set; } = true;

    /// <summary>
    /// Draws the windows from back to front; only the front one draws as active. The window under the pointer shows
    /// what it rests on, unless a button is held, when the press shows instead.
    /// </summary>
    public void Draw(IPainter painter)
    {
        var focused = Focused;
        var under = ShowsHover && !wasDown ? WindowAt(pointerAt.X, pointerAt.Y) : null;
        foreach (var window in windows)
        {
            if (window.IsMinimized)
                continue;

            var held = armed is { Part: FramePart.ToolbarItem } item && item.Window == window ? item.Index : -1;
            window.Draw(painter, window == focused, held, window == under ? pointerAt : null);
        }
    }

    /// <summary>The frontmost window under a point, or <see langword="null"/>.</summary>
    public DesktopWindow? WindowAt(int x, int y)
    {
        for (var index = windows.Count - 1; index >= 0; index--)
        {
            if (!windows[index].IsMinimized && windows[index].Bounds.Contains(x, y))
                return windows[index];
        }

        return null;
    }

    private void Press(int x, int y)
    {
        var target = WindowAt(x, y);

        // A press anywhere but a title field drops the edit in progress, as Esc does, except on the same window's
        // toolbar, whose items act on what was typed.
        foreach (var window in windows)
        {
            if (window.IsEditingTitle
                && !(window == target && target.Hit(x, y).Part is FramePart.TitleField or FramePart.ToolbarItem))
                window.CancelTitleEdit();
        }

        if (target is null)
            return;

        BringToFront(target);
        var (part, index) = target.Hit(x, y);
        switch (part)
        {
            case FramePart.Close or FramePart.Minimize:
                armed = (target, part, index);
                break;
            case FramePart.ToolbarItem when !target.View.IsDisabled(target.View.Script.ToolbarItems[index].Name):
                armed = (target, part, index);
                break;
            case FramePart.ToolbarItem:
                break;
            case FramePart.TitleField:
                target.BeginTitleEdit();
                break;
            case FramePart.Bar:
                drag = (target, x - target.X, y - target.Y);
                break;
            case FramePart.Grip:
                resizing = (target, x, y, target.Width, target.Height);
                break;
            default:
                captured = target;
                break;
        }
    }

    private void Act(DesktopWindow window, FramePart part, int index, List<WindowEvent> happened)
    {
        switch (part)
        {
            case FramePart.Close:
                Close(window);
                happened.Add(new WindowEvent(window, null, Closed: true));
                break;
            case FramePart.Minimize:
                Minimize(window);
                break;
            case FramePart.ToolbarItem:
                window.EndTitleEdit();
                Dispatch(window, new UIEvent(UIEventKind.Pressed, window.View.Script.ToolbarItems[index].Name), happened);
                break;
        }
    }

    private static void Dispatch(DesktopWindow window, UIEvent uiEvent, List<WindowEvent> happened)
    {
        happened.Add(new WindowEvent(window, uiEvent));
        window.EventHandler?.Invoke(window, uiEvent);
    }

    private void BringToFront(DesktopWindow window)
    {
        if (Focused == window)
            return;

        Unfocus(Focused);
        windows.Remove(window);
        windows.Add(window);
        window.View.RestoreFocus();
    }

    /// <summary>Takes the keyboard from a window that's leaving the front.</summary>
    private static void Unfocus(DesktopWindow? window)
    {
        window?.View.ClearFocus();
        window?.CancelTitleEdit();
    }

    /// <summary>Lets go of anything a window that's closing or minimizing held.</summary>
    private void Release(DesktopWindow window)
    {
        if (captured == window)
            captured = null;
        if (armed?.Window == window)
            armed = null;
        if (drag?.Window == window)
            drag = null;
        if (resizing?.Window == window)
            resizing = null;
    }
}
