using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>Who a menu in the bar belongs to, which decides who handles its items.</summary>
public enum MenuGroup
{
    /// <summary>The VentureOS menu, which the desktop handles.</summary>
    System,

    /// <summary>The active app's menu, such as Quit, which the desktop handles for the app.</summary>
    App,

    /// <summary>A menu from the focused window's <c>.uix</c> document, which the window's owner handles.</summary>
    Window,

    /// <summary>
    /// One of the desktop's standard menus, such as File: the desktop's own items, which it handles, after the
    /// focused window's items of the same role, which the window's owner handles.
    /// </summary>
    Standard,

    /// <summary>
    /// An app's status item, on the bar's right, left of the bell: its menus drop down from it, their items going to
    /// the item's owner, or, with <see cref="BarMenu.Pressed"/>, a press acts at once.
    /// </summary>
    Status,
}

/// <summary>A menu in the bar: the menu, who it belongs to, and the view whose state its items follow.</summary>
/// <param name="Menu">The menu and its items.</param>
/// <param name="Group">Who handles its items.</param>
/// <param name="View">
/// The view that disables and checks its items by name, as for controls; <see langword="null"/> for the desktop's own
/// menus, whose items are always on.
/// </param>
/// <param name="Emphasized">Whether its title draws in heavier text, as the app's menu does.</param>
/// <param name="Own">
/// In a standard menu, the desktop's own items, told apart by reference so an app's item of the same name is never
/// mistaken for one; whether they're on is <paramref name="OwnEnabled"/>'s to say, whatever the view says.
/// </param>
/// <param name="Pressed">
/// For a status item that's a button or opens a panel, what a press on it does, in place of dropping a menu down.
/// </param>
/// <param name="OwnEnabled">
/// Whether one of the desktop's own items can act now, such as Copy with text selected, asked each time the menu is
/// drawn or chosen; without one, they're always on.
/// </param>
public sealed record BarMenu(
    UIMenu Menu, MenuGroup Group, UIView? View = null, bool Emphasized = false, IReadOnlyList<UIMenuEntry>? Own = null,
    Func<UIMenuEntry, bool>? OwnEnabled = null, DesktopImage? Icon = null, Action? Pressed = null)
{
    /// <summary>
    /// Whether an entry can be chosen, or, for a submenu's entry, opened: one that isn't disabled. A disabled
    /// submenu's items can't be reached at all, by the pointer, the keys, or their shortcuts.
    /// </summary>
    public bool IsEnabled(UIMenuEntry entry) =>
        entry.Name is { } name && (IsOwn(entry) ? OwnEnabled?.Invoke(entry) ?? true : !(View?.IsDisabled(name) ?? false));

    /// <summary>Whether an item has a check mark.</summary>
    public bool IsChecked(UIMenuEntry entry) => entry.Name is { } name && !IsOwn(entry) && (View?.IsSelected(name) ?? false);

    /// <summary>Whether an entry is one of the desktop's own items in a standard menu.</summary>
    public bool IsOwn(UIMenuEntry entry)
    {
        if (Own is null)
            return false;

        for (var index = 0; index < Own.Count; index++)
        {
            if (ReferenceEquals(Own[index], entry))
                return true;
        }

        return false;
    }
}

/// <summary>A menu item someone chose, and the menu it's in.</summary>
public readonly record struct MenuChoice(BarMenu Menu, UIMenuEntry Item);

