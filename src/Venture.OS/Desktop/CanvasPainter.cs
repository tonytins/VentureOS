using Venture.OS.Desktop;
using Venture.OS.Interface;
using DrawingColor = System.Drawing.Color;

namespace Venture.OS.Desktop;

/// <summary>
/// Draws the desktop on Cosmos's full-screen canvas, with Open Sans for the interface and Fira Code for monospace.
/// </summary>
/// <remarks>Nothing appears until the frame loop calls <c>Canvas.Display</c>, which copies the whole frame at once.</remarks>
internal sealed class CanvasPainter(Canvas canvas, TrueTypeFont interfaceFont, TrueTypeFont monospaceFont) : IPainter
{
    public int Width => canvas.Width;

    public int Height => canvas.Height;

    public void DrawPixels(int[] pixels, int x, int y, int width, int height) =>
        canvas.DrawArray(pixels, x, y, width, height);

    public void FillRectangle(int x, int y, int width, int height, UIColor color)
    {
        if (width <= 0 || height <= 0 || color.Alpha == 0)
            return;

        if (color.Alpha == 255)
        {
            canvas.DrawFilledRectangle(ToColor(color), x, y, width, height);
            return;
        }

        Blend(x, y, width, height, color);
    }

    /// <summary>
    /// Lays a translucent color over the frame, pixel by pixel, since the canvas's own drawing doesn't blend.
    /// </summary>
    private void Blend(int x, int y, int width, int height, UIColor color)
    {
        if (canvas.GetBuffer() is not { } buffer)
            return;

        var left = Math.Max(0, x);
        var top = Math.Max(0, y);
        var right = Math.Min(canvas.Width, x + width);
        var bottom = Math.Min(canvas.Height, y + height);
        var alpha = color.Alpha;
        var keep = 255 - alpha;

        for (var row = top; row < bottom; row++)
        {
            var index = (row * canvas.Width) + left;
            for (var column = left; column < right; column++, index++)
            {
                var under = buffer[index];
                var red = ((((under >> 16) & 0xFF) * keep) + (color.Red * alpha)) / 255;
                var green = ((((under >> 8) & 0xFF) * keep) + (color.Green * alpha)) / 255;
                var blue = (((under & 0xFF) * keep) + (color.Blue * alpha)) / 255;
                buffer[index] = unchecked((int)0xFF000000) | (red << 16) | (green << 8) | blue;
            }
        }
    }

    public void DrawText(string text, int x, int y, UIColor color, int size, DesktopFont font = DesktopFont.Interface)
    {
        if (text.Length > 0)
            canvas.DrawString(text, Font(font), size, ToColor(color), x, y);
    }

    public int MeasureText(string text, int size, DesktopFont font = DesktopFont.Interface)
    {
        var chosen = Font(font);
        chosen.SizePx = size;
        return chosen.MeasureString(text);
    }

    private TrueTypeFont Font(DesktopFont font) => font == DesktopFont.Monospace ? monospaceFont : interfaceFont;

    private static DrawingColor ToColor(UIColor color) =>
        DrawingColor.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
}
