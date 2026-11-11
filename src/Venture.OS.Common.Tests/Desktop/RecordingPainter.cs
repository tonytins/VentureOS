using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// Paints into an array of pixels, as the screen would, and keeps the text it was asked to draw.
/// </summary>
/// <remarks>Text measures 8 pixels per character, so layouts are predictable in tests.</remarks>
internal sealed class RecordingPainter(int width, int height) : IPainter
{
    public int Width { get; } = width;
    public int Height { get; } = height;

    public int[] Pixels { get; } = new int[width * height];

    public List<(string Text, int X, int Y, UIColor Color, int Size, DesktopFont Font)> Texts { get; } = [];

    public int Calls { get; private set; }

    public void DrawPixels(int[] pixels, int x, int y, int width, int height)
    {
        Calls++;
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
                Set(x + column, y + row, pixels[(row * width) + column]);
        }
    }

    public void FillRectangle(int x, int y, int width, int height, UIColor color)
    {
        Calls++;
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var (px, py) = (x + column, y + row);
                if (px < 0 || py < 0 || px >= Width || py >= Height)
                    continue;

                var under = Pixels[(py * Width) + px];
                float alpha = color.Alpha / 255f;
                Set(px, py, DesktopBackground.Pack(
                    Blend((under >> 16) & 0xFF, color.Red, alpha),
                    Blend((under >> 8) & 0xFF, color.Green, alpha),
                    Blend(under & 0xFF, color.Blue, alpha)));
            }
        }
    }

    private static float Blend(int under, byte over, float alpha) => under + ((over - under) * alpha);

    public void DrawText(string text, int x, int y, UIColor color, int size, DesktopFont font = DesktopFont.Interface)
    {
        Calls++;
        Texts.Add((text, x, y, color, size, font));
    }

    public int MeasureText(string text, int size, DesktopFont font = DesktopFont.Interface) => text.Length * 8;

    public int At(int x, int y) => Pixels[(y * Width) + x];

    private void Set(int x, int y, int pixel)
    {
        if (x >= 0 && y >= 0 && x < Width && y < Height)
            Pixels[(y * Width) + x] = pixel;
    }
}
