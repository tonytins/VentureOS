using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>
/// An item a grid shows: a picture, a color, or, with neither, a plain tile with its title.
/// </summary>
/// <param name="Title">What it's called, shown as the grid's <c>titles</c> say.</param>
/// <param name="Name">What the grid reports when it's chosen, or <see langword="null"/>.</param>
/// <param name="Image">An <c>&lt;Image&gt;</c> in the document, supplied with <see cref="UIView.SetImage"/>.</param>
/// <param name="Picture">A picture the owner supplies itself, such as one read from a file; it wins over <paramref name="Image"/>.</param>
/// <param name="Color">A swatch's color, drawn as a circle.</param>
/// <param name="Tooltip">What shows while the pointer rests on it.</param>
public sealed record UIGridEntry(
    string Title, string? Name = null, string? Image = null, DesktopImage? Picture = null, UIColor? Color = null,
    string? Tooltip = null);

/// <summary>A grid's items, which its owner may replace, which one is chosen, and how far it's scrolled.</summary>
public sealed class UIGridState
{
    /// <summary>The items, in order. Change them freely; the grid keeps up on the next frame.</summary>
    public List<UIGridEntry> Items { get; } = [];

    /// <summary>The chosen item's index, or -1. Setting it reports no event.</summary>
    public int SelectedIndex { get; set; } = -1;

    /// <summary>The first row showing, for scrolling.</summary>
    public int FirstRow { get; set; }
}

/// <summary>
/// Grids: items of one size in rows that wrap to the grid's width and scroll, with a bar of their own, once there are
/// more than fit. A click chooses an item, ringed in the accent, and reports <see cref="UIEventKind.Selected"/> with
/// its index and name; a second click soon after reports <see cref="UIEventKind.Submitted"/>.
/// </summary>
public sealed partial class UIView
{
    // Room around each cell for the ring a chosen item gets, and how far the ring sits outside it.
    private const int GridRing = 3;
    private const int GridTileRadius = 6;

    // Each item's picture at the size it was last drawn, kept with the picture it was made from.
    private readonly Dictionary<(string Grid, int Index), (DesktopImage Source, DesktopImage Scaled)> gridPictures = [];

    /// <summary>The grid with this name, or <see langword="null"/>.</summary>
    public UIGridState? Grid(string name) => Find(name)?.Grid;

    /// <summary>A grid's items as its document lists them, with their titles and tooltips resolved.</summary>
    private UIGridState NewGrid(UIGridLayout layout, Func<UIStringResource, string?>? resolveString)
    {
        UIGridState grid = new();
        foreach (var item in layout.Items)
        {
            grid.Items.Add(new UIGridEntry(
                Resolve(item.Title, resolveString) ?? "", item.Name, item.Image, Color: item.Color,
                Tooltip: item.Tooltip is { } tooltip ? Resolve(UIText.Reference(tooltip), resolveString) : null));
        }

        return grid;
    }

    private int GridTitleRoom(UIControl grid) =>
        grid.Grid!.Titles == UIGridTitles.None ? 0 : LineHeight(TextSize(grid)) + 4;

    private int GridRowHeight(UIControl grid) =>
        (int)(grid.Grid!.ItemHeight + grid.Grid.Spacing) + GridTitleRoom(grid);

    private int GridVisibleRows(UIControl grid) =>
        Math.Max(1, (int)((PlaceOf(grid).Height - (2 * GridRing) + grid.Grid!.Spacing) / GridRowHeight(grid)));

    private static int GridColumns(UIGridLayout layout, float width) =>
        Math.Max(1, (int)((width - (2 * GridRing) + layout.Spacing) / (layout.ItemWidth + layout.Spacing)));

    /// <summary>
    /// How many columns and rows a grid's items take. A classic bar takes room once the rows don't fit, and the items
    /// wrap again in what's left; fewer columns only add rows, so the choice holds.
    /// </summary>
    private (int Columns, int Rows) GridShape(UIControl grid)
    {
        var layout = grid.Grid!;
        var count = states[grid.Name].Grid!.Items.Count;
        var width = PlaceOf(grid).Width;
        var columns = GridColumns(layout, width);
        var rows = (count + columns - 1) / columns;
        if (StyleOf(grid) == ScrollBarStyle.Classic && rows > GridVisibleRows(grid))
        {
            columns = GridColumns(layout, width - ClassicScrollBarWidth);
            rows = (count + columns - 1) / columns;
        }

        return (columns, rows);
    }

