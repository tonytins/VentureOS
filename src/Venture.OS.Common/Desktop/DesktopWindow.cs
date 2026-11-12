using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>The part of a window's frame a point is on.</summary>
public enum FramePart
{
    /// <summary>Not on the title bar: the content, or outside the window.</summary>
    None,

    /// <summary>The title bar's empty space, which drags the window.</summary>
    Bar,

    /// <summary>The red button, which closes the window.</summary>
    Close,

    /// <summary>The orange button, which sends the window to the dock.</summary>
    Minimize,

    /// <summary>One of the document's toolbar items.</summary>
    ToolbarItem,

    /// <summary>The editable title, when the document has a <c>&lt;TitleField&gt;</c>.</summary>
    TitleField,

    /// <summary>The dots in a resizable window's bottom-right corner, which resize it.</summary>
    Grip,
}

/// <summary>
/// A window on the desktop: a unified title bar over a <see cref="UIView"/>, after CityLimits' window frame.
/// </summary>
/// <remarks>
/// <para>The bar is one band: a red close button and an orange minimize button on the left, then the title, then the
/// document's toolbar items (<c>&lt;ToolbarItem&gt;</c>) along the right end. The title is centred in the window when
/// it fits there clear of the buttons and toolbar, as macOS has it, and otherwise in the room between them.</para>
/// <para>A document with a <c>&lt;TitleField&gt;</c> gets an editable title in place of the plain one, as an address
/// bar, after CityLimits' city name: it reads as a plain title until it's clicked, then becomes a field across all
/// the room. Enter submits it under the field's name, and Esc or a click elsewhere puts the old text back. A title
/// too long for its room keeps its end, where a path's last folder is. The rest of the bar drags the window. A
/// window that isn't in front draws faded, as early Aqua did.</para>
/// <para>A document with a <c>&lt;Sidebar&gt;</c> gets it down the window's left, its full height, in its own shade,
/// as Nautilus has it in GNOME 45: the buttons sit on its top, and the title bar, title, and toolbar span only the
/// content beside it. The view draws and handles its rows.</para>
/// <para>A resizable window (<c>resizable="true"</c> on <c>&lt;Window&gt;</c>) has a grip of dots in its bottom-right
/// corner, over its content; dragging it resizes the content, and controls follow their edge offsets.</para>
/// <para>Toolbar items and the idle title are laid out when the bar is drawn, since their widths come from the font,
/// so the pointer hits what was last on screen.</para>
/// </remarks>
public sealed class DesktopWindow
{
    /// <summary>The close and minimize buttons' size.</summary>
    public const int ButtonSize = 12;

    /// <summary>The longest title a title field takes.</summary>
    public const int TitleFieldLength = 256;

    private const int ButtonInset = 9;
    private const int ButtonGap = 8;
    private const int ItemHeight = 20;
    private const int ItemPadding = 10;
    private const int ItemGap = 6;
    private const int Margin = 8;

    /// <summary>The grip's size, in each direction, from the window's bottom-right corner.</summary>
    public const int GripSize = 16;

    // An idle title field is padded on each side, and never narrower than this, so an empty one can still be clicked.
    private const int TitlePadding = 5;
    private const int SmallestTitleTarget = 48;
    private const string Ellipsis = "…";

    private readonly List<UIRect> itemBounds = [];
    private string savedTitle = "";
    // The caret and selection in the title field while it's being edited.
    private readonly TextCursor cursor = new();

    // Where the idle title was last drawn, from the frame's left edge, so a drag doesn't leave it behind.
    private int shownLeft;
    private int shownWidth;

    // The title as last cut to fit, worked out again only when the text or its room changes, as most frames don't.
    private string? cutFor;
    private int cutRoom = -1;
    private bool cutKeepsEnd;
    private string cutText = "";

