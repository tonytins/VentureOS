using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>
/// The desktop's look: colors and sizes after early Aqua (Mac OS X Tiger), by way of Distro Tycoon 2's appearance.
/// </summary>
/// <remarks>A document's <c>&lt;Style&gt;</c> wins over these for the controls that name it.</remarks>
public static class DesktopTheme
{
    /// <summary>The accent, a clear sky blue, for focus and selection.</summary>
    public static readonly UIColor Accent = new(30, 120, 220);

    /// <summary>Text on light backgrounds.</summary>
    public static readonly UIColor Text = new(24, 28, 36);

    /// <summary>Placeholders and other quieter text.</summary>
    public static readonly UIColor SecondaryText = new(110, 116, 126);

    /// <summary>Text on a control that's turned off.</summary>
    public static readonly UIColor DisabledText = new(150, 154, 160);

    /// <summary>A window's content background.</summary>
    public static readonly UIColor Panel = new(240, 243, 247);

    /// <summary>Lines around windows and controls.</summary>
    public static readonly UIColor Border = new(120, 126, 136);

    /// <summary>The inside of text fields and lists.</summary>
    public static readonly UIColor InputBackground = new(255, 255, 255);

    /// <summary>A selected list item.</summary>
    public static readonly UIColor Selection = new(158, 200, 242);

    /// <summary>The top and bottom of a title bar, and of a button.</summary>
    public static readonly UIColor TitleBarTop = new(250, 251, 253);

    /// <inheritdoc cref="TitleBarTop"/>
    public static readonly UIColor TitleBarBottom = new(196, 210, 226);

    /// <summary>The close button, and the mark on it.</summary>
    public static readonly UIColor CloseButton = new(236, 95, 86);

    /// <inheritdoc cref="CloseButton"/>
    public static readonly UIColor CloseMark = new(90, 20, 16);

    /// <summary>The minimize button's fill.</summary>
    public static readonly UIColor MinimizeButton = new(245, 178, 50);

    /// <summary>The minimize button's rim.</summary>
    public static readonly UIColor MinimizeMark = new(120, 82, 12);

    /// <summary>A button while the pointer is down on it.</summary>
    public static readonly UIColor ButtonPressed = new(170, 196, 228);

    /// <summary>The red outline where an image couldn't be drawn, as Hudini draws it.</summary>
    public static readonly UIColor MissingImage = new(220, 30, 30);

    /// <summary>The drop shadow under windows.</summary>
    public static readonly UIColor Shadow = new(0, 0, 0, 60);

    /// <summary>White laid over a window that isn't in front, as early Aqua faded background windows.</summary>
    public static readonly UIColor InactiveFade = new(255, 255, 255, 90);

    /// <summary>The bottom of the menu bar's gradient, which starts from <see cref="TitleBarTop"/>.</summary>
    public static readonly UIColor MenuBarBottom = new(222, 228, 236);

    /// <summary>A menu's background.</summary>
    public static readonly UIColor MenuBackground = new(250, 251, 253);

    /// <summary>Text on the accent color, such as a highlighted menu item.</summary>
    public static readonly UIColor HighlightedText = new(255, 255, 255);

    /// <summary>The line between groups of menu items.</summary>
    public static readonly UIColor Separator = new(214, 218, 224);

    /// <summary>The dock's glassy shelf, laid over the sky.</summary>
    public static readonly UIColor DockShelf = new(255, 255, 255, 70);

    /// <summary>The dock shelf's bright edge.</summary>
    public static readonly UIColor DockEdge = new(255, 255, 255, 150);

    /// <summary>The dot under an open app's icon.</summary>
    public static readonly UIColor OpenAppDot = new(24, 28, 36);

    /// <summary>The label that names the icon under the pointer.</summary>
    public static readonly UIColor DockLabel = new(0, 0, 0, 150);

    /// <summary>The shade over an icon while it's pressed.</summary>
    public static readonly UIColor IconPressed = new(0, 0, 0, 60);

    /// <summary>The height of a window's title bar, in pixels.</summary>
    public const int TitleBarHeight = 28;

    /// <summary>The size of title and body text, in pixels.</summary>
    public const int TextSize = 13;

    /// <summary>How far a drop shadow reaches.</summary>
    public const int ShadowSize = 4;

    /// <summary>
    /// Fills a rectangle with a top-to-bottom gradient, one row at a time.
    /// </summary>
    public static void FillGradient(IPainter painter, int x, int y, int width, int height, UIColor top, UIColor bottom)
    {
        ArgumentNullException.ThrowIfNull(painter);
        for (var row = 0; row < height; row++)
        {
            var along = height == 1 ? 0 : (float)row / (height - 1);
            painter.FillRectangle(x, y + row, width, 1, Mix(top, bottom, along));
        }
    }

    /// <summary>Draws a one-pixel outline inside a rectangle.</summary>
    public static void Outline(IPainter painter, int x, int y, int width, int height, UIColor color)
    {
        ArgumentNullException.ThrowIfNull(painter);
        if (width <= 0 || height <= 0)
            return;

        painter.FillRectangle(x, y, width, 1, color);
        painter.FillRectangle(x, y + height - 1, width, 1, color);
        painter.FillRectangle(x, y, 1, height, color);
        painter.FillRectangle(x + width - 1, y, 1, height, color);
    }

    /// <summary>
    /// A gradient with rounded corners, row by row, since the canvas has no shapes of its own beyond rectangles.
    /// </summary>
    public static void FillRoundedGradient(IPainter painter, int x, int y, int width, int height, int radius, UIColor top, UIColor bottom)
    {
        ArgumentNullException.ThrowIfNull(painter);
        if (width <= 0 || height <= 0)
            return;

        radius = Math.Clamp(radius, 0, Math.Min(width, height) / 2);
        for (var row = 0; row < height; row++)
        {
            var inset = 0;
            var fromEdge = Math.Min(row, height - 1 - row);
            if (fromEdge < radius)
            {
                var dy = radius - fromEdge - 0.5f;
                inset = (int)MathF.Round(radius - MathF.Sqrt(Math.Max(0, (radius * radius) - (dy * dy))));
            }

            var along = height == 1 ? 0 : (float)row / (height - 1);
            painter.FillRectangle(x + inset, y + row, width - (2 * inset), 1, Mix(top, bottom, along));
        }
    }

    /// <summary>A color part of the way from one color to another.</summary>
    public static UIColor Blend(UIColor from, UIColor to, float along) => Mix(from, to, Math.Clamp(along, 0, 1));

    private static UIColor Mix(UIColor from, UIColor to, float along) => new(
        (byte)MathF.Round(from.Red + ((to.Red - from.Red) * along)),
        (byte)MathF.Round(from.Green + ((to.Green - from.Green) * along)),
        (byte)MathF.Round(from.Blue + ((to.Blue - from.Blue) * along)),
        (byte)MathF.Round(from.Alpha + ((to.Alpha - from.Alpha) * along)));
}