/// <summary>
/// The global menu bar along the top of the screen, the menu that drops down from it, and the submenus open beside it.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>Clicking a title opens its menu, and clicking it again closes it. While one is open, moving
/// over another title opens that one instead. Releasing over an item chooses it, so a press, drag, and release
/// works as well as two clicks.</description></item>
/// <item><description>Resting on a submenu's entry opens the submenu beside it, on the right, or on the left where
/// the right has no room. Moving toward an open submenu over its parent's other entries keeps it open, so the
/// pointer can cross to it.</description></item>
/// <item><description>Alt with a menu's hotkey opens it. While a menu is open, Up and Down move through the
/// innermost menu, Right opens the highlighted submenu or else the next menu in the bar, Left closes the innermost
/// submenu or else opens the menu before, Enter chooses or opens, an entry's hotkey chooses or opens it, and Esc
/// closes the innermost menu. An open menu takes every key, so nothing reaches the window behind it.</description></item>
/// <item><description><see cref="FindShortcut"/> matches a key against every item's shortcut, such as Ctrl+N, in
/// submenus too, except those in a disabled submenu, so the host can choose items from anywhere.</description></item>
/// </list>
/// <para>Text widths come from the painter, so titles and items are placed when the bar is drawn and hit-tested
/// against what was last drawn: the pointer can only reach what's on screen.</para>
/// </remarks>
public sealed class MenuBar
{
    /// <summary>The bar's height.</summary>
    public const int Height = 22;

    private const int FirstTitleX = 10;
    private const int TitlePadding = 9;
    private const int StatusMargin = 14;
    private const int ItemHeight = 20;
    private const int SeparatorHeight = 9;
    private const int MenuPadding = 4;
    private const int CheckRoom = 20;
    private const int ShortcutGap = 24;
    private const int ArrowRoom = 18;
    private const int MinimumMenuWidth = 160;

    private IReadOnlyList<BarMenu> menus = [];
    private readonly List<(int X, int Width)> titles = [];

    /// <summary>
    /// Text on the right of the bar, such as the time, or <see langword="null"/> for none. It's a status item: it
    /// shows, and takes no clicks.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>Whether the bar shows the notification bell, left of <see cref="Status"/>.</summary>
    public bool ShowsBell { get; set; }

    /// <summary>Whether a dot beside the bell says something arrived unseen.</summary>
    public bool BellDot { get; set; }

    /// <summary>Whether the bell is on, as the history it opens is open: drawn as an open menu's title is.</summary>
    public bool BellOn { get; set; }

    // Where the bell was last drawn, for clicks.
    private UIRect bell;

    // The bell, a row at a time: each row's width, centered; a gap, then the clapper.
    private static readonly int[] BellRows = [2, 4, 6, 6, 8, 8, 8, 8, 10, 12, 0, 4];

    /// <summary>Whether a point is on the bell.</summary>
    public bool OnBell(int x, int y) => ShowsBell && bell.Contains(x, y);

    // An icon title's silhouette in the open title's text color, made when first shown and kept until the icon
    // or the color changes.
    private (DesktopImage Icon, UIColor Color, DesktopImage Silhouette)? silhouette;

    // The open menus: the bar's drop-down first, then each submenu open beside the one before. Empty when closed.
    private readonly List<Level> levels = [];
    private int open = -1;
    private bool openedByThisPress;
    private (int X, int Y)? lastPointer;

    /// <summary>One open menu: what it shows, its highlighted entry, and where it and its rows were last drawn.</summary>
    private sealed class Level(UIMenu menu)
    {
        public UIMenu Menu { get; } = menu;

        public int Highlighted { get; set; } = -1;

        public UIRect Box { get; set; }

        public List<(int Y, int Height)> Rows { get; } = [];
    }

    /// <summary>The menus in the bar, left to right.</summary>
    public IReadOnlyList<BarMenu> Menus => menus;

    /// <summary>Whether a menu is open.</summary>
    public bool IsOpen => open >= 0;

    /// <summary>The open menu, or <see langword="null"/>.</summary>
    public BarMenu? OpenMenu => open >= 0 ? menus[open] : null;

    /// <summary>
    /// The open menus, the bar's drop-down first and then each open submenu, as they're shown; empty when none is.
    /// </summary>
    public IReadOnlyList<UIMenu> OpenMenus => [.. levels.Select(level => level.Menu)];

    /// <summary>The highlighted entry in the innermost open menu, or <see langword="null"/>.</summary>
    public UIMenuEntry? Highlighted =>
        levels.Count > 0 && levels[^1].Highlighted is >= 0 and var at ? levels[^1].Menu.Entries[at] : null;

