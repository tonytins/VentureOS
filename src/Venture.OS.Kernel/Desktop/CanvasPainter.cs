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

    public void ReadPixels(int[] into, int x, int y, int width, int height)
    {
        if (canvas.GetBuffer() is { } buffer)
            FrameBuffer.Read(buffer, canvas.Width, canvas.Height, into, x, y, width, height);
        else
            Array.Clear(into, 0, Math.Min(into.Length, Math.Max(0, width) * Math.Max(0, height)));
    }

    public void DrawImage(DesktopImage image, int x, int y)
    {
        // An opaque picture is copied, which the canvas clips; anything else is blended into the frame by hand.
        if (image.IsOpaque)
        {
            canvas.DrawArray(image.Pixels, x, y, image.Width, image.Height);
            return;
        }

        if (canvas.GetBuffer() is { } buffer)
            image.DrawOnto(buffer, canvas.Width, x, y);
    }

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
    /// Lays a translucent color over the frame, pixel by pixel, since the canvas's own drawing doesn't blend, with
    /// the same arithmetic Roundhouse's painter uses.
    /// </summary>
    private void Blend(int x, int y, int width, int height, UIColor color)
    {
        if (canvas.GetBuffer() is { } buffer)
            FrameBuffer.Fill(buffer, canvas.Width, canvas.Height, x, y, width, height, color);
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
