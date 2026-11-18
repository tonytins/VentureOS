using System.Runtime.InteropServices;
using SkiaSharp;
using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.Roundhouse.Drawing;

/// <summary>
/// Draws VentureOS's desktop code into a frame of VentureOS's own 0xAARRGGBB pixels, as the kernel's painter draws on
/// Cosmos's canvas: fills, blending, pictures, and reading back go through VentureOS's <see cref="FrameBuffer"/> and
/// <see cref="DesktopImage"/>, so they land on the same values, and only text is drawn by Skia, from the same
/// Open Sans and Fira Code files.
/// </summary>
/// <remarks>
/// The frame is pinned for Skia to draw text into, so dispose of the painter when done with it. Skia measures text
/// itself, so a text's width can differ from VentureOS's by a pixel.
/// </remarks>
public sealed class SkiaPainter : IPainter, IDisposable
{
    private readonly GCHandle pin;
    private readonly SKBitmap bitmap;
    private readonly SKCanvas canvas;
    private readonly SKPaint paint = new() { IsAntialias = true };
    private readonly SKFont interfaceFont;
    private readonly SKFont monospaceFont;

    /// <summary>Makes a frame of this size, filled with the panel's color, with VentureOS's fonts.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A side isn't one a picture can have.</exception>
    public SkiaPainter(int width, int height, SkiaFonts fonts)
    {
        DesktopImage.CheckSize(width, height);
        ArgumentNullException.ThrowIfNull(fonts);
        Width = width;
        Height = height;
        Pixels = new int[width * height];
        pin = GCHandle.Alloc(Pixels, GCHandleType.Pinned);

        // Little-endian BGRA is VentureOS's 0xAARRGGBB, and the frame is always opaque, so premultiplied or not.
        bitmap = new SKBitmap();
        bitmap.InstallPixels(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul), pin.AddrOfPinnedObject(), width * 4);
        canvas = new SKCanvas(bitmap);
        interfaceFont = new SKFont(fonts.Interface) { Subpixel = true, Edging = SKFontEdging.Antialias };
        monospaceFont = new SKFont(fonts.Monospace) { Subpixel = true, Edging = SKFontEdging.Antialias };
        FillRectangle(0, 0, width, height, DesktopTheme.Panel);
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>The frame, 0xAARRGGBB row by row, as drawn so far.</summary>
    public int[] Pixels { get; }

    public void DrawPixels(int[] pixels, int x, int y, int width, int height) =>
        FrameBuffer.Copy(Pixels, Width, Height, pixels, x, y, width, height);

    public void DrawImage(DesktopImage image, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(image);
        image.DrawOnto(Pixels, Width, x, y);
    }

    public void ReadPixels(int[] into, int x, int y, int width, int height) =>
        FrameBuffer.Read(Pixels, Width, Height, into, x, y, width, height);

    public void FillRectangle(int x, int y, int width, int height, UIColor color) =>
        FrameBuffer.Fill(Pixels, Width, Height, x, y, width, height, color);

    /// <summary>Draws one line with its top at <paramref name="y"/>, as VentureOS's canvas does.</summary>
    public void DrawText(string text, int x, int y, UIColor color, int size, DesktopFont font = DesktopFont.Interface)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
            return;

        var chosen = Font(font, size);
        paint.Color = new SKColor(color.Red, color.Green, color.Blue, color.Alpha);
        canvas.DrawText(text, x, y - chosen.Metrics.Ascent, SKTextAlign.Left, chosen, paint);
        canvas.Flush();
    }

    public int MeasureText(string text, int size, DesktopFont font = DesktopFont.Interface)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length == 0 ? 0 : (int)MathF.Ceiling(Font(font, size).MeasureText(text));
    }

    /// <summary>The frame as a PNG file's bytes.</summary>
    public byte[] EncodePng()
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public void Dispose()
    {
        canvas.Dispose();
        bitmap.Dispose();
        paint.Dispose();
        interfaceFont.Dispose();
        monospaceFont.Dispose();
        if (pin.IsAllocated)
            pin.Free();
    }

    private SKFont Font(DesktopFont font, int size)
    {
        var chosen = font == DesktopFont.Monospace ? monospaceFont : interfaceFont;
        chosen.Size = size;
        return chosen;
    }
}
