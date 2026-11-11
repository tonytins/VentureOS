using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>
/// A window on the desktop: a Tiger-style frame around a <see cref="UIView"/>.
/// </summary>
/// <remarks>
/// The frame is a soft shadow, a pale gradient title bar with the title in the middle and a red close button on
/// the left, and the view's content below it. A window that isn't in front draws faded, as early Aqua did.
/// </remarks>
public sealed class DesktopWindow
{
    /// <summary>The close button's size, and its distance from the frame's left and top.</summary>
    public const int CloseButtonSize = 12;

    private const int CloseButtonInset = 7;

    /// <param name="title">The title in the title bar.</param>
    /// <param name="view">The content, sized from its document's window.</param>
    /// <param name="closable">Whether the title bar has a close button.</param>
    public DesktopWindow(string title, UIView view, bool closable = true)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(view);
        Title = title;
        View = view;
        IsClosable = closable;
    }

    public string Title { get; set; }

    public UIView View { get; }

    public bool IsClosable { get; }

    /// <summary>
    /// The name of the app the window belongs to, or <see langword="null"/> for the desktop's own windows. The menu
    /// bar shows that app's menu while the window is in front.
    /// </summary>
    public string? App { get; init; }

    /// <summary>Whether the window asked to close; the window manager closes it at the end of the frame.</summary>
    public bool IsCloseRequested { get; private set; }

    /// <summary>
    /// Asks for the window to close, such as from its OK button's handler, which can't close it directly while the
    /// window manager is going through its events.
    /// </summary>
    public void RequestClose() => IsCloseRequested = true;

    /// <summary>Forgets a request to close, when the window opens again.</summary>
    internal void ClearCloseRequest() => IsCloseRequested = false;

    /// <summary>The frame's left edge, on the screen.</summary>
    public int X { get; set; }

    /// <summary>The frame's top edge, the top of the title bar.</summary>
    public int Y { get; set; }

    /// <summary>The frame's width, which is the content's.</summary>
    public int Width => (int)MathF.Ceiling(View.WindowSize.Width);

    /// <summary>The frame's height: the title bar and the content.</summary>
    public int Height => DesktopTheme.TitleBarHeight + (int)MathF.Ceiling(View.WindowSize.Height);

    /// <summary>Called with each of the view's events; set by whoever opened the window.</summary>
    public Action<DesktopWindow, UIEvent>? EventHandler { get; set; }

    /// <summary>The whole frame.</summary>
    public UIRect Bounds => new(X, Y, Width, Height);

    /// <summary>The title bar, which drags the window.</summary>
    public UIRect TitleBar => new(X, Y, Width, DesktopTheme.TitleBarHeight);

    /// <summary>The close button.</summary>
    public UIRect CloseButton => new(X + CloseButtonInset, Y + ((DesktopTheme.TitleBarHeight - CloseButtonSize) / 2), CloseButtonSize, CloseButtonSize);

    /// <summary>Where the view's corner is.</summary>
    public (int X, int Y) ContentOrigin => (X, Y + DesktopTheme.TitleBarHeight);

    /// <summary>Draws the frame and the content. A window that isn't <paramref name="active"/> draws faded.</summary>
    public void Draw(IPainter painter, bool active)
    {
        ArgumentNullException.ThrowIfNull(painter);
        const int Shadow = DesktopTheme.ShadowSize;

        painter.FillRectangle(X + Shadow, Y + Shadow, Width, Height, DesktopTheme.Shadow);

        DesktopTheme.FillGradient(painter, X, Y, Width, DesktopTheme.TitleBarHeight, DesktopTheme.TitleBarTop, DesktopTheme.TitleBarBottom);
        painter.FillRectangle(X, Y + DesktopTheme.TitleBarHeight - 1, Width, 1, DesktopTheme.Border);

        if (IsClosable)
            DrawCloseButton(painter);

        var room = Width - (2 * (CloseButtonInset + CloseButtonSize + 6));
        var title = Title;
        while (title.Length > 0 && painter.MeasureText(title, DesktopTheme.TextSize) > room)
            title = title[..^1];
        painter.DrawText(title, X + ((Width - painter.MeasureText(title, DesktopTheme.TextSize)) / 2),
            Y + ((DesktopTheme.TitleBarHeight - (int)(DesktopTheme.TextSize * 1.35f)) / 2), DesktopTheme.Text, DesktopTheme.TextSize);

        var (contentX, contentY) = ContentOrigin;
        painter.FillRectangle(contentX, contentY, Width, Height - DesktopTheme.TitleBarHeight, DesktopTheme.Panel);
        View.Draw(painter, contentX, contentY);

        DesktopTheme.Outline(painter, X, Y, Width, Height, DesktopTheme.Border);

        if (!active)
            painter.FillRectangle(X, Y, Width, Height, DesktopTheme.InactiveFade);
    }

    /// <summary>A filled circle, drawn row by row, with a darker rim.</summary>
    private void DrawCloseButton(IPainter painter)
    {
        var button = CloseButton;
        var radius = CloseButtonSize / 2f;
        var centerX = button.X + radius;

        for (var row = 0; row < CloseButtonSize; row++)
        {
            var dy = row + 0.5f - radius;
            var half = MathF.Sqrt(Math.Max(0, (radius * radius) - (dy * dy)));
            var left = (int)MathF.Round(centerX - half);
            var width = (int)MathF.Round(centerX + half) - left;
            if (width <= 0)
                continue;

            painter.FillRectangle(left, (int)button.Y + row, width, 1, DesktopTheme.CloseMark);
            if (width > 2)
                painter.FillRectangle(left + 1, (int)button.Y + row, width - 2, 1, DesktopTheme.CloseButton);
        }

    }
}
