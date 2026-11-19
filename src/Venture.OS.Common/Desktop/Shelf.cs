using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>An icon on a shelf.</summary>
/// <param name="Title">The name shown above it under the pointer, and the initial on its tile without an icon.</param>
/// <param name="Icon">Its icon, at <see cref="Shelf.IconSize"/>, or <see langword="null"/> for a tile with its initial.</param>
/// <param name="IsOpen">Whether to put a dot under it, as under an open app.</param>
/// <param name="Group">Which group it's in; a divider goes between neighbours whose groups differ.</param>
/// <param name="IsWindow">
/// Whether it stands for a window rather than an app, such as a minimized one: a pale tile, so it reads as a window.
/// </param>
public sealed record ShelfItem(string Title, DesktopIcon? Icon = null, bool IsOpen = false, int Group = 0, bool IsWindow = false);

/// <summary>
/// A glassy shelf of icons, as the dock is: each an app's icon, or a colored tile with its initial, its color from its
/// title so it keeps it; a dot under open ones; a divider between groups; and the title of the icon under the
/// pointer above it. It draws and finds icons; whoever holds it says where it is and what the pointer does.
/// </summary>
/// <remarks>
/// It's what <c>&lt;Shelf&gt;</c> controls draw, and the desktop's own dock too. The hover label is drawn above the
/// shelf, outside its bounds, as the dock's always was.
/// </remarks>
public sealed class Shelf
{
    /// <summary>An icon's width and height.</summary>
    public const int IconSize = 48;

    private const int Gap = 10;
    private const int Padding = 8;
    private const int DotRoom = 8;

    // The extra room each divider between groups takes, on top of the usual gap.
    private const int DividerRoom = 12;
    private const int LabelSize = DesktopTheme.TextSize;

    // Tile colors, picked by the title.
    private static readonly UIColor[] Colors =
    [
        new(46, 132, 222), new(62, 168, 92), new(232, 146, 40), new(152, 92, 204),
        new(214, 72, 92), new(34, 160, 170), new(120, 128, 140),
    ];

    private IReadOnlyList<ShelfItem> items = [];
    private (int X, int Y) at;

    /// <summary>The shelf's height, the same whatever it holds.</summary>
    public static int Height => IconSize + (2 * Padding) + DotRoom;

    /// <summary>The icons, left to right.</summary>
    public IReadOnlyList<ShelfItem> Items => items;

    /// <summary>How wide the shelf is with its icons, or 0 with none.</summary>
    public int Width => items.Count == 0
        ? 0
        : (items.Count * IconSize) + ((items.Count - 1) * Gap) + (2 * Padding) + (DividersBefore(items.Count) * DividerRoom);

    /// <summary>Where the shelf is; empty with no icons.</summary>
    public UIRect Bounds => items.Count == 0 ? default : new UIRect(at.X, at.Y, Width, Height);

    /// <summary>Replaces the icons.</summary>
    public void SetItems(IReadOnlyList<ShelfItem> newItems)
    {
        ArgumentNullException.ThrowIfNull(newItems);
        items = newItems;
    }

    /// <summary>Puts the shelf's top-left corner at a point.</summary>
    public void Place(int x, int y) => at = (x, y);

    /// <summary>Where an icon is.</summary>
    public UIRect IconBounds(int index)
    {
        var divider = DividersBefore(index + 1) * DividerRoom;
        return new UIRect(at.X + Padding + (index * (IconSize + Gap)) + divider, at.Y + Padding, IconSize, IconSize);
    }