    /// <summary>The item under a point in the view, cell or title, or -1.</summary>
    private int GridItemAt(UIControl grid, float x, float y)
    {
        var layout = grid.Grid!;
        var state = states[grid.Name].Grid!;
        var place = PlaceOf(grid);
        var (columns, _) = GridShape(grid);
        var along = x - place.X - GridRing;
        var down = y - place.Y - GridRing;
        if (along < 0 || down < 0)
            return -1;

        var step = layout.ItemWidth + layout.Spacing;
        var column = (int)(along / step);
        var row = (int)(down / GridRowHeight(grid));
        if (column >= columns || along - (column * step) >= layout.ItemWidth
            || down - (row * GridRowHeight(grid)) >= layout.ItemHeight + GridTitleRoom(grid) || row >= GridVisibleRows(grid))
            return -1;

        var index = ((state.FirstRow + row) * columns) + column;
        return index < state.Items.Count ? index : -1;
    }

    private void PressGrid(UIControl grid, UIGridState state, float x, float y, TimeSpan time, List<UIEvent> events)
    {
        var index = GridItemAt(grid, x, y);
        if (index < 0)
        {
            lastListClick = null;
            return;
        }

        var item = state.Items[index];
        if (index != state.SelectedIndex)
        {
            state.SelectedIndex = index;
            events.Add(new UIEvent(UIEventKind.Selected, grid.Name, Index: index, Item: item.Name));
        }

        // As a list does: a second press on the same item soon after opens it, and a third starts over.
        if (lastListClick is { } previous && previous.List == grid.Name && previous.Index == index
            && time - previous.Time <= DoubleClickInterval)
        {
            events.Add(new UIEvent(UIEventKind.Submitted, grid.Name, item.Title, Index: index, Item: item.Name));
            lastListClick = null;
        }
        else
        {
            lastListClick = (grid.Name, index, time);
        }
    }

    /// <summary>
    /// What a grid shows while the pointer rests on one of its items: the item's tooltip, or its title when the grid
    /// shows none; <see langword="null"/> elsewhere.
    /// </summary>
    private string? GridTooltipAt(UIControl grid, int x, int y)
    {
        var index = GridItemAt(grid, x, y);
        if (index < 0)
            return null;

        var item = states[grid.Name].Grid!.Items[index];
        return item.Tooltip ?? (grid.Grid!.Titles == UIGridTitles.None && item.Title.Length > 0 ? item.Title : null);
    }

    /// <summary>Keeps a grid's scroll in range, and its choice among its items.</summary>
    private void FollowGrid(UIControl grid, UIGridState state)
    {
        var (_, rows) = GridShape(grid);
        state.FirstRow = Math.Clamp(state.FirstRow, 0, Math.Max(0, rows - GridVisibleRows(grid)));
        if (state.SelectedIndex >= state.Items.Count)
            state.SelectedIndex = -1;
    }

    /// <summary>
    /// Draws a grid's visible rows with nothing behind them, over the window: pictures scaled to their cells, colors as
    /// circles, and plain tiles with their titles, the chosen one ringed in the accent, then titles as the grid says.
    /// </summary>
    private void DrawGrid(IPainter painter, UIControl control, State state, UIRect bounds)
    {
        var layout = control.Grid!;
        var grid = state.Grid!;
        var (columns, _) = GridShape(control);
        var visibleRows = GridVisibleRows(control);
        var size = TextSize(control);
        var rowHeight = GridRowHeight(control);
        var (itemWidth, itemHeight) = ((int)layout.ItemWidth, (int)layout.ItemHeight);
        var step = (int)(layout.ItemWidth + layout.Spacing);
        var behind = control.InSidebar ? DesktopTheme.SidebarBackground : DesktopTheme.Panel;

        for (var row = 0; row < visibleRows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var index = ((grid.FirstRow + row) * columns) + column;
                if (index >= grid.Items.Count)
                    break;

                var item = grid.Items[index];
                var chosen = index == grid.SelectedIndex;
                var x = (int)bounds.X + GridRing + (column * step);
                var y = (int)bounds.Y + GridRing + (row * rowHeight);

                if (item.Color is { } color)
                {
                    DrawSwatch(painter, x, y, itemWidth, itemHeight, color, chosen, behind);
                }
                else if (item.Picture is not null || item.Image is not null)
                {
                    DrawGridPicture(painter, control, index, item, x, y, itemWidth, itemHeight, chosen);
                }
                else
                {
                    DrawPlainTile(painter, control, state, item, x, y, itemWidth, itemHeight, chosen, size);
                    continue;
                }

                var titled = layout.Titles == UIGridTitles.Below || (layout.Titles == UIGridTitles.Selected && chosen);
                if (titled && item.Title.Length > 0)
                    DrawGridTitle(painter, control, state, item.Title, x, y + itemHeight + 2, itemWidth, bounds, chosen, size);
            }
        }