    /// <summary>Puts new menus in the bar, such as when focus moves to another app. Any open menu closes.</summary>
    public void SetMenus(IReadOnlyList<BarMenu> newMenus)
    {
        ArgumentNullException.ThrowIfNull(newMenus);
        if (SameMenus(newMenus))
            return;

        menus = newMenus;
        titles.Clear();
        Close();
    }

    /// <summary>Closes the open menu and its submenus, if there are any.</summary>
    public void Close()
    {
        open = -1;
        levels.Clear();
    }

    /// <summary>
    /// Where a menu's title was last drawn, by its position in the bar, or <see langword="null"/> before it's drawn;
    /// for a host that shows the bar, such as Roundhouse's designer, to find its menus.
    /// </summary>
    public UIRect? TitleBounds(int index) =>
        index >= 0 && index < titles.Count ? new UIRect(titles[index].X, 0, titles[index].Width, Height) : null;

    /// <summary>
    /// Where an entry of an open menu was last drawn, by its position in the menu, or <see langword="null"/> when that
    /// menu isn't open or it hasn't been drawn. <paramref name="level"/> is 0 for the bar's drop-down, 1 for a submenu
    /// open beside it, and so on.
    /// </summary>
    public UIRect? EntryBounds(int index, int level = 0)
    {
        if (level < 0 || level >= levels.Count)
            return null;

        var shown = levels[level];
        return index >= 0 && index < shown.Rows.Count
            ? new UIRect(shown.Box.X, shown.Rows[index].Y, shown.Box.Width, shown.Rows[index].Height)
            : null;
    }

    /// <summary>Whether a point is on a status item that acts when pressed, a button or a panel's, rather than a menu.</summary>
    public bool PressesAt(int x, int y) => TitleAt(x, y) is >= 0 and var index && menus[index].Pressed is not null;

    /// <summary>Whether a point is on the bar or in an open menu.</summary>
    public bool Contains(int x, int y) => y < Height || LevelAt(x, y) >= 0;

    /// <summary>Opens a menu by its position in the bar, with its first item that can be chosen highlighted.</summary>
    public void OpenAt(int index)
    {
        if (index < 0 || index >= menus.Count || menus[index].Pressed is not null)
            return;

        if (open != index || levels.Count == 0)
        {
            levels.Clear();
            levels.Add(new Level(menus[index].Menu));
        }
        else
        {
            levels.RemoveRange(1, levels.Count - 1);
        }

        open = index;
        levels[0].Highlighted = Step(levels[0], -1, 1);
    }

    /// <summary>Handles the pointer for a frame, while the bar owns it or a menu is open.</summary>
    /// <returns>The item chosen, if one was.</returns>
    public MenuChoice? UpdatePointer(int x, int y, bool pressed, bool released)
    {
        var previous = lastPointer;
        lastPointer = (x, y);
        var title = TitleAt(x, y);
        var level = LevelAt(x, y);
        var row = level >= 0 ? RowAt(levels[level], y) : -1;

        if (pressed)
        {
            openedByThisPress = false;
            if (title >= 0 && menus[title].Pressed is { } press)
            {
                Close();
                press();
                return null;
            }

            if (title >= 0)
            {
                if (title == open)
                {
                    Close();
                }
                else
                {
                    OpenAt(title);
                    levels[0].Highlighted = -1;
                    openedByThisPress = true;
                }

                return null;
            }

            if (level < 0)
            {
                // A press anywhere else just closes the menus, and goes no further.
                Close();
                return null;
            }
        }

        if (open < 0)
            return null;

        // While a menu is open, the pointer moves between menus and picks out entries; a status item that's a
        // button or a panel has no menu to move to.
        if (title >= 0 && title != open && menus[title].Pressed is null)
        {
            OpenAt(title);
            levels[0].Highlighted = -1;
            return null;
        }

        if (level >= 0)
        {
            Hover(level, row, x, y, previous);
        }
        else if (title < 0 && previous != (x, y))
        {
            // Moving off every menu, the innermost lets go of its highlight; the ones holding a submenu open keep
            // theirs. A pointer resting off the menus leaves alone what the keys highlighted, since the desktop
            // passes the pointer on every frame while a menu is open.
            levels[^1].Highlighted = -1;
        }

        if (!released)
            return null;

        if (level >= 0 && row >= 0 && levels[level].Highlighted == row)
        {
            var entry = levels[level].Menu.Entries[row];
            return entry.IsSubmenu ? null : Choose(entry);
        }

        // Releasing on the title that just opened leaves the menu open, to be clicked through.
        if (title < 0 && level < 0 && !openedByThisPress)
            Close();
        return null;
    }

