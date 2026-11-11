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
}

/// <summary>A menu in the bar: the menu, who it belongs to, and the view whose state its items follow.</summary>
/// <param name="Menu">The menu and its items.</param>
/// <param name="Group">Who handles its items.</param>
/// <param name="View">
/// The view that disables and checks its items by name, as for controls; <see langword="null"/> for the desktop's own
/// menus, whose items are always on.
/// </param>
/// <param name="Emphasized">Whether its title draws in heavier text, as the app's menu does.</param>
public sealed record BarMenu(UIMenu Menu, MenuGroup Group, UIView? View = null, bool Emphasized = false)
{
    /// <summary>Whether an entry can be chosen: an item that isn't disabled.</summary>
    public bool IsEnabled(UIMenuEntry entry) => entry.Name is { } name && !(View?.IsDisabled(name) ?? false);

    /// <summary>Whether an item has a check mark.</summary>
    public bool IsChecked(UIMenuEntry entry) => entry.Name is { } name && (View?.IsSelected(name) ?? false);
}

/// <summary>A menu item someone chose, and the menu it's in.</summary>
public readonly record struct MenuChoice(BarMenu Menu, UIMenuEntry Item);

/// <summary>
/// The global menu bar along the top of the screen, and the menu that drops down from it.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>Clicking a title opens its menu, and clicking it again closes it. While one is open, moving
/// over another title opens that one instead. Releasing over an item chooses it, so a press, drag, and release
/// works as well as two clicks.</description></item>
/// <item><description>Alt with a menu's hotkey opens it. While a menu is open, the arrows move through menus and
/// items, Enter chooses, an item's hotkey chooses it, and Esc closes the menu. An open menu takes every key, so
/// nothing reaches the window behind it.</description></item>
/// <item><description><see cref="FindShortcut"/> matches a key against every item's shortcut, such as Ctrl+N, so
/// the host can choose items from anywhere.</description></item>
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
    private const int ItemHeight = 20;
    private const int SeparatorHeight = 9;
    private const int MenuPadding = 4;
    private const int CheckRoom = 20;
    private const int ShortcutGap = 24;
    private const int MinimumMenuWidth = 160;

    private IReadOnlyList<BarMenu> menus = [];
    private readonly List<(int X, int Width)> titles = [];
    private readonly List<(int Y, int Height)> rows = [];
    private UIRect dropdown;
    private int open = -1;
    private int highlighted = -1;
    private bool openedByThisPress;

    /// <summary>The menus in the bar, left to right.</summary>
    public IReadOnlyList<BarMenu> Menus => menus;

    /// <summary>Whether a menu is open.</summary>
    public bool IsOpen => open >= 0;

    /// <summary>The open menu, or <see langword="null"/>.</summary>
    public BarMenu? OpenMenu => open >= 0 ? menus[open] : null;

    /// <summary>The highlighted item in the open menu, or <see langword="null"/>.</summary>
    public UIMenuEntry? Highlighted => open >= 0 && highlighted >= 0 ? menus[open].Menu.Entries[highlighted] : null;

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

    /// <summary>Closes the open menu, if there is one.</summary>
    public void Close()
    {
        open = -1;
        highlighted = -1;
        rows.Clear();
        dropdown = default;
    }

    /// <summary>Whether a point is on the bar or in the open menu.</summary>
    public bool Contains(int x, int y) => y < Height || (open >= 0 && dropdown.Contains(x, y));

    /// <summary>Opens a menu by its position in the bar, with its first item that can be chosen highlighted.</summary>
    public void OpenAt(int index)
    {
        if (index < 0 || index >= menus.Count)
            return;

        if (open != index)
        {
            rows.Clear();
            dropdown = default;
        }

        open = index;
        highlighted = Step(-1, 1);
    }

    /// <summary>Handles the pointer for a frame, while the bar owns it or a menu is open.</summary>
    /// <returns>The item chosen, if one was.</returns>
    public MenuChoice? UpdatePointer(int x, int y, bool pressed, bool released)
    {
        var title = TitleAt(x, y);
        var inMenu = open >= 0 && dropdown.Contains(x, y);
        var row = inMenu ? RowAt(y) : -1;

        if (pressed)
        {
            openedByThisPress = false;
            if (title >= 0)
            {
                if (title == open)
                {
                    Close();
                }
                else
                {
                    OpenAt(title);
                    highlighted = -1;
                    openedByThisPress = true;
                }

                return null;
            }

            if (!inMenu)
            {
                // A press anywhere else just closes the menu, and goes no further.
                Close();
                return null;
            }
        }

        if (open < 0)
            return null;

        // While a menu is open, the pointer moves between menus and picks out items.
        if (title >= 0 && title != open)
        {
            OpenAt(title);
            highlighted = -1;
            return null;
        }

        if (row >= 0)
            highlighted = menus[open].IsEnabled(menus[open].Menu.Entries[row]) ? row : -1;
        else if (title < 0)
            highlighted = -1;

        if (!released)
            return null;

        if (row >= 0 && highlighted == row)
            return Choose(row);

        // Releasing on the title that just opened leaves the menu open, to be clicked through.
        if (title < 0 && !inMenu && !openedByThisPress)
            Close();
        return null;
    }

    /// <summary>Handles a key while a menu is open. Every key is taken, chosen or not.</summary>
    /// <returns>The item chosen, if one was.</returns>
    public MenuChoice? UpdateKey(KeyPress key)
    {
        if (open < 0)
            return null;

        switch (key.Key)
        {
            case DesktopKey.Escape:
                Close();
                return null;
            case DesktopKey.Left:
                OpenAt((open + menus.Count - 1) % menus.Count);
                return null;
            case DesktopKey.Right:
                OpenAt((open + 1) % menus.Count);
                return null;
            case DesktopKey.Up:
                highlighted = Step(highlighted, -1);
                return null;
            case DesktopKey.Down:
                highlighted = Step(highlighted, 1);
                return null;
            case DesktopKey.Enter:
                return highlighted >= 0 ? Choose(highlighted) : null;
            case DesktopKey.Character when key.Alt && !key.Control:
                OpenByHotkey(key.Character);
                return null;
            case DesktopKey.Character when !key.Control:
                var entries = menus[open].Menu.Entries;
                for (var index = 0; index < entries.Count; index++)
                {
                    if (entries[index].Hotkey is { } hotkey && SameLetter(hotkey, key.Character) && menus[open].IsEnabled(entries[index]))
                        return Choose(index);
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

    /// <summary>The item whose shortcut this key is, if it can be chosen.</summary>
    public MenuChoice? FindShortcut(KeyPress key)
    {
        if (key.Key != DesktopKey.Character || !(key.Control || key.Alt))
            return null;

        foreach (var menu in menus)
        {
            foreach (var entry in menu.Menu.Entries)
            {
                if (entry.Shortcut is { } shortcut && shortcut.Control == key.Control && shortcut.Alt == key.Alt
                    && shortcut.Shift == key.Shift && SameLetter(shortcut.Key, key.Character) && menu.IsEnabled(entry))
                {
                    return new MenuChoice(menu, entry);
                }
            }
        }

        return null;
    }

    /// <summary>Draws the bar across the top of the screen, then the open menu below its title.</summary>
    public void Draw(IPainter painter)
    {
        ArgumentNullException.ThrowIfNull(painter);
        const int Size = DesktopTheme.TextSize;

        DesktopTheme.FillGradient(painter, 0, 0, painter.Width, Height, DesktopTheme.TitleBarTop, DesktopTheme.MenuBarBottom);
        painter.FillRectangle(0, Height - 1, painter.Width, 1, DesktopTheme.Border);

        titles.Clear();
        var x = FirstTitleX;
        var textY = TextY(0, Height);
        for (var index = 0; index < menus.Count; index++)
        {
            var menu = menus[index];
            var width = painter.MeasureText(menu.Menu.Title, Size) + (2 * TitlePadding) + (menu.Emphasized ? 1 : 0);
            titles.Add((x, width));

            var color = DesktopTheme.Text;
            if (index == open)
            {
                painter.FillRectangle(x, 0, width, Height - 1, DesktopTheme.Accent);
                color = DesktopTheme.HighlightedText;
            }

            DrawLabel(painter, menu.Menu.Title, menu.Menu.Hotkey, x + TitlePadding, textY, color, menu.Emphasized);
            x += width;
        }

        if (open >= 0)
            DrawMenu(painter);
    }

    private void DrawMenu(IPainter painter)
    {
        const int Size = DesktopTheme.TextSize;
        var menu = menus[open];
        var entries = menu.Menu.Entries;

        // Wide enough for the longest title and shortcut, kept on the screen.
        var widest = MinimumMenuWidth;
        foreach (var entry in entries)
        {
            if (entry.IsSeparator)
                continue;

            var width = CheckRoom + painter.MeasureText(entry.Title!, Size) + 12;
            if (entry.Shortcut is { } shortcut)
                width += ShortcutGap + painter.MeasureText(shortcut.ToString(), Size);
            widest = Math.Max(widest, width);
        }

        var height = MenuPadding * 2;
        foreach (var entry in entries)
            height += entry.IsSeparator ? SeparatorHeight : ItemHeight;

        var (titleX, _) = titles[open];
        var left = Math.Max(0, Math.Min(titleX, painter.Width - widest));
        dropdown = new UIRect(left, Height - 1, widest, height);

        painter.FillRectangle(left + DesktopTheme.ShadowSize, Height - 1 + DesktopTheme.ShadowSize, widest, height, DesktopTheme.Shadow);
        painter.FillRectangle(left, Height - 1, widest, height, DesktopTheme.MenuBackground);
        DesktopTheme.Outline(painter, left, Height - 1, widest, height, DesktopTheme.Border);

        rows.Clear();
        var y = Height - 1 + MenuPadding;
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (entry.IsSeparator)
            {
                rows.Add((y, SeparatorHeight));
                painter.FillRectangle(left + 1, y + (SeparatorHeight / 2), widest - 2, 1, DesktopTheme.Separator);
                y += SeparatorHeight;
                continue;
            }

            rows.Add((y, ItemHeight));
            var enabled = menu.IsEnabled(entry);
            var color = enabled ? DesktopTheme.Text : DesktopTheme.DisabledText;
            var shortcutColor = enabled ? DesktopTheme.SecondaryText : DesktopTheme.DisabledText;
            if (index == highlighted)
            {
                painter.FillRectangle(left + 1, y, widest - 2, ItemHeight, DesktopTheme.Accent);
                color = shortcutColor = DesktopTheme.HighlightedText;
            }

            if (menu.IsChecked(entry))
                DrawCheck(painter, left + 7, y + 5, color);

            var textY = TextY(y, ItemHeight);
            DrawLabel(painter, entry.Title!, entry.Hotkey, left + CheckRoom, textY, color, emphasized: false);
            if (entry.Shortcut is { } shortcut)
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

    private static int TextY(int top, int height) => top + ((height - (int)MathF.Ceiling(DesktopTheme.TextSize * 1.35f)) / 2);

    private MenuChoice Choose(int index)
    {
        var choice = new MenuChoice(menus[open], menus[open].Menu.Entries[index]);
        Close();
        return choice;
    }

    /// <summary>The next item that can be chosen, from <paramref name="from"/> in a direction, wrapping round.</summary>
    private int Step(int from, int direction)
    {
        if (open < 0)
            return -1;

        var entries = menus[open].Menu.Entries;
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

    private int RowAt(int y)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            if (y >= rows[index].Y && y < rows[index].Y + rows[index].Height)
                return menus[open].Menu.Entries[index].IsSeparator ? -1 : index;
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
