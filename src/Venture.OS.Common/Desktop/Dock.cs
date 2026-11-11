using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>An app in the dock.</summary>
/// <param name="Name">The name it shows, from its <c>Info.xml</c>.</param>
/// <param name="Target">What opens it, such as its name in the apps folder.</param>
/// <param name="IsOpen">Whether it's open, which puts a dot under its icon.</param>
/// <param name="IsUser">Whether it's one of the user's apps, which sit after the system's, past a divider.</param>
public sealed record DockItem(string Name, string Target, bool IsOpen, bool IsUser = false);

/// <summary>
/// The dock: a glassy shelf along the bottom of the screen with an icon for each of the system's apps.
/// </summary>
/// <remarks>
/// <para>Clicking an icon reports its item, so the desktop can open the app; a press that leaves the icon before the
/// button comes up does nothing. The icon under the pointer shows the app's name above it, and an open app has a dot
/// below.</para>
/// <para>Apps have no icon images yet, since the desktop doesn't draw images, so each icon is a colored tile with
/// the app's initial. The color comes from the name, so an app keeps its color.</para>
/// </remarks>
public sealed class Dock(int screenWidth, int screenHeight)
{
    /// <summary>An icon's width and height.</summary>
    public const int IconSize = 48;

    private const int Gap = 10;
    private const int Padding = 8;
    private const int DotRoom = 8;

    // The extra room the divider between system and user apps takes, on top of the usual gap.
    private const int DividerRoom = 12;
    private const int LabelSize = DesktopTheme.TextSize;

    // Tile colors, picked by the app's name.
    private static readonly UIColor[] Colors =
    [
        new(46, 132, 222), new(62, 168, 92), new(232, 146, 40), new(152, 92, 204),
        new(214, 72, 92), new(34, 160, 170), new(120, 128, 140),
    ];

    private IReadOnlyList<DockItem> items = [];
    private int pressed = -1;
    private int hovered = -1;

    /// <summary>The apps in the dock, left to right.</summary>
    public IReadOnlyList<DockItem> Items => items;

    /// <summary>The icon under the pointer, or <see langword="null"/>.</summary>
    public DockItem? Hovered => hovered >= 0 && hovered < items.Count ? items[hovered] : null;

    /// <summary>The shelf's height, which windows may sit over.</summary>
    public static int Height => IconSize + (2 * Padding) + DotRoom;

    /// <summary>The shelf, centered along the bottom; empty with no apps.</summary>
    public UIRect Bounds
    {
        get
        {
            if (items.Count == 0)
                return default;

            var width = (items.Count * IconSize) + ((items.Count - 1) * Gap) + (2 * Padding) + (FirstUser > 0 ? DividerRoom : 0);
            return new UIRect((screenWidth - width) / 2, screenHeight - Height, width, Height);
        }
    }

    /// <summary>Replaces the apps, such as after one opens or closes.</summary>
    public void SetItems(IReadOnlyList<DockItem> newItems)
    {
        ArgumentNullException.ThrowIfNull(newItems);
        items = newItems;
        if (pressed >= items.Count)
            pressed = -1;
        if (hovered >= items.Count)
            hovered = -1;
    }

    /// <summary>Whether a point is on the shelf.</summary>
    public bool Contains(int x, int y) => items.Count > 0 && Bounds.Contains(x, y);

    /// <summary>Where an icon is.</summary>
    public UIRect IconBounds(int index)
    {
        var shelf = Bounds;
        var divider = FirstUser > 0 && index >= FirstUser ? DividerRoom : 0;
        return new UIRect(shelf.X + Padding + (index * (IconSize + Gap)) + divider, shelf.Y + Padding, IconSize, IconSize);
    }

    /// <summary>The first user app's position, when there are system apps before it to divide it from; else -1.</summary>
    private int FirstUser
    {
        get
        {
            for (var index = 0; index < items.Count; index++)
            {
                if (items[index].IsUser)
                    return index;
            }

            return -1;
        }
    }

    /// <summary>Handles the pointer for a frame.</summary>
    /// <param name="owned">Whether the dock has the pointer: the button went down on it, or isn't down at all.</param>
    /// <returns>The app clicked, if one was.</returns>
    public DockItem? Update(int x, int y, bool owned, bool justPressed, bool released)
    {
        hovered = owned ? IconAt(x, y) : -1;

        if (justPressed && owned)
            pressed = hovered;

        if (!released)
            return null;

        var clicked = pressed >= 0 && pressed == IconAt(x, y) ? items[pressed] : null;
        pressed = -1;
        return clicked;
    }