    /// <summary>Handles a key while a menu is open. Every key is taken, chosen or not.</summary>
    /// <returns>The item chosen, if one was.</returns>
    public MenuChoice? UpdateKey(KeyPress key)
    {
        if (open < 0)
            return null;

        var innermost = levels[^1];
        switch (key.Key)
        {
            case DesktopKey.Escape:
                if (levels.Count > 1)
                    levels.RemoveAt(levels.Count - 1);
                else
                    Close();
                return null;
            case DesktopKey.Left:
                if (levels.Count > 1)
                    levels.RemoveAt(levels.Count - 1);
                else
                    OpenAt((open + menus.Count - 1) % menus.Count);
                return null;
            case DesktopKey.Right:
                if (innermost.Highlighted >= 0 && innermost.Menu.Entries[innermost.Highlighted].IsSubmenu)
                    OpenSubmenu(levels.Count - 1, innermost.Highlighted, highlightFirst: true);
                else
                    OpenAt((open + 1) % menus.Count);
                return null;
            case DesktopKey.Up:
                innermost.Highlighted = Step(innermost, innermost.Highlighted, -1);
                return null;
            case DesktopKey.Down:
                innermost.Highlighted = Step(innermost, innermost.Highlighted, 1);
                return null;
            case DesktopKey.Enter:
                return innermost.Highlighted >= 0 ? Activate(levels.Count - 1, innermost.Highlighted) : null;
            case DesktopKey.Character when key.Alt && !key.Control:
                OpenByHotkey(key.Character);
                return null;
            case DesktopKey.Character when !key.Control:
                var entries = innermost.Menu.Entries;
                for (var index = 0; index < entries.Count; index++)
                {
                    if (entries[index].Hotkey is { } hotkey && SameLetter(hotkey, key.Character) && menus[open].IsEnabled(entries[index]))
                        return Activate(levels.Count - 1, index);
                }

                return null;
            default:
                return null;
        }
    }

    /// <summary>Opens the menu whose hotkey this is, as Alt and the hotkey do.</summary>
    /// <returns>Whether a menu has that hotkey.</returns>
    public bool OpenByHotkey(char character)
    {
        for (var index = 0; index < menus.Count; index++)
        {
            if (menus[index].Menu.Hotkey is { } hotkey && SameLetter(hotkey, character))
            {
                OpenAt(index);
                return true;
            }
        }

        return false;
    }

    /// <summary>The item whose shortcut this key is, if it can be chosen, however deep its submenu.</summary>
    public MenuChoice? FindShortcut(KeyPress key)
    {
        if (key.Key != DesktopKey.Character || !(key.Control || key.Alt))
            return null;

        foreach (var menu in menus)
        {
            if (Find(menu, menu.Menu, key) is { } entry)
                return new MenuChoice(menu, entry);
        }

        return null;

        static UIMenuEntry? Find(BarMenu menu, UIMenu shown, KeyPress key)
        {
            foreach (var entry in shown.Entries)
            {
                // A disabled submenu keeps everything in it from being chosen, shortcuts included.
                if (entry.Submenu is { } submenu)
                {
                    if (menu.IsEnabled(entry) && Find(menu, submenu, key) is { } inner)
                        return inner;
                }
                else if (entry.Shortcut is { } shortcut && shortcut.Control == key.Control && shortcut.Alt == key.Alt
                    && shortcut.Shift == key.Shift && SameLetter(shortcut.Key, key.Character) && menu.IsEnabled(entry))
                {
                    return entry;
                }
            }

            return null;
        }
    }

