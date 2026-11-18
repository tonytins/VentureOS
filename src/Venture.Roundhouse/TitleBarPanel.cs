using Avalonia;
using Avalonia.Controls;

namespace Venture.Roundhouse;

/// <summary>
/// Lays out a title bar as VentureOS's unified title bar does: its first child along the left end, its third along
/// the right end, and its second, the title, centred in the whole bar when it fits clear of both ends, and otherwise
/// in the room between them.
/// </summary>
public sealed class TitleBarPanel : Panel
{
    /// <summary>The space kept between the title and the items at either end.</summary>
    public const double Gap = 12;

    protected override Size MeasureOverride(Size availableSize)
    {
        var (start, title, end) = Parts();
        var height = 0.0;
        foreach (var child in (Control?[])[start, end])
        {
            child?.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            height = Math.Max(height, child?.DesiredSize.Height ?? 0);
        }

        var ends = (start?.DesiredSize.Width ?? 0) + (end?.DesiredSize.Width ?? 0);
        var room = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : Math.Max(0, availableSize.Width - ends - (2 * Gap));
        title?.Measure(new Size(room, availableSize.Height));
        height = Math.Max(height, title?.DesiredSize.Height ?? 0);
        return new Size(ends + (title?.DesiredSize.Width ?? 0) + (2 * Gap), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (start, title, end) = Parts();
        var startWidth = start?.DesiredSize.Width ?? 0;
        var endWidth = end?.DesiredSize.Width ?? 0;
        start?.Arrange(new Rect(0, 0, startWidth, finalSize.Height));
        end?.Arrange(new Rect(finalSize.Width - endWidth, 0, endWidth, finalSize.Height));

        if (title is not null)
            title.Arrange(TitleBounds(finalSize.Width, finalSize.Height, startWidth, endWidth, title.DesiredSize.Width));
        return finalSize;
    }

    /// <summary>
    /// Where the title goes in a bar <paramref name="width"/> wide: centred in the whole bar when it fits clear of
    /// the items at both ends, and otherwise centred in the room between them, no wider than that room.
    /// </summary>
    public static Rect TitleBounds(double width, double height, double startWidth, double endWidth, double titleWidth)
    {
        var left = startWidth + Gap;
        var right = width - endWidth - Gap;
        var centred = (width - titleWidth) / 2;
        if (centred >= left && centred + titleWidth <= right)
            return new Rect(centred, 0, titleWidth, height);

        var room = Math.Max(0, right - left);
        var fitted = Math.Min(titleWidth, room);
        return new Rect(left + ((room - fitted) / 2), 0, fitted, height);
    }

    private (Control? Start, Control? Title, Control? End) Parts() =>
        (Children.Count > 0 ? Children[0] : null, Children.Count > 1 ? Children[1] : null, Children.Count > 2 ? Children[2] : null);
}