    /// <summary>Draws the shelf, the icons, the dots under open apps, and the name of the icon under the pointer.</summary>
    public void Draw(IPainter painter)
    {
        ArgumentNullException.ThrowIfNull(painter);
        if (items.Count == 0)
            return;

        var shelf = Bounds;
        var (shelfX, shelfY, shelfWidth) = ((int)shelf.X, (int)shelf.Y, (int)shelf.Width);
        painter.FillRectangle(shelfX, shelfY, shelfWidth, Height, DesktopTheme.DockShelf);
        painter.FillRectangle(shelfX, shelfY, shelfWidth, 1, DesktopTheme.DockEdge);
        painter.FillRectangle(shelfX, shelfY + 1, 1, Height - 1, DesktopTheme.DockEdge);
        painter.FillRectangle(shelfX + shelfWidth - 1, shelfY + 1, 1, Height - 1, DesktopTheme.DockEdge);

        for (var index = 0; index < items.Count; index++)
            DrawIcon(painter, index);

        if (FirstUser > 0)
        {
            var x = (int)IconBounds(FirstUser).X - ((Gap + DividerRoom) / 2);
            painter.FillRectangle(x, shelfY + Padding, 1, IconSize, DesktopTheme.DockEdge);
        }

        if (Hovered is { } item)
            DrawLabel(painter, item.Name, IconBounds(hovered));
    }

    private void DrawIcon(IPainter painter, int index)
    {
        var item = items[index];
        var icon = IconBounds(index);
        var (x, y) = ((int)icon.X, (int)icon.Y);
        var color = ColorFor(item.Name);
        var light = DesktopTheme.Blend(color, new UIColor(255, 255, 255), 0.45f);

        DesktopTheme.FillRoundedGradient(painter, x, y + 2, IconSize, IconSize, 10, DesktopTheme.Shadow, DesktopTheme.Shadow);
        DesktopTheme.FillRoundedGradient(painter, x, y, IconSize, IconSize, 10, light, color);

        // The initial, in heavier text.
        const int InitialSize = 26;
        var initial = item.Name.Length > 0 ? char.ToUpperInvariant(item.Name[0]).ToString() : "?";
        var initialX = x + ((IconSize - painter.MeasureText(initial, InitialSize)) / 2);
        var initialY = y + ((IconSize - (int)MathF.Ceiling(InitialSize * 1.35f)) / 2);
        painter.DrawText(initial, initialX, initialY, DesktopTheme.HighlightedText, InitialSize);
        painter.DrawText(initial, initialX + 1, initialY, DesktopTheme.HighlightedText, InitialSize);

        if (index == pressed && index == hovered)
            DesktopTheme.FillRoundedGradient(painter, x, y, IconSize, IconSize, 10, DesktopTheme.IconPressed, DesktopTheme.IconPressed);

        if (item.IsOpen)
            painter.FillRectangle(x + (IconSize / 2) - 2, y + IconSize + 3, 4, 4, DesktopTheme.OpenAppDot);
    }

    private void DrawLabel(IPainter painter, string name, UIRect icon)
    {
        var textWidth = painter.MeasureText(name, LabelSize);
        var width = textWidth + 16;
        const int LabelHeight = 22;
        var x = Math.Clamp((int)(icon.X + (icon.Width / 2)) - (width / 2), 0, Math.Max(0, screenWidth - width));
        var y = (int)Bounds.Y - LabelHeight - 6;

        DesktopTheme.FillRoundedGradient(painter, x, y, width, LabelHeight, 11, DesktopTheme.DockLabel, DesktopTheme.DockLabel);
        painter.DrawText(name, x + 8, y + ((LabelHeight - (int)MathF.Ceiling(LabelSize * 1.35f)) / 2), DesktopTheme.HighlightedText, LabelSize);
    }

    private int IconAt(int x, int y)
    {
        if (!Contains(x, y))
            return -1;

        for (var index = 0; index < items.Count; index++)
        {
            if (IconBounds(index).Contains(x, y))
                return index;
        }

        return -1;
    }

    /// <summary>A color that depends only on the name, so an app looks the same every time.</summary>
    private static UIColor ColorFor(string name)
    {
        var hash = 0;
        foreach (var character in name)
            hash = unchecked((hash * 31) + char.ToUpperInvariant(character));
        return Colors[(hash & int.MaxValue) % Colors.Length];
    }
}