    /// <param name="title">The title in the title bar.</param>
    /// <param name="view">The content, sized from its document's window.</param>
    /// <param name="closable">Whether the title bar has close and minimize buttons.</param>
    public DesktopWindow(string title, UIView view, bool closable = true)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(view);
        Title = title;
        View = view;
        IsClosable = closable;
    }

    /// <summary>The plain title, and the window's name in the dock when it's minimized.</summary>
    public string Title { get; set; }

    public UIView View { get; }

    public bool IsClosable { get; }

    /// <summary>
    /// The name of the app the window belongs to, or <see langword="null"/> for the desktop's own windows. The menu
    /// bar shows that app's menu while the window is in front.
    /// </summary>
    public string? App { get; init; }

    /// <summary>
    /// Whether the app opened it through <c>venture.ui</c>, rather than the desktop opening it for the app, such as
    /// a message. An app quits when the last window it opened closes, and only its own windows outlast the desktop.
    /// </summary>
    public bool OpenedByApp { get; init; }

    /// <summary>Whether the window asked to close; the window manager closes it at the end of the frame.</summary>
    public bool IsCloseRequested { get; private set; }

    /// <summary>Whether it's in the dock rather than on the desktop.</summary>
    public bool IsMinimized { get; internal set; }

    /// <summary>Whether the title field is being edited, when the keyboard is the title's.</summary>
    public bool IsEditingTitle { get; private set; }

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
    public int Width => View.SidebarWidth + (int)MathF.Ceiling(View.WindowSize.Width);

    /// <summary>The frame's height: the title bar and the content.</summary>
    public int Height => DesktopTheme.TitleBarHeight + (int)MathF.Ceiling(View.WindowSize.Height);

    /// <summary>Called with each of the view's events, and the bar's; set by whoever opened the window.</summary>
    public Action<DesktopWindow, UIEvent>? EventHandler { get; set; }

    /// <summary>Whether the document lets the window be resized, by its grip.</summary>
    public bool IsResizable => View.Script.Window.IsResizable;

    /// <summary>The grip in the bottom-right corner, which resizes a resizable window.</summary>
    public UIRect ResizeGrip => new(X + Width - GripSize, Y + Height - GripSize, GripSize, GripSize);

    /// <summary>
    /// Resizes a resizable window's frame, keeping its top-left corner. The sidebar keeps its width and the content
    /// takes the rest; the content never gets smaller than its document's minimum. A fixed window ignores this.
    /// </summary>
    public void Resize(int width, int height)
    {
        if (IsResizable)
            View.SetWindowSize(new UISize(width - View.SidebarWidth, height - DesktopTheme.TitleBarHeight));
    }

    /// <summary>The whole frame.</summary>
    public UIRect Bounds => new(X, Y, Width, Height);

    /// <summary>The title bar.</summary>
    public UIRect TitleBar => new(X, Y, Width, DesktopTheme.TitleBarHeight);

    /// <summary>The red close button.</summary>
    public UIRect CloseButton => new(X + ButtonInset, Y + ((DesktopTheme.TitleBarHeight - ButtonSize) / 2), ButtonSize, ButtonSize);

    /// <summary>The orange minimize button.</summary>
    public UIRect MinimizeButton =>
        new(X + ButtonInset + ButtonSize + ButtonGap, Y + ((DesktopTheme.TitleBarHeight - ButtonSize) / 2), ButtonSize, ButtonSize);

    /// <summary>Where the view's corner is.</summary>
    public (int X, int Y) ContentOrigin => (X + View.SidebarWidth, Y + DesktopTheme.TitleBarHeight);

    /// <summary>Where a toolbar item was last drawn, by its position in the document.</summary>
    public UIRect ToolbarItemBounds(int index) => index >= 0 && index < itemBounds.Count ? itemBounds[index] : default;

    /// <summary>
    /// Where the title field takes clicks: all the room between the buttons and the toolbar while it's being edited,
    /// and otherwise only the title as last drawn, so the bar around it still drags the window.
    /// </summary>
    public UIRect TitleFieldBounds
    {
        get
        {
            var top = Y + ((DesktopTheme.TitleBarHeight - ItemHeight) / 2);
            if (!IsEditingTitle)
                return new UIRect(X + shownLeft, top, shownWidth, ItemHeight);

            var (left, right) = TitleRoom();
            return new UIRect(left, top, right - left, ItemHeight);
        }
    }

    // The room the title has: over the content, after the buttons, and before the toolbar.
    private (int Left, int Right) TitleRoom()
    {
        var left = Math.Max(X + (IsClosable ? ButtonInset + (2 * ButtonSize) + ButtonGap + 12 : Margin),
            X + View.SidebarWidth + Margin);
        var right = itemBounds.Count > 0 ? (int)itemBounds[0].X - Margin : X + Width - Margin;
        return (left, Math.Max(left, right));
    }

    /// <summary>The part of the frame a point is on, and for a toolbar item, which one.</summary>
    public (FramePart Part, int Index) Hit(int x, int y)
    {
        if (IsResizable && ResizeGrip.Contains(x, y))
            return (FramePart.Grip, -1);
        if (!TitleBar.Contains(x, y))
            return (FramePart.None, -1);
        if (IsClosable && Grown(CloseButton).Contains(x, y))
            return (FramePart.Close, -1);
        if (IsClosable && Grown(MinimizeButton).Contains(x, y))
            return (FramePart.Minimize, -1);

        for (var index = 0; index < itemBounds.Count; index++)
        {
            if (itemBounds[index].Contains(x, y))
                return (FramePart.ToolbarItem, index);
        }

        if (View.Script.TitleField is not null && TitleFieldBounds.Contains(x, y))
            return (FramePart.TitleField, -1);
        return (FramePart.Bar, -1);
    }

    /// <summary>The hover text at a point on the screen, from the content under it, or <see langword="null"/>.</summary>
    public string? TooltipAt(int x, int y)
    {
        if (!Bounds.Contains(x, y) || Hit(x, y).Part != FramePart.None)
            return null;

        var (left, top) = ContentOrigin;
        return View.TooltipAt(x - left, y - top);
    }

    /// <summary>
    /// Starts editing the title field, if the document has one, with all of it selected, as an address bar does, so
    /// typing replaces it and an arrow key edits it in place.
    /// </summary>
    public void BeginTitleEdit()
    {
        if (View.Script.TitleField is not { } field || IsEditingTitle || View.IsDisabled(field.Name))
            return;

        View.ClearFocus();
        savedTitle = View.Text(field.Name) ?? "";
        cursor.SelectAll(savedTitle.Length);
        IsEditingTitle = true;
    }

    /// <summary>The title field's selection while it's being edited, or <see langword="null"/> when it isn't.</summary>
    public (int Start, int Length)? TitleSelection => IsEditingTitle ? cursor.Selection : null;

    /// <summary>Drops an edit of the title, putting back what it said before, as Esc or a click elsewhere does.</summary>
    public void CancelTitleEdit()
    {
        if (!IsEditingTitle || View.Script.TitleField is not { } field)
            return;

        View.SetText(field.Name, savedTitle);
        IsEditingTitle = false;
    }

    /// <summary>Stops editing the title, keeping what was typed, as a toolbar item does before it acts on it.</summary>
    public void EndTitleEdit() => IsEditingTitle = false;

    /// <summary>Takes this frame's keys while the title is being edited.</summary>
    /// <returns>A submitted event when Enter applies the edit.</returns>
    public IReadOnlyList<UIEvent> TitleKeys(IReadOnlyList<KeyPress> keys)
    {
        List<UIEvent> events = [];
        if (!IsEditingTitle || View.Script.TitleField is not { } field)
            return events;

        var text = View.Text(field.Name) ?? "";
        cursor.Clamp(text.Length);
        foreach (var key in keys)
        {
            switch (key.Key)
            {
                case DesktopKey.Character when key.Control && !key.Alt && key.Character is 'a' or 'A':
                    cursor.SelectAll(text.Length);
                    break;
                case DesktopKey.Character when !key.Control && !key.Alt && key.Character >= ' ':
                    text = cursor.Replace(text, key.Character.ToString(), TitleFieldLength);
                    break;
                case DesktopKey.Backspace:
                    text = cursor.DeleteBackward(text);
                    break;
                case DesktopKey.Delete:
                    text = cursor.DeleteForward(text);
                    break;
                case DesktopKey.Left:
                    cursor.Left(key.Shift);
                    break;
                case DesktopKey.Right:
                    cursor.Right(text.Length, key.Shift);
                    break;
                case DesktopKey.Home:
                    cursor.MoveTo(0, key.Shift);
                    break;
                case DesktopKey.End:
                    cursor.MoveTo(text.Length, key.Shift);
                    break;
                case DesktopKey.Enter:
                    View.SetText(field.Name, text);
                    IsEditingTitle = false;
                    events.Add(new UIEvent(UIEventKind.Submitted, field.Name, text));
                    return events;
                case DesktopKey.Escape:
                    CancelTitleEdit();
                    return events;
            }
        }

        View.SetText(field.Name, text);
        return events;
    }

    /// <summary>Draws the frame and the content. A window that isn't <paramref name="active"/> draws faded.</summary>
    /// <param name="pressedItem">The toolbar item being pressed, drawn held down, or -1.</param>
    /// <param name="pointer">
    /// Where the pointer is on the screen when it's over this window and no button is held, so what it rests on
    /// shows it; otherwise <see langword="null"/>.
    /// </param>
    public void Draw(IPainter painter, bool active, int pressedItem = -1, (int X, int Y)? pointer = null)
    {
        ArgumentNullException.ThrowIfNull(painter);
        const int Shadow = DesktopTheme.ShadowSize;
        const int Bar = DesktopTheme.TitleBarHeight;

        painter.FillRectangle(X + Shadow, Y + Shadow, Width, Height, DesktopTheme.Shadow);

        // A sidebar runs the window's full height, with the buttons on its top, and the title bar spans the content.
        var side = View.SidebarWidth;
        if (side > 0)
        {
            painter.FillRectangle(X, Y, side, Height, DesktopTheme.SidebarBackground);
            painter.FillRectangle(X + side - 1, Y, 1, Height, DesktopTheme.Border);
        }

        DesktopTheme.FillGradient(painter, X + side, Y, Width - side, Bar, DesktopTheme.TitleBarTop, DesktopTheme.TitleBarBottom);
        painter.FillRectangle(X + side, Y + Bar - 1, Width - side, 1, DesktopTheme.Border);

        var (hovered, hoveredIndex) = pointer is { } at ? Hit(at.X, at.Y) : (FramePart.None, -1);
        if (IsClosable)
        {
            // Over either button, both show their marks, as macOS does.
            var marked = hovered is FramePart.Close or FramePart.Minimize;
            DrawDot(painter, CloseButton, DesktopTheme.CloseButton, DesktopTheme.CloseMark);
            DrawDot(painter, MinimizeButton, DesktopTheme.MinimizeButton, DesktopTheme.MinimizeMark);
            if (marked)
                DrawMarks(painter);
        }

        DrawToolbar(painter, pressedItem, hovered == FramePart.ToolbarItem ? hoveredIndex : -1);

        if (View.Script.TitleField is { } field && IsEditingTitle)
            DrawTitleEdit(painter, field, active);
        else
            DrawTitle(painter, View.Script.TitleField, hovered == FramePart.TitleField);

        var (contentX, contentY) = ContentOrigin;
        View.SetHover(pointer is { } point && hovered == FramePart.None ? (point.X - contentX, point.Y - contentY) : null);
        painter.FillRectangle(contentX, contentY, Width - side, Height - Bar, DesktopTheme.Panel);
        View.Draw(painter, contentX, contentY);
        if (IsResizable)
            DrawGrip(painter);

        DesktopTheme.Outline(painter, X, Y, Width, Height, DesktopTheme.Border);

        if (!active)
            painter.FillRectangle(X, Y, Width, Height, DesktopTheme.InactiveFade);
    }

    private void DrawToolbar(IPainter painter, int pressedItem, int hoveredItem)
    {
        const int Size = DesktopTheme.TextSize;
        var items = View.Script.ToolbarItems;
        itemBounds.Clear();

        // Right to left, so the last item sits at the right end.
        var right = X + Width - Margin;
        var top = Y + ((DesktopTheme.TitleBarHeight - ItemHeight) / 2);
        UIRect[] placed = new UIRect[items.Count];
        for (var index = items.Count - 1; index >= 0; index--)
        {
            var width = painter.MeasureText(items[index].Title, Size) + (2 * ItemPadding);
            right -= width;
            placed[index] = new UIRect(right, top, width, ItemHeight);
            right -= ItemGap;
        }

        itemBounds.AddRange(placed);
        for (var index = 0; index < items.Count; index++)
        {
            var bounds = placed[index];
            var (x, y, width) = ((int)bounds.X, (int)bounds.Y, (int)bounds.Width);
            var disabled = View.IsDisabled(items[index].Name);
            var held = !disabled && (index == pressedItem || View.IsSelected(items[index].Name));
            var bottom = held ? DesktopTheme.ButtonPressed
                : !disabled && index == hoveredItem ? DesktopTheme.ButtonHover
                : DesktopTheme.TitleBarBottom;

            DesktopTheme.FillGradient(painter, x, y, width, ItemHeight,
                held ? DesktopTheme.ButtonPressed : DesktopTheme.InputBackground, bottom);
            DesktopTheme.Outline(painter, x, y, width, ItemHeight, DesktopTheme.Border);
            var title = items[index].Title;
            painter.DrawText(title, x + ((width - painter.MeasureText(title, Size)) / 2),
                y + ((ItemHeight - (int)MathF.Ceiling(Size * 1.35f)) / 2),
                disabled ? DesktopTheme.DisabledText : DesktopTheme.Text, Size);
        }
    }

    /// <summary>
    /// Draws the title as plain text, centred: the window's title, or the title field's text, or its placeholder
    /// dimmed when it's empty, in the accent while the pointer is over it, to show it can be clicked. A title
    /// field's text keeps its end when it's cut, and remembers where it was drawn.
    /// </summary>
    private void DrawTitle(IPainter painter, UITitleField? field, bool hovered)
    {
        const int Size = DesktopTheme.TextSize;
        var (left, right) = TitleRoom();
        var padding = field is null ? 0 : TitlePadding;
        var text = Title;
        var color = DesktopTheme.Text;
        if (field is not null)
        {
            text = View.Text(field.Name) ?? "";
            if (text.Length == 0)
                (text, color) = (field.Placeholder ?? "", DesktopTheme.SecondaryText);
        }

        if (hovered)
            color = DesktopTheme.Accent;

        var shown = Cut(painter, text, right - left - (2 * padding), keepEnd: field is not null);
        var textWidth = painter.MeasureText(shown, Size);
        var wanted = field is null ? textWidth : Math.Max(textWidth + (2 * padding), SmallestTitleTarget);
        var (start, width) = Centre(wanted, left, right);
        (shownLeft, shownWidth) = field is null ? (0, 0) : (start - X, width);

        var textY = Y + ((DesktopTheme.TitleBarHeight - (int)MathF.Ceiling(Size * 1.35f)) / 2);
        painter.DrawText(shown, start + ((width - textWidth) / 2), textY, color, Size);
    }

    /// <summary>Draws the title field being edited: a field across all the room, showing the end that holds the caret.</summary>
    private void DrawTitleEdit(IPainter painter, UITitleField field, bool active)
    {
        const int Size = DesktopTheme.TextSize;
        var bounds = TitleFieldBounds;
        var (x, y, width) = ((int)bounds.X, (int)bounds.Y, (int)bounds.Width);
        if (width <= 0)
            return;

        painter.FillRectangle(x, y, width, ItemHeight, DesktopTheme.InputBackground);
        DesktopTheme.Outline(painter, x, y, width, ItemHeight, active ? DesktopTheme.Accent : DesktopTheme.Border);

        var text = View.Text(field.Name) ?? "";
        var textY = y + ((ItemHeight - (int)MathF.Ceiling(Size * 1.35f)) / 2);
        var room = width - (2 * TitlePadding);
        var lineHeight = (int)MathF.Ceiling(Size * 1.35f);
        cursor.Clamp(text.Length);
        var end = cursor.Caret;
        var start = 0;
        while (start < end && painter.MeasureText(text[start..end], Size) > room)
            start++;
        var visible = text[start..];
        while (visible.Length > 0 && painter.MeasureText(visible, Size) > room)
            visible = visible[..^1];

        // The selection, behind the text it covers.
        var (from, length) = cursor.Selection;
        var (first, last) = (Math.Max(from, start), Math.Min(from + length, start + visible.Length));
        if (active && last > first)
        {
            painter.FillRectangle(x + TitlePadding + painter.MeasureText(text[start..first], Size), textY,
                painter.MeasureText(text[first..last], Size), lineHeight, DesktopTheme.Selection);
        }

        painter.DrawText(visible, x + TitlePadding, textY, DesktopTheme.Text, Size);

        if (active)
            painter.FillRectangle(x + TitlePadding + painter.MeasureText(text[start..end], Size) + 1, textY, 1, (int)MathF.Ceiling(Size * 1.35f), DesktopTheme.Text);
    }

    /// <summary>
    /// Where something of the wanted width goes in the title's room: centred over the content, beside any sidebar,
    /// when it fits there clear of both ends of the bar, and otherwise centred in the room, no wider than it.
    /// </summary>
    private (int Start, int Width) Centre(int wanted, int left, int right)
    {
        var (paneLeft, paneWidth) = (X + View.SidebarWidth, Width - View.SidebarWidth);
        var room = right - left;
        var width = Math.Clamp(wanted, 0, room);
        var side = Math.Max(left - paneLeft, paneLeft + paneWidth - right);
        return width <= paneWidth - (2 * side)
            ? (paneLeft + ((paneWidth - width) / 2), width)
            : (left + ((room - width) / 2), width);
    }

    /// <summary>
    /// Text cut with an ellipsis to fit the room: from the start when <paramref name="keepEnd"/> is set, as a path's
    /// last folder matters most, and from the end otherwise. Kept until the text or its room changes.
    /// </summary>
    private string Cut(IPainter painter, string text, int room, bool keepEnd)
    {
        const int Size = DesktopTheme.TextSize;
        if (ReferenceEquals(text, cutFor) && room == cutRoom && keepEnd == cutKeepsEnd)
            return cutText;

        (cutFor, cutRoom, cutKeepsEnd) = (text, room, keepEnd);
        cutText = text;
        if (painter.MeasureText(text, Size) <= room)
            return cutText;

        var length = text.Length - 1;
        while (length > 0 && painter.MeasureText(Piece(text, length, keepEnd), Size) > room)
            length--;
        cutText = length > 0 ? Piece(text, length, keepEnd) : "";
        return cutText;

        static string Piece(string text, int length, bool keepEnd) =>
            keepEnd ? Ellipsis + text[^length..] : text[..length] + Ellipsis;
    }

    /// <summary>The close button's × and the minimize button's −, drawn over their dots.</summary>
    private void DrawMarks(IPainter painter)
    {
        var (closeX, closeY) = ((int)CloseButton.X, (int)CloseButton.Y);
        for (var step = 0; step < 6; step++)
        {
            painter.FillRectangle(closeX + 3 + step, closeY + 3 + step, 1, 1, DesktopTheme.CloseMark);
            painter.FillRectangle(closeX + 3 + step, closeY + 8 - step, 1, 1, DesktopTheme.CloseMark);
        }

        painter.FillRectangle((int)MinimizeButton.X + 3, (int)MinimizeButton.Y + 5, 6, 2, DesktopTheme.MinimizeMark);
    }

    /// <summary>Six dots in a triangle against the bottom-right corner, as a resizable window's grip.</summary>
    private void DrawGrip(IPainter painter)
    {
        const int Dot = 2;
        const int Spacing = 4;
        var (left, top) = (X + Width - 14, Y + Height - 14);
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                if (row + column >= 2)
                    painter.FillRectangle(left + (column * Spacing), top + (row * Spacing), Dot, Dot, DesktopTheme.SecondaryText);
            }
        }
    }

    // The buttons are small, so a press a little outside still counts.
    private static UIRect Grown(UIRect rect) => new(rect.X - 2, rect.Y - 2, rect.Width + 4, rect.Height + 4);

    /// <summary>A filled circle, drawn row by row, with a darker rim.</summary>
    private static void DrawDot(IPainter painter, UIRect button, UIColor fill, UIColor rim)
    {
        var radius = ButtonSize / 2f;
        var centerX = button.X + radius;

        for (var row = 0; row < ButtonSize; row++)
        {
            var dy = row + 0.5f - radius;
            var half = MathF.Sqrt(Math.Max(0, (radius * radius) - (dy * dy)));
            var left = (int)MathF.Round(centerX - half);
            var width = (int)MathF.Round(centerX + half) - left;
            if (width <= 0)
                continue;

            painter.FillRectangle(left, (int)button.Y + row, width, 1, rim);
            if (width > 2)
                painter.FillRectangle(left + 1, (int)button.Y + row, width - 2, 1, fill);
        }
    }
}