    /// <summary>The icon at a point, or -1.</summary>
    public int IndexAt(int x, int y)
    {
        if (items.Count == 0 || !Bounds.Contains(x, y))
            return -1;

        for (var index = 0; index < items.Count; index++)
        {
            if (IconBounds(index).Contains(x, y))
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Draws the shelf, its icons, dots under open ones, dividers, and the title of the hovered icon above it, kept
    /// within <paramref name="screenWidth"/>.
    /// </summary>
    /// <param name="hovered">The icon under the pointer, or -1.</param>
    /// <param name="pressed">The icon being pressed, or -1; it's shaded while it's also <paramref name="hovered"/>.</param>
    public void Draw(IPainter painter, int hovered, int pressed, int screenWidth)
    {
        ArgumentNullException.ThrowIfNull(painter);
        if (items.Count == 0)
            return;

        var (shelfX, shelfY, shelfWidth) = (at.X, at.Y, Width);
        painter.FillRectangle(shelfX, shelfY, shelfWidth, Height, DesktopTheme.DockShelf);
        painter.FillRectangle(shelfX, shelfY, shelfWidth, 1, DesktopTheme.DockEdge);
        painter.FillRectangle(shelfX, shelfY + 1, 1, Height - 1, DesktopTheme.DockEdge);
        painter.FillRectangle(shelfX + shelfWidth - 1, shelfY + 1, 1, Height - 1, DesktopTheme.DockEdge);

        for (var index = 0; index < items.Count; index++)
            DrawIcon(painter, index, index == pressed && index == hovered);

        for (var index = 1; index < items.Count; index++)
        {
            if (items[index].Group == items[index - 1].Group)
                continue;

            var x = (int)IconBounds(index).X - ((Gap + DividerRoom) / 2);
            painter.FillRectangle(x, shelfY + Padding, 1, IconSize, DesktopTheme.DockEdge);
        }

        if (hovered >= 0 && hovered < items.Count)
            DrawLabel(painter, items[hovered].Title, IconBounds(hovered), screenWidth);
    }

    /// <summary>How many dividers come before the item at <paramref name="end"/>: one at each change of group.</summary>
    private int DividersBefore(int end)
    {
        var count = 0;
        for (var index = 1; index < Math.Min(end, items.Count); index++)
        {
            if (items[index].Group != items[index - 1].Group)
                count++;
        }

        return count;
    }

    private void DrawIcon(IPainter painter, int index, bool pressed)
    {
        var item = items[index];
        var icon = IconBounds(index);
        var (x, y) = ((int)icon.X, (int)icon.Y);
        if (item.Icon is { } appIcon)
        {
            // A shaped icon carries its own shadow; a tile gets the same one the letter tiles have.
            if (appIcon.IsTile)
                DesktopTheme.FillRoundedGradient(painter, x, y + 2, IconSize, IconSize, 10, DesktopTheme.Shadow, DesktopTheme.Shadow);
            painter.DrawImage(appIcon.Image, x, y);
            DrawPressedAndOpen(painter, item, x, y, pressed);
            return;
        }

        // A window is a pale tile, so it reads as a window rather than an app.
        var color = item.IsWindow ? DesktopTheme.TitleBarBottom : ColorFor(item.Title);
        var light = DesktopTheme.Blend(color, new UIColor(255, 255, 255), 0.45f);

        DesktopTheme.FillRoundedGradient(painter, x, y + 2, IconSize, IconSize, 10, DesktopTheme.Shadow, DesktopTheme.Shadow);
        DesktopTheme.FillRoundedGradient(painter, x, y, IconSize, IconSize, 10, light, color);

        // The initial, in heavier text.
        const int InitialSize = 26;
        var initial = item.Title.Length > 0 ? char.ToUpperInvariant(item.Title[0]).ToString() : "?";
        var initialX = x + ((IconSize - painter.MeasureText(initial, InitialSize)) / 2);
        var initialY = y + ((IconSize - (int)MathF.Ceiling(InitialSize * 1.35f)) / 2);
        var ink = item.IsWindow ? DesktopTheme.Text : DesktopTheme.HighlightedText;
        painter.DrawText(initial, initialX, initialY, ink, InitialSize);
        painter.DrawText(initial, initialX + 1, initialY, ink, InitialSize);
        if (item.IsWindow)
            DesktopTheme.Outline(painter, x + 4, y + 4, IconSize - 8, IconSize - 8, DesktopTheme.Border);

        DrawPressedAndOpen(painter, item, x, y, pressed);
    }

    private static void DrawPressedAndOpen(IPainter painter, ShelfItem item, int x, int y, bool pressed)
    {
        if (pressed)
            DesktopTheme.FillRoundedGradient(painter, x, y, IconSize, IconSize, 10, DesktopTheme.IconPressed, DesktopTheme.IconPressed);

        if (item.IsOpen)
            painter.FillRectangle(x + (IconSize / 2) - 2, y + IconSize + 3, 4, 4, DesktopTheme.OpenAppDot);
    }

    private void DrawLabel(IPainter painter, string title, UIRect icon, int screenWidth)
    {
        var textWidth = painter.MeasureText(title, LabelSize);
        var width = textWidth + 16;
        const int LabelHeight = 22;
        var x = Math.Clamp((int)(icon.X + (icon.Width / 2)) - (width / 2), 0, Math.Max(0, screenWidth - width));
        var y = at.Y - LabelHeight - 6;

        DesktopTheme.FillRoundedGradient(painter, x, y, width, LabelHeight, 11, DesktopTheme.DockLabel, DesktopTheme.DockLabel);
        painter.DrawText(title, x + 8, y + ((LabelHeight - (int)MathF.Ceiling(LabelSize * 1.35f)) / 2), DesktopTheme.HighlightedText, LabelSize);
    }

    /// <summary>A color that depends only on the title, so an app looks the same every time.</summary>
    private static UIColor ColorFor(string title)
    {
        var hash = 0;
        foreach (var character in title)
            hash = unchecked((hash * 31) + char.ToUpperInvariant(character));
        return Colors[(hash & int.MaxValue) % Colors.Length];
    }
}