    /// <summary>Draws the bar across the top of the screen, then the open menu below its title and its submenus.</summary>
    public void Draw(IPainter painter)
    {
        ArgumentNullException.ThrowIfNull(painter);
        const int Size = DesktopTheme.TextSize;

        // Tiger's glass: the gradient, a bright band over its top half, and a sheen along the top edge.
        DesktopTheme.FillGradient(painter, 0, 0, painter.Width, Height, DesktopTheme.TitleBarTop, DesktopTheme.MenuBarBottom);
        painter.FillRectangle(0, 0, painter.Width, Height / 2, DesktopTheme.MenuBarGloss);
        painter.FillRectangle(0, 0, painter.Width, 1, DesktopTheme.MenuBarSheen);
        painter.FillRectangle(0, Height - 1, painter.Width, 1, DesktopTheme.Border);

        titles.Clear();
        var x = FirstTitleX;
        var textY = TextY(0, Height);
        for (var index = 0; index < menus.Count; index++)
        {
            var menu = menus[index];
            if (menu.Group == MenuGroup.Status)
                break;

            var width = (menu.Icon?.Width ?? painter.MeasureText(menu.Menu.Title, Size)) + (2 * TitlePadding) + (menu.Emphasized ? 1 : 0);
            titles.Add((x, width));

            var color = DesktopTheme.Text;
            if (index == open)
            {
                painter.FillRectangle(x, 0, width, Height - 1, DesktopTheme.Accent);
                color = DesktopTheme.HighlightedText;
            }

            if (menu.Icon is { } icon)
            {
                // Open, it turns the title's color, as Tiger's Apple menu turned white.
                var iconY = (Height - 1 - icon.Height) / 2;
                painter.DrawImage(index == open ? Silhouette(icon, color) : icon, x + TitlePadding, iconY);
            }
            else
            {
                DrawLabel(painter, menu.Menu.Title, menu.Menu.Hotkey, x + TitlePadding, textY, color, menu.Emphasized);
            }
            x += width;
        }

        var statusX = painter.Width - StatusMargin;
        if (Status is { Length: > 0 } status)
        {
            statusX -= painter.MeasureText(status, Size);
            painter.DrawText(status, statusX, textY, DesktopTheme.Text, Size);
        }

        var right = statusX;
        if (ShowsBell)
            right = DrawBell(painter, statusX - StatusMargin);
        DrawStatusItems(painter, x, right - StatusMargin, textY);

        for (var level = 0; level < levels.Count; level++)
            DrawMenu(painter, level);
    }

    /// <summary>
    /// Apps' status items, right to left from <paramref name="right"/>, in the order they're listed, each its symbol and
    /// title; one that would run into the menus at <paramref name="left"/> isn't shown, nor those after it.
    /// </summary>
    private void DrawStatusItems(IPainter painter, int left, int right, int textY)
    {
        const int Size = DesktopTheme.TextSize;
        const int Gap = 4;
        var first = titles.Count;
        for (var index = first; index < menus.Count; index++)
            titles.Add((0, 0));

        var x = right;
        for (var index = first; index < menus.Count; index++)
        {
            var menu = menus[index];
            var title = menu.Menu.Title;
            var titleWidth = title.Length > 0 ? painter.MeasureText(title, Size) : 0;
            var iconWidth = menu.Icon?.Width ?? 0;
            var width = iconWidth + titleWidth + (iconWidth > 0 && titleWidth > 0 ? Gap : 0) + (2 * TitlePadding);
            if (x - width < left + StatusMargin)
                break;

            x -= width;
            titles[index] = (x, width);
            var color = DesktopTheme.Text;
            if (index == open)
            {
                painter.FillRectangle(x, 0, width, Height - 1, DesktopTheme.Accent);
                color = DesktopTheme.HighlightedText;
            }

            var at = x + TitlePadding;
            if (menu.Icon is { } icon)
            {
                // A symbol, drawn in the bar's text color, as toolbar items' are.
                painter.DrawImage(Silhouette(icon, color), at, (Height - 1 - icon.Height) / 2);
                at += iconWidth + Gap;
            }

            if (titleWidth > 0)
                painter.DrawText(title, at, textY, color, Size);
        }
    }

