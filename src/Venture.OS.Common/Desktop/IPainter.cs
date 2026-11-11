using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>Which of the desktop's fonts to draw text in.</summary>
public enum DesktopFont
{
    /// <summary>Open Sans: menus, titles, and controls.</summary>
    Interface,

    /// <summary>Fira Code: Terminal and anything that lines up in columns.</summary>
    Monospace,
}

/// <summary>
/// What the desktop draws with. The kernel draws on Cosmos's canvas; tests record the calls.
/// </summary>
/// <remarks>Coordinates are in pixels from the screen's top left. Anything outside the screen is clipped.</remarks>
public interface IPainter
{
    /// <summary>The screen's width in pixels.</summary>
    int Width { get; }

    /// <summary>The screen's height in pixels.</summary>
    int Height { get; }

    /// <summary>Copies a block of pixels, as 0xAARRGGBB values row by row, with no blending.</summary>
    void DrawPixels(int[] pixels, int x, int y, int width, int height);

    /// <summary>Fills a rectangle with a solid color.</summary>
    void FillRectangle(int x, int y, int width, int height, UIColor color);

    /// <summary>Draws one line of text with its top at <paramref name="y"/>.</summary>
    void DrawText(string text, int x, int y, UIColor color, int size, DesktopFont font = DesktopFont.Interface);

    /// <summary>How wide <see cref="DrawText"/> draws <paramref name="text"/>, in pixels.</summary>
    int MeasureText(string text, int size, DesktopFont font = DesktopFont.Interface);
}
