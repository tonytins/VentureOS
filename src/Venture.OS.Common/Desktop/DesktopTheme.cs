using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>
/// The desktop's look: colors and sizes after early Aqua (Mac OS X Tiger), by way of Distro Tycoon 2's appearance.
/// </summary>
/// <remarks>
/// <para>The colors come from the <see cref="Palette"/> in use, so switching it, such as to dark, reaches every
/// window and app on the next frame with no change to them. Everything that draws reads its colors here each time
/// it draws, rather than keeping them.</para>
/// <para>A document's <c>&lt;Style&gt;</c> wins over these for the controls that name it.</para>
/// </remarks>
public static class DesktopTheme
{
    /// <summary>The palette the desktop draws with: the standard light one until something switches it.</summary>
    public static DesktopPalette Palette { get; private set; } = DesktopPalette.Light;

    /// <summary>
    /// Switches the palette. The desktop notices on its next frame and draws everything again, its sky included.
    /// </summary>
    public static void Use(DesktopPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        Palette = palette;
    }

    /// <summary>The accent, a clear sky blue, for focus and selection.</summary>
    public static UIColor Accent => Palette.Accent;

    /// <summary>Text on light backgrounds.</summary>
    public static UIColor Text => Palette.Text;

    /// <summary>Placeholders and other quieter text.</summary>
    public static UIColor SecondaryText => Palette.SecondaryText;

    /// <summary>Text on a control that's turned off.</summary>
    public static UIColor DisabledText => Palette.DisabledText;

    /// <summary>A window's content background.</summary>
    public static UIColor Panel => Palette.Panel;

    /// <summary>Lines around windows and controls.</summary>
    public static UIColor Border => Palette.Border;

    /// <summary>The inside of text fields and lists.</summary>
    public static UIColor InputBackground => Palette.InputBackground;

    /// <summary>A selected list item.</summary>
    public static UIColor Selection => Palette.Selection;

    /// <summary>A window's sidebar, a shade cooler than its content, from the top of the window to the bottom.</summary>
    public static UIColor SidebarBackground => Palette.SidebarBackground;

    /// <summary>The selected place in a sidebar, softer than a list's selection.</summary>
    public static UIColor SidebarSelection => Palette.SidebarSelection;

    /// <summary>The place in a sidebar under the pointer, fainter than the selected one.</summary>
    public static UIColor SidebarHover => Palette.SidebarHover;

    /// <summary>The bottom of a button or toolbar item under the pointer, lighter than its usual gradient.</summary>
    public static UIColor ButtonHover => Palette.ButtonHover;

    /// <summary>The top and bottom of a title bar, and of a button.</summary>
    public static UIColor TitleBarTop => Palette.TitleBarTop;

    /// <inheritdoc cref="TitleBarTop"/>
    public static UIColor TitleBarBottom => Palette.TitleBarBottom;

    /// <summary>The close button, and the mark on it.</summary>
    public static UIColor CloseButton => Palette.CloseButton;

    /// <inheritdoc cref="CloseButton"/>
    public static UIColor CloseMark => Palette.CloseMark;

    /// <summary>The minimize button's fill.</summary>
    public static UIColor MinimizeButton => Palette.MinimizeButton;

    /// <summary>The minimize button's rim.</summary>
    public static UIColor MinimizeMark => Palette.MinimizeMark;

    /// <summary>A button while the pointer is down on it.</summary>
    public static UIColor ButtonPressed => Palette.ButtonPressed;

    /// <summary>The red outline where an image couldn't be drawn, as Hudini draws it.</summary>
    public static UIColor MissingImage => Palette.MissingImage;

    /// <summary>The drop shadow under windows.</summary>
    public static UIColor Shadow => Palette.Shadow;

    /// <summary>White laid over a window that isn't in front, as early Aqua faded background windows.</summary>
    public static UIColor InactiveFade => Palette.InactiveFade;

    /// <summary>The bottom of the menu bar's gradient, which starts from <see cref="TitleBarTop"/>.</summary>
    public static UIColor MenuBarBottom => Palette.MenuBarBottom;

    /// <summary>A menu's background.</summary>
    public static UIColor MenuBackground => Palette.MenuBackground;

    /// <summary>Text on the accent color, such as a highlighted menu item.</summary>
    public static UIColor HighlightedText => Palette.HighlightedText;

    /// <summary>The line between groups of menu items.</summary>
    public static UIColor Separator => Palette.Separator;

    /// <summary>The dock's glassy shelf, laid over the sky.</summary>
    public static UIColor DockShelf => Palette.DockShelf;

    /// <summary>The dock shelf's bright edge.</summary>
    public static UIColor DockEdge => Palette.DockEdge;

    /// <summary>The dot under an open app's icon.</summary>
    public static UIColor OpenAppDot => Palette.OpenAppDot;

    /// <summary>The label that names the icon under the pointer.</summary>
    public static UIColor DockLabel => Palette.DockLabel;

    /// <summary>The shade over an icon while it's pressed.</summary>
    public static UIColor IconPressed => Palette.IconPressed;

    /// <summary>The height of a window's title bar, in pixels.</summary>
    public const int TitleBarHeight = 28;

    /// <summary>The size of title and body text, in pixels.</summary>
    public const int TextSize = 13;

    /// <summary>The sizes of the <c>caption</c>, <c>title</c>, and <c>largeTitle</c> roles; <c>body</c> is <see cref="TextSize"/>.</summary>
    public const int CaptionSize = 11;

    /// <inheritdoc cref="CaptionSize"/>
    public const int TitleSize = 17;

    /// <inheritdoc cref="CaptionSize"/>
    public const int LargeTitleSize = 22;

    /// <summary>The color a style's role stands for in the palette in use.</summary>
    public static UIColor Resolve(UIColorRole role) => role switch
    {
        UIColorRole.Text => Text,
        UIColorRole.SecondaryText => SecondaryText,
        UIColorRole.DisabledText => DisabledText,
        UIColorRole.HighlightedText => HighlightedText,
        UIColorRole.Accent => Accent,
        UIColorRole.Panel => Panel,
        UIColorRole.InputBackground => InputBackground,
        UIColorRole.Border => Border,
        UIColorRole.Selection => Selection,
        _ => Text,
    };

    /// <summary>The points a style's size role stands for.</summary>
    public static int Resolve(UIFontSizeRole role) => role switch
    {
        UIFontSizeRole.Caption => CaptionSize,
        UIFontSizeRole.Title => TitleSize,
        UIFontSizeRole.LargeTitle => LargeTitleSize,
        _ => TextSize,
    };

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