    // The bell and its dot, ending at right; on, it's drawn as an open title is, in the accent. Gives its left edge.
    private int DrawBell(IPainter painter, int right)
    {
        const int BellWidth = 12;
        const int DotSize = 6;
        var width = BellWidth + (2 * TitlePadding) + (BellDot ? DotSize + 2 : 0);
        bell = new UIRect(right - width, 0, width, Height - 1);
        var color = DesktopTheme.Text;
        if (BellOn)
        {
            painter.FillRectangle((int)bell.X, 0, width, Height - 1, DesktopTheme.Accent);
            color = DesktopTheme.HighlightedText;
        }

        var centerX = (int)bell.X + TitlePadding + (BellWidth / 2);
        var top = (Height - 1 - BellRows.Length) / 2;
        for (var row = 0; row < BellRows.Length; row++)
            painter.FillRectangle(centerX - (BellRows[row] / 2), top + row, BellRows[row], 1, color);

        if (BellDot && !BellOn)
        {
            DesktopTheme.FillRoundedGradient(painter, centerX + (BellWidth / 2) + 2, (Height - 1 - DotSize) / 2, DotSize, DotSize,
                DotSize / 2, DesktopTheme.Accent, DesktopTheme.Accent);
        }

        return (int)bell.X;
    }

    private DesktopImage Silhouette(DesktopImage icon, UIColor color)
    {
        if (silhouette is { } kept && ReferenceEquals(kept.Icon, icon) && kept.Color == color)
            return kept.Silhouette;

        var made = icon.Silhouette(color.Red, color.Green, color.Blue);
        silhouette = (icon, color, made);
        return made;
    }