        DrawScrollBar(painter, control, state, bounds);
    }

    /// <summary>A color as a circle, centred in its cell, inside a ring in the accent while it's chosen.</summary>
    private static void DrawSwatch(IPainter painter, int x, int y, int width, int height, UIColor color, bool chosen, UIColor behind)
    {
        var diameter = Math.Min(width, height);
        var left = x + ((width - diameter) / 2);
        var top = y + ((height - diameter) / 2);
        if (chosen)
        {
            var ring = diameter + (2 * GridRing);
            DesktopTheme.FillRoundedGradient(painter, left - GridRing, top - GridRing, ring, ring, ring / 2, DesktopTheme.Accent, DesktopTheme.Accent);
            DesktopTheme.FillRoundedGradient(painter, left - 1, top - 1, diameter + 2, diameter + 2, (diameter + 2) / 2, behind, behind);
        }

        DesktopTheme.FillRoundedGradient(painter, left, top, diameter, diameter, diameter / 2, color, color);
    }

    /// <summary>
    /// A picture scaled to its cell, scaled once and kept until the picture or the cell changes, inside a ring in the
    /// accent while it's chosen; a picture that wasn't supplied shows the missing outline, as a visual's does.
    /// </summary>
    private void DrawGridPicture(IPainter painter, UIControl grid, int index, UIGridEntry item, int x, int y, int width, int height, bool chosen)
    {
        if (chosen)
        {
            DesktopTheme.Outline(painter, x - GridRing, y - GridRing, width + (2 * GridRing), height + (2 * GridRing), DesktopTheme.Accent);
            DesktopTheme.Outline(painter, x - GridRing + 1, y - GridRing + 1, width + (2 * GridRing) - 2, height + (2 * GridRing) - 2, DesktopTheme.Accent);
        }

        var source = item.Picture ?? (item.Image is { } name ? images.GetValueOrDefault(name) : null);
        if (source is null || width <= 0 || height <= 0 || width > DesktopImage.MaxSide || height > DesktopImage.MaxSide)
        {
            DesktopTheme.Outline(painter, x, y, width, height, DesktopTheme.MissingImage);
            return;
        }

        var key = (grid.Name, index);
        if (!gridPictures.TryGetValue(key, out var kept) || !ReferenceEquals(kept.Source, source)
            || kept.Scaled.Width != width || kept.Scaled.Height != height)
        {
            kept = (source, source.Scaled(width, height));
            gridPictures[key] = kept;
        }

        painter.DrawImage(kept.Scaled, x, y);
    }

    /// <summary>A rounded tile with its title inside, in the accent while it's chosen, as a choice with no picture.</summary>
    private void DrawPlainTile(IPainter painter, UIControl grid, State state, UIGridEntry item, int x, int y, int width, int height, bool chosen, int size)
    {
        if (chosen)
            DesktopTheme.FillRoundedGradient(painter, x - GridRing + 1, y - GridRing + 1, width + (2 * GridRing) - 2,
                height + (2 * GridRing) - 2, GridTileRadius + 2, DesktopTheme.Accent, DesktopTheme.Accent);

        var tile = DesktopTheme.Blend(DesktopTheme.Panel, DesktopTheme.Text, 0.08f);
        DesktopTheme.FillRoundedGradient(painter, x, y, width, height, GridTileRadius, tile, tile);
        var text = Fit(painter, item.Title, size, width - (2 * Inset));
        var ink = state.Disabled ? DesktopTheme.DisabledText : TextColor(grid);
        painter.DrawText(text, x + ((width - painter.MeasureText(text, size)) / 2), y + ((height - LineHeight(size)) / 2), ink, size);
    }

    /// <summary>
    /// A title centred under its cell. Every title fits its cell and the room beside it; a chosen one shown alone may
    /// run as wide as the grid, as a row of colors names the one in use.
    /// </summary>
    private void DrawGridTitle(IPainter painter, UIControl grid, State state, string title, int x, int y, int width, UIRect bounds, bool chosen, int size)
    {
        var room = grid.Grid!.Titles == UIGridTitles.Selected ? (int)bounds.Width : width + (int)grid.Grid.Spacing;
        var text = Fit(painter, title, size, room);
        var measured = painter.MeasureText(text, size);
        var left = Math.Clamp(x + ((width - measured) / 2), (int)bounds.X, Math.Max((int)bounds.X, (int)(bounds.X + bounds.Width) - measured));
        var ink = state.Disabled ? DesktopTheme.DisabledText : chosen ? TextColor(grid) : DesktopTheme.SecondaryText;
        painter.DrawText(text, left, y, ink, size);
    }
}
