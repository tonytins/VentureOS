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

    /// <summary>The green button, on resizable windows, which zooms the window to fill the screen and back.</summary>
    Zoom,

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
    /// <summary>
    /// The title bar's height: the theme's, or taller when it holds a subtitle or toolbar items with images, as a
    /// GNOME header bar is.
    /// </summary>
    /// <remarks>It's asked many times a frame, so whether the document has toolbar images is worked out once.</remarks>
    public int BarHeight =>
        View.Subtitle is { Length: > 0 } || HasImageItems ? DesktopTheme.TallTitleBarHeight : DesktopTheme.TitleBarHeight;

    private bool HasImageItems => hasImageItems ??= AnyImageItems(View.Script.ToolbarItems);

    private bool? hasImageItems;

    private static bool AnyImageItems(IReadOnlyList<UIToolbarItem> items)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index].Image is not null)
                return true;
        }

        return false;
    }

    /// <summary>The close and minimize buttons' size.</summary>
    public const int ButtonSize = 12;

    /// <summary>The longest title a title field takes.</summary>
    public const int TitleFieldLength = 256;

    private const int ButtonInset = 9;
    private const int ButtonGap = 8;
    private const int ItemHeight = 22;
    private const int TallItemHeight = 30;
    private const int ItemRadius = 5;
    private const int SymbolSize = 16;
    private const int SubtitleSize = DesktopTheme.CaptionSize;
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

    // Where the items at the start end and those at the end begin, as last laid out; before the first layout, none.
    private int startItemsRight = int.MinValue / 2;
    private int endItemsLeft = int.MaxValue / 2;

    // Each toolbar item's symbol, by its name, with the image and color it was made from.
    private readonly Dictionary<string, (DesktopImage Source, UIColor Color, DesktopImage Symbol)> symbols = [];
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
    public int Height => BarHeight + (int)MathF.Ceiling(View.WindowSize.Height);

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
            View.SetWindowSize(new UISize(width - View.SidebarWidth, height - BarHeight));
    }

    /// <summary>The whole frame.</summary>
    public UIRect Bounds => new(X, Y, Width, Height);

    /// <summary>The title bar.</summary>
    public UIRect TitleBar => new(X, Y, Width, BarHeight);

    /// <summary>The red close button.</summary>
    public UIRect CloseButton => new(X + ButtonInset, Y + ((BarHeight - ButtonSize) / 2), ButtonSize, ButtonSize);

    /// <summary>The orange minimize button.</summary>
    public UIRect MinimizeButton =>
        new(X + ButtonInset + ButtonSize + ButtonGap, Y + ((BarHeight - ButtonSize) / 2), ButtonSize, ButtonSize);

    /// <summary>The green zoom button, which only a resizable window has.</summary>
    public UIRect ZoomButton =>
        new(X + ButtonInset + (2 * (ButtonSize + ButtonGap)), Y + ((BarHeight - ButtonSize) / 2), ButtonSize, ButtonSize);

    /// <summary>Whether it has the green zoom button: a resizable window that can be closed.</summary>
    public bool HasZoom => IsClosable && IsResizable;

    /// <summary>
    /// Whether it's zoomed to fill the screen, with the frame it had before kept to go back to. Moving or resizing it
    /// forgets that, as on macOS, so the next zoom fills the screen again from where the user left it.
    /// </summary>
    public bool IsZoomed => unzoomed is not null;

    // The frame before zooming, while zoomed.
    internal UIRect? unzoomed;

    /// <summary>Where the view's corner is.</summary>
    public (int X, int Y) ContentOrigin => (X + View.SidebarWidth, Y + BarHeight);

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
            // Idle, it's the title's line, which a subtitle moves up; edited, a field in the bar's middle.
            if (!IsEditingTitle)
                return new UIRect(X + shownLeft, TitleTextY - ((ItemHeight - TitleLineHeight) / 2), shownWidth, ItemHeight);

            var top = Y + ((BarHeight - ItemHeight) / 2);

            var (left, right) = TitleRoom();
            return new UIRect(left, top, right - left, ItemHeight);
        }
    }

    // The room the title has: over the content, after the buttons and the items at the start, and before those at
    // the end, as last laid out.
    private (int Left, int Right) TitleRoom()
    {
        var left = Math.Max(StartOfBar, startItemsRight + Margin);
        var right = Math.Min(X + Width - Margin, endItemsLeft - Margin);
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
        if (HasZoom && Grown(ZoomButton).Contains(x, y))
            return (FramePart.Zoom, -1);

        for (var index = 0; index < itemBounds.Count; index++)
        {
            if (itemBounds[index].Contains(x, y))
                return (FramePart.ToolbarItem, index);
        }

        if (View.Script.TitleField is not null && TitleFieldBounds.Contains(x, y))
            return (FramePart.TitleField, -1);
        return (FramePart.Bar, -1);
    }

    /// <summary>
    /// The hover text at a point on the screen, from the content under it, or a toolbar item's title when it shows a
    /// symbol, or <see langword="null"/>.
    /// </summary>
    public string? TooltipAt(int x, int y)
    {
        if (!Bounds.Contains(x, y))
            return null;

        var (part, index) = Hit(x, y);
        if (part == FramePart.ToolbarItem && View.Script.ToolbarItems[index] is var item && HasSymbol(item))
            return item.Title;
        if (part != FramePart.None)
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
        cursor.History.Clear();
        cursor.SelectAll(savedTitle.Length);
        IsEditingTitle = true;
    }

    /// <summary>
    /// Whether the window has text with focus that Edit can act on: the title field being edited, or a text field or
    /// area in its content.
    /// </summary>
    public bool HasEditableText => IsEditingTitle || View.FocusedControl is not null;

    /// <summary>
    /// Whether whatever has the window's focus has text selected that Cut and Copy can take, worked out without
    /// allocating, since the menu bar asks each time it draws.
    /// </summary>
    public bool HasSelectedText => IsEditingTitle ? cursor.HasSelection : View.HasFocusedSelection;

    /// <summary>The selected text in whatever has the window's focus, for Copy and Cut, or <see langword="null"/>.</summary>
    public string? SelectedText()
    {
        if (!IsEditingTitle || View.Script.TitleField is not { } field)
            return View.FocusedSelection();

        var text = View.Text(field.Name) ?? "";
        cursor.Clamp(text.Length);
        var (start, length) = cursor.Selection;
        return length > 0 ? text.Substring(start, length) : null;
    }

    /// <summary>
    /// Puts text in place of the selection in whatever has the window's focus, as Paste does, or removes it with
    /// empty text, as Cut does. The title field keeps to one line, as a text field does.
    /// </summary>
    /// <returns>What the content reports, such as a text area's changed event.</returns>
    public IReadOnlyList<UIEvent> ReplaceSelectedText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!IsEditingTitle || View.Script.TitleField is not { } field)
            return View.ReplaceFocusedSelection(text);

        var current = View.Text(field.Name) ?? "";
        cursor.Clamp(current.Length);
        List<char> kept = [];
        foreach (var character in text)
        {
            if (character is '\n' or '\r' or '\t')
                kept.Add(' ');
            else if (!char.IsControl(character))
                kept.Add(character);
        }

        var cleaned = new string([.. kept]);
        var room = Math.Max(0, TitleFieldLength - (current.Length - cursor.Selection.Length));
        View.SetText(field.Name, cursor.Replace(current, cleaned.Length > room ? cleaned[..room] : cleaned, TitleFieldLength));
        return [];
    }

    /// <summary>Whether whatever text has the window's focus has an edit to undo.</summary>
    public bool CanUndo => IsEditingTitle ? cursor.History.CanUndo : View.CanUndoFocused;

    /// <summary>Whether whatever text has the window's focus has an undone edit to redo.</summary>
    public bool CanRedo => IsEditingTitle ? cursor.History.CanRedo : View.CanRedoFocused;

    /// <summary>Takes back the newest edit to whatever text has the window's focus, as Undo does.</summary>
    /// <returns>What the content reports, such as a text area's changed event.</returns>
    public IReadOnlyList<UIEvent> Undo() => Step(undo: true);

    /// <summary>Applies again the newest undone edit to whatever text has the window's focus, as Redo does.</summary>
    /// <returns>What the content reports, such as a text area's changed event.</returns>
    public IReadOnlyList<UIEvent> Redo() => Step(undo: false);

    private IReadOnlyList<UIEvent> Step(bool undo)
    {
        if (!IsEditingTitle || View.Script.TitleField is not { } field)
            return undo ? View.UndoFocused() : View.RedoFocused();

        var text = View.Text(field.Name) ?? "";
        View.SetText(field.Name, undo ? cursor.Undo(text) : cursor.Redo(text));
        return [];
    }

    /// <summary>Selects all of whatever text has the window's focus, as Select All does.</summary>
    public void SelectAllText()
    {
        if (IsEditingTitle && View.Script.TitleField is { } field)
            cursor.SelectAll((View.Text(field.Name) ?? "").Length);
        else
            View.SelectAllFocused();
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
                    text = cursor.Replace(text, key.Character.ToString(), TitleFieldLength, TextChangeKind.Typing);
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
        var bar = BarHeight;

        painter.FillRectangle(X + Shadow, Y + Shadow, Width, Height, DesktopTheme.Shadow);

        // A sidebar runs the window's full height, with the buttons on its top, and the title bar spans the content.
        var side = View.SidebarWidth;
        if (side > 0)
        {
            painter.FillRectangle(X, Y, side, Height, DesktopTheme.SidebarBackground);
            painter.FillRectangle(X + side - 1, Y, 1, Height, DesktopTheme.Border);
        }

        DesktopTheme.FillGradient(painter, X + side, Y, Width - side, bar, DesktopTheme.TitleBarTop, DesktopTheme.TitleBarBottom);
        painter.FillRectangle(X + side, Y + bar - 1, Width - side, 1, DesktopTheme.Border);

        var (hovered, hoveredIndex) = pointer is { } at ? Hit(at.X, at.Y) : (FramePart.None, -1);
        if (IsClosable)
        {
            // Over either button, both show their marks, as macOS does.
            var marked = hovered is FramePart.Close or FramePart.Minimize or FramePart.Zoom;
            DrawDot(painter, CloseButton, DesktopTheme.CloseButton, DesktopTheme.CloseMark);
            DrawDot(painter, MinimizeButton, DesktopTheme.MinimizeButton, DesktopTheme.MinimizeMark);
            if (HasZoom)
                DrawDot(painter, ZoomButton, DesktopTheme.ZoomButton, DesktopTheme.ZoomMark);
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
        painter.FillRectangle(contentX, contentY, Width - side, Height - bar, DesktopTheme.Panel);
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
        LayOutToolbar(painter);

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var bounds = itemBounds[index];
            var (x, y, width, height) = ((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height);
            var disabled = View.IsDisabled(item.Name);
            var held = !disabled && (index == pressedItem || View.IsSelected(item.Name));

            // Flat, as GNOME's header bar buttons are, until the pointer's over one or it's held.
            if (held || (!disabled && index == hoveredItem))
            {
                var shade = held ? DesktopTheme.ButtonPressed : DesktopTheme.ButtonHover;
                DesktopTheme.FillRoundedGradient(painter, x, y, width, height, ItemRadius, shade, shade);
            }

            var ink = disabled ? DesktopTheme.DisabledText : DesktopTheme.Text;
            if (Symbol(item, ink) is { } symbol)
            {
                painter.DrawImage(symbol, x + ((width - symbol.Width) / 2), y + ((height - symbol.Height) / 2));
                continue;
            }

            painter.DrawText(item.Title, x + ((width - painter.MeasureText(item.Title, Size)) / 2),
                y + ((height - (int)MathF.Ceiling(Size * 1.35f)) / 2), ink, Size);
        }
    }

    /// <summary>
    /// Places the toolbar items: those at the start left to right after the buttons, and those at the end right to
    /// left from the bar's right end, each in document order, and the room left between them for the title.
    /// </summary>
    private void LayOutToolbar(IPainter painter)
    {
        const int Size = DesktopTheme.TextSize;
        var items = View.Script.ToolbarItems;
        var height = BarHeight == DesktopTheme.TallTitleBarHeight ? TallItemHeight : ItemHeight;
        var top = Y + ((BarHeight - height) / 2);
        itemBounds.Clear();

        var left = StartOfBar;
        for (var index = 0; index < items.Count; index++)
        {
            var width = ItemWidth(painter, items[index], height, Size);
            if (items[index].Placement == UIToolbarPlacement.Start)
            {
                itemBounds.Add(new UIRect(left, top, width, height));
                left += width + ItemGap;
            }
            else
            {
                itemBounds.Add(new UIRect(0, top, width, height));
            }
        }

        var right = X + Width - Margin;
        for (var index = items.Count - 1; index >= 0; index--)
        {
            if (items[index].Placement != UIToolbarPlacement.End)
                continue;

            var bounds = itemBounds[index];
            right -= (int)bounds.Width;
            itemBounds[index] = bounds with { X = right };
            right -= ItemGap;
        }

        // With none at one end, the title's room reaches the bar's own edge there.
        startItemsRight = left > StartOfBar ? left - ItemGap : int.MinValue / 2;
        endItemsLeft = right < X + Width - Margin ? right + ItemGap : int.MaxValue / 2;
    }

    private int ItemWidth(IPainter painter, UIToolbarItem item, int height, int size) =>
        HasSymbol(item) ? height : painter.MeasureText(item.Title, size) + (2 * ItemPadding);

    // Where the bar's own room starts: after the buttons, and past the sidebar, whose top they sit on.
    private int StartOfBar => Math.Max(
        X + (IsClosable ? ButtonInset + (Buttons * ButtonSize) + ((Buttons - 1) * ButtonGap) + 12 : Margin),
        X + View.SidebarWidth + Margin);

    private int Buttons => HasZoom ? 3 : 2;

    private bool HasSymbol(UIToolbarItem item) => item.Image is { } name && View.Image(name) is not null;

    /// <summary>
    /// A toolbar item's image as a symbol in the text's color, so it follows light and dark, made once for each
    /// color and kept; <see langword="null"/> when the item has no image, or its image wasn't supplied, so its title
    /// shows instead.
    /// </summary>
    private DesktopImage? Symbol(UIToolbarItem item, UIColor color)
    {
        if (item.Image is not { } name || View.Image(name) is not { } source)
            return null;

        if (symbols.TryGetValue(item.Name, out var kept) && ReferenceEquals(kept.Source, source) && kept.Color == color)
            return kept.Symbol;

        var symbol = DesktopIcon.Symbol(source, SymbolSize).Silhouette(color.Red, color.Green, color.Blue);
        symbols[item.Name] = (source, color, symbol);
        return symbol;
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

        var textY = TitleTextY;
        painter.DrawText(shown, start + ((width - textWidth) / 2), textY, color, Size);
        if (View.Subtitle is { Length: > 0 } subtitle)
            DrawSubtitle(painter, subtitle, left, right, textY + TitleLineHeight);
    }

    private static int TitleLineHeight => (int)MathF.Ceiling(DesktopTheme.TextSize * 1.35f);

    /// <summary>
    /// Where the plain title's line starts: centred in the bar, or, with a subtitle, centred with it, the subtitle
    /// under the title.
    /// </summary>
    private int TitleTextY
    {
        get
        {
            var subtitleHeight = View.Subtitle is { Length: > 0 } ? (int)MathF.Ceiling(SubtitleSize * 1.35f) : 0;
            return Y + ((BarHeight - TitleLineHeight - subtitleHeight) / 2);
        }
    }

    /// <summary>The subtitle: smaller and quieter than the title, centred under it, and cut to the title's room.</summary>
    private void DrawSubtitle(IPainter painter, string subtitle, int left, int right, int y)
    {
        var room = right - left;
        var shown = subtitle;
        if (painter.MeasureText(shown, SubtitleSize) > room)
        {
            while (shown.Length > 0 && painter.MeasureText(shown + "…", SubtitleSize) > room)
                shown = shown[..^1];
            shown += "…";
        }

        var width = painter.MeasureText(shown, SubtitleSize);
        var (start, _) = Centre(width, left, right);
        painter.DrawText(shown, start, y, DesktopTheme.SecondaryText, SubtitleSize);
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

        // A plus, as Tiger's zoom button showed.
        if (HasZoom)
        {
            var (zoomX, zoomY) = ((int)ZoomButton.X, (int)ZoomButton.Y);
            painter.FillRectangle(zoomX + 3, zoomY + 5, 6, 2, DesktopTheme.ZoomMark);
            painter.FillRectangle(zoomX + 5, zoomY + 3, 2, 6, DesktopTheme.ZoomMark);
        }
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