    private void DrawMenu(IPainter painter, int depth)
    {
        const int Size = DesktopTheme.TextSize;
        var menu = menus[open];
        var level = levels[depth];
        var entries = level.Menu.Entries;

        // Wide enough for the longest title and shortcut, or a submenu's arrow, kept on the screen.
        var widest = MinimumMenuWidth;
        foreach (var entry in entries)
        {
            if (entry.IsSeparator)
                continue;

            var width = CheckRoom + painter.MeasureText(entry.Title!, Size) + 12;
            if (entry.Shortcut is { } shortcut)
                width += ShortcutGap + painter.MeasureText(shortcut.ToString(), Size);
            if (entry.IsSubmenu)
                width += ArrowRoom;
            widest = Math.Max(widest, width);
        }

        var height = MenuPadding * 2;
        foreach (var entry in entries)
            height += entry.IsSeparator ? SeparatorHeight : ItemHeight;

        int left, top;
        if (depth == 0)
        {
            var (titleX, _) = titles[open];
            left = Math.Max(0, Math.Min(titleX, painter.Width - widest));
            top = Height - 1;
        }
        else
        {
            // Beside its entry, its first row level with it: on the right, or on the left where the right has no room.
            // The entry that opened it is its parent's highlighted one.
            var parent = levels[depth - 1];
            var entryRow = parent.Rows[parent.Highlighted];
            var parentLeft = (int)parent.Box.X;
            left = parentLeft + (int)parent.Box.Width - 2;
            if (left + widest > painter.Width)
                left = Math.Max(0, parentLeft - widest + 2);
            top = Math.Max(Height - 1, Math.Min(entryRow.Y - MenuPadding, painter.Height - height));
        }

        level.Box = new UIRect(left, top, widest, height);

        painter.FillRectangle(left + DesktopTheme.ShadowSize, top + DesktopTheme.ShadowSize, widest, height, DesktopTheme.Shadow);
        painter.FillRectangle(left, top, widest, height, DesktopTheme.MenuBackground);
        DesktopTheme.Outline(painter, left, top, widest, height, DesktopTheme.Border);

        level.Rows.Clear();
        var y = top + MenuPadding;
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (entry.IsSeparator)
            {
                level.Rows.Add((y, SeparatorHeight));
                painter.FillRectangle(left + 1, y + (SeparatorHeight / 2), widest - 2, 1, DesktopTheme.Separator);
                y += SeparatorHeight;
                continue;
            }

            level.Rows.Add((y, ItemHeight));
            var enabled = menu.IsEnabled(entry);
            var color = enabled ? DesktopTheme.Text : DesktopTheme.DisabledText;
            var shortcutColor = enabled ? DesktopTheme.SecondaryText : DesktopTheme.DisabledText;
            if (index == level.Highlighted)
            {
                painter.FillRectangle(left + 1, y, widest - 2, ItemHeight, DesktopTheme.Accent);
                color = shortcutColor = DesktopTheme.HighlightedText;
            }

            if (menu.IsChecked(entry))
                DrawCheck(painter, left + 7, y + 5, color);

            var textY = TextY(y, ItemHeight);
            DrawLabel(painter, entry.Title!, entry.Hotkey, left + CheckRoom, textY, color, emphasized: false);
            if (entry.IsSubmenu)
                DrawArrow(painter, left + widest - 14, y + ((ItemHeight - 7) / 2), color);
            else if (entry.Shortcut is { } shortcut)
            {
                var text = shortcut.ToString();
                painter.DrawText(text, left + widest - 12 - painter.MeasureText(text, Size), textY, shortcutColor, Size);
            }

            y += ItemHeight;
        }
    }

    /// <summary>A title or item, with its hotkey underlined, or added in brackets when the title doesn't have it.</summary>
    private static void DrawLabel(IPainter painter, string text, char? hotkey, int x, int y, UIColor color, bool emphasized)
    {
        const int Size = DesktopTheme.TextSize;
        var at = hotkey is { } key ? text.IndexOf(key.ToString(), StringComparison.OrdinalIgnoreCase) : -1;
        if (hotkey is { } missing && at < 0)
            text = $"{text} ({char.ToUpperInvariant(missing)})";

        painter.DrawText(text, x, y, color, Size);
        if (emphasized)
            painter.DrawText(text, x + 1, y, color, Size);

        if (at >= 0)
        {
            var start = painter.MeasureText(text[..at], Size);
            var width = Math.Max(1, painter.MeasureText(text[at].ToString(), Size));
            // Just under the baseline, which sits about a text size below the top of the line.
            painter.FillRectangle(x + start, y + Size - 2, width, 1, color);
        }
    }

    /// <summary>A small tick, drawn as two strokes.</summary>
    private static void DrawCheck(IPainter painter, int x, int y, UIColor color)
    {
        for (var step = 0; step < 3; step++)
            painter.FillRectangle(x + step, y + 4 + step, 2, 2, color);
        for (var step = 0; step < 6; step++)
            painter.FillRectangle(x + 3 + step, y + 6 - step, 2, 2, color);
    }

    /// <summary>A submenu's arrow: a small triangle pointing right, 4 pixels wide and 7 tall.</summary>
    private static void DrawArrow(IPainter painter, int x, int y, UIColor color)
    {
        for (var column = 0; column < 4; column++)
            painter.FillRectangle(x + column, y + column, 1, 7 - (2 * column), color);
    }

    private static int TextY(int top, int height) => top + ((height - (int)MathF.Ceiling(DesktopTheme.TextSize * 1.35f)) / 2);

    /// <summary>
    /// The pointer over a row of an open menu: it highlights the row and opens or keeps a submenu as it should,
    /// closing any deeper menus it leaves behind.
    /// </summary>
    private void Hover(int level, int row, int x, int y, (int X, int Y)? previous)
    {
        var shown = levels[level];

        // Crossing a parent's other rows on the way into its open submenu keeps the submenu open.
        if (level < levels.Count - 1 && row != shown.Highlighted && HeadingInto(levels[level + 1].Box, x, y, previous))
            return;

        if (row < 0)
        {
            // A separator, or the menu's padding: deeper menus stay, with the entry that opened them.
            if (level == levels.Count - 1)
                shown.Highlighted = -1;
            return;
        }

        var entry = shown.Menu.Entries[row];
        var enabled = menus[open].IsEnabled(entry);
        if (shown.Highlighted == row && level < levels.Count - 1)
            return; // Its submenu is already open.

        levels.RemoveRange(level + 1, levels.Count - level - 1);
        shown.Highlighted = enabled ? row : -1;
        if (enabled && entry.IsSubmenu)
            OpenSubmenu(level, row, highlightFirst: false);
    }

    /// <summary>
    /// Whether the pointer moved this frame from <paramref name="previous"/> toward a submenu: across, toward its
    /// side, more than it moved up or down, and not past its top or bottom.
    /// </summary>
    private static bool HeadingInto(UIRect submenu, int x, int y, (int X, int Y)? previous)
    {
        if (previous is not { } from || submenu.Width == 0)
            return false;

        var across = x - from.X;
        var toward = submenu.X >= from.X ? across > 0 : across < 0;
        return toward && Math.Abs(y - from.Y) <= 2 * Math.Abs(across)
            && y >= submenu.Y - ItemHeight && y < submenu.Y + submenu.Height;
    }

    /// <summary>Opens the submenu of the entry at <paramref name="row"/> in the menu at <paramref name="level"/>.</summary>
    private void OpenSubmenu(int level, int row, bool highlightFirst)
    {
        var entry = levels[level].Menu.Entries[row];
        if (entry.Submenu is not { } submenu || !menus[open].IsEnabled(entry))
            return;

        levels.RemoveRange(level + 1, levels.Count - level - 1);
        levels[level].Highlighted = row;
        Level opened = new(submenu);
        levels.Add(opened);
        if (highlightFirst)
            opened.Highlighted = Step(opened, -1, 1);
    }

    /// <summary>Chooses an item, or opens a submenu with its first entry highlighted, as Enter or a hotkey does.</summary>
    private MenuChoice? Activate(int level, int row)
    {
        var entry = levels[level].Menu.Entries[row];
        if (entry.IsSubmenu)
        {
            OpenSubmenu(level, row, highlightFirst: true);
            return null;
        }

        return Choose(entry);
    }

    private MenuChoice Choose(UIMenuEntry entry)
    {
        var choice = new MenuChoice(menus[open], entry);
        Close();
        return choice;
    }

    /// <summary>The next entry in a menu that can be chosen or opened, from <paramref name="from"/> in a direction, wrapping round.</summary>
    private int Step(Level level, int from, int direction)
    {
        if (open < 0)
            return -1;

        var entries = level.Menu.Entries;
        for (var tried = 1; tried <= entries.Count; tried++)
        {
            var index = (((from + (direction * tried)) % entries.Count) + entries.Count) % entries.Count;
            if (menus[open].IsEnabled(entries[index]))
                return index;
        }

        return -1;
    }

    private int TitleAt(int x, int y)
    {
        if (y < 0 || y >= Height)
            return -1;

        for (var index = 0; index < titles.Count && index < menus.Count; index++)
        {
            if (x >= titles[index].X && x < titles[index].X + titles[index].Width)
                return index;
        }

        return -1;
    }

    /// <summary>The innermost open menu under a point, since a submenu may cover its parent, or -1.</summary>
    private int LevelAt(int x, int y)
    {
        for (var level = levels.Count - 1; level >= 0; level--)
        {
            if (levels[level].Box.Contains(x, y))
                return level;
        }

        return -1;
    }

    private static int RowAt(Level level, int y)
    {
        for (var index = 0; index < level.Rows.Count; index++)
        {
            if (y >= level.Rows[index].Y && y < level.Rows[index].Y + level.Rows[index].Height)
                return level.Menu.Entries[index].IsSeparator ? -1 : index;
        }

        return -1;
    }

    private bool SameMenus(IReadOnlyList<BarMenu> other)
    {
        if (other.Count != menus.Count)
            return false;

        for (var index = 0; index < other.Count; index++)
        {
            if (other[index] != menus[index])
                return false;
        }

        return true;
    }

    private static bool SameLetter(char left, char right) => char.ToUpperInvariant(left) == char.ToUpperInvariant(right);
}
