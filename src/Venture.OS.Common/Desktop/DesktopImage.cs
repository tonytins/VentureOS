using System.Buffers.Binary;

namespace Venture.OS.Desktop;

/// <summary>
/// A picture the desktop draws, such as an app's icon or a wallpaper: its size and its pixels, as 0xAARRGGBB row by
/// row, the layout Cosmos's canvas uses.
/// </summary>
/// <remarks>
/// <para>Decoding a file is the host's job, since the kernel's PNG decoder lives in Cosmos; everything after that,
/// scaling and laying one picture over another, is here, where it can be tested. Scaling allocates and is slow on
/// Cosmos, so callers scale once, to the size the picture is drawn at, and keep the result.</para>
/// <para>The pixels are the image's own, not a copy, so it can be drawn without allocating. Don't change them.</para>
/// </remarks>
public sealed class DesktopImage
{
    /// <summary>The widest or tallest picture the desktop takes, in pixels.</summary>
    public const int MaxSide = 4096;

    /// <summary>The most pixels a picture may have: 8 megapixels, 32 MB once decoded.</summary>
    public const int MaxPixels = 8 * 1024 * 1024;

    private static ReadOnlySpan<byte> PngSignature => [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Makes a picture from its pixels.</summary>
    /// <param name="width">How wide it is, from 1 to <see cref="MaxSide"/>.</param>
    /// <param name="height">How tall it is, from 1 to <see cref="MaxSide"/>.</param>
    /// <param name="pixels">Its pixels, 0xAARRGGBB row by row, exactly <paramref name="width"/> times
    /// <paramref name="height"/> of them. The picture keeps the array.</param>
    /// <exception cref="ArgumentOutOfRangeException">A side is out of range, or there are too many pixels.</exception>
    /// <exception cref="ArgumentException">The number of pixels doesn't match the size.</exception>
    public DesktopImage(int width, int height, int[] pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        CheckSize(width, height);
        if (pixels.Length != width * height)
            throw new ArgumentException($"A {width}x{height} picture has {width * height} pixels, not {pixels.Length}.", nameof(pixels));

        Width = width;
        Height = height;
        Pixels = pixels;
        IsOpaque = true;
        foreach (var pixel in pixels)
        {
            if (Alpha(pixel) != 255)
            {
                IsOpaque = false;
                break;
            }
        }
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>The pixels, 0xAARRGGBB row by row. Don't change them.</summary>
    public int[] Pixels { get; }

    /// <summary>Whether every pixel is fully opaque, so it can be copied to the screen without blending.</summary>
    public bool IsOpaque { get; }

    /// <summary>Throws when a picture of this size isn't one the desktop takes.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A side is out of range, or there are too many pixels.</exception>
    public static void CheckSize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, MaxSide);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(height, MaxSide);
        if ((long)width * height > MaxPixels)
            throw new ArgumentOutOfRangeException(nameof(height), $"A {width}x{height} picture has more than {MaxPixels} pixels.");
    }

    /// <summary>
    /// Reads a PNG file's size from its header without decoding it, so a picture too big for the desktop is turned
    /// away before the decoder allocates for it.
    /// </summary>
    /// <returns>Its size, or <see langword="null"/> with <paramref name="problem"/> saying why it can't be used.</returns>
    public static (int Width, int Height)? ReadPngSize(ReadOnlySpan<byte> file, out string? problem)
    {
        // The signature, then the IHDR chunk: its length (13), its type, then the width and height.
        const int HeaderEnd = 8 + 4 + 4 + 8;
        if (file.Length < HeaderEnd || !file[..8].SequenceEqual(PngSignature))
        {
            problem = "it isn't a PNG file";
            return null;
        }

        if (!file.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            problem = "its header is missing";
            return null;
        }

        var width = BinaryPrimitives.ReadUInt32BigEndian(file.Slice(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(file.Slice(20, 4));
        if (width == 0 || height == 0 || width > MaxSide || height > MaxSide || (long)width * height > MaxPixels)
        {
            problem = $"it's {width}x{height}; the desktop takes pictures up to {MaxSide} pixels a side and {MaxPixels} pixels in all";
            return null;
        }

        problem = null;
        return ((int)width, (int)height);
    }

    /// <summary>A copy at another size, each new pixel the average of the ones it covers.</summary>
    /// <remarks>
    /// Averaging keeps a shrunken icon's edges smooth, where picking pixels would leave them jagged. Colors are
    /// weighted by their opacity, so a clear pixel's color doesn't bleed into the edge of the shape beside it.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">A side is out of range, or there are too many pixels.</exception>
    public DesktopImage Scaled(int width, int height)
    {
        CheckSize(width, height);
        if (width == Width && height == Height)
            return this;

        var pixels = new int[width * height];
        for (var y = 0; y < height; y++)
        {
            var (top, bottom) = Span(y, height, Height);
            for (var x = 0; x < width; x++)
            {
                var (left, right) = Span(x, width, Width);
                long alpha = 0, red = 0, green = 0, blue = 0;
                for (var row = top; row < bottom; row++)
                {
                    var index = (row * Width) + left;
                    for (var column = left; column < right; column++, index++)
                    {
                        var pixel = Pixels[index];
                        var a = Alpha(pixel);
                        alpha += a;
                        red += ((pixel >> 16) & 0xFF) * a;
                        green += ((pixel >> 8) & 0xFF) * a;
                        blue += (pixel & 0xFF) * a;
                    }
                }

                var count = (long)(bottom - top) * (right - left);
                pixels[(y * width) + x] = alpha == 0
                    ? 0
                    : Pack((int)((alpha + (count / 2)) / count), (int)(red / alpha), (int)(green / alpha), (int)(blue / alpha));
            }
        }

        return new DesktopImage(width, height, pixels);
    }

    /// <summary>
    /// A copy that fills a screen of this size without stretching: scaled until it covers the screen, then cut to it
    /// from the middle, as a wallpaper is.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A side is out of range, or there are too many pixels.</exception>
    public DesktopImage Covering(int width, int height)
    {
        CheckSize(width, height);
        if (width == Width && height == Height)
            return this;

        // The larger of the two scales covers both sides; the other side then overhangs and is cut evenly.
        var scale = Math.Max((double)width / Width, (double)height / Height);
        var scaledWidth = Math.Clamp((int)Math.Ceiling(Width * scale), width, MaxSide);
        var scaledHeight = Math.Clamp((int)Math.Ceiling(Height * scale), height, MaxSide);
        if ((long)scaledWidth * scaledHeight > MaxPixels)
            return Scaled(width, height); // Too big to scale whole, so stretch rather than fail.

        var scaled = Scaled(scaledWidth, scaledHeight);
        var (offsetX, offsetY) = ((scaledWidth - width) / 2, (scaledHeight - height) / 2);
        var pixels = new int[width * height];
        for (var y = 0; y < height; y++)
            Array.Copy(scaled.Pixels, ((y + offsetY) * scaledWidth) + offsetX, pixels, y * width, width);
        return new DesktopImage(width, height, pixels);
    }

    /// <summary>
    /// The picture's shape in one color, as a symbol: its dark parts take the color and its light parts are left
    /// clear, so a white windshield on a blue train reads as a hole in it, not a solid block.
    /// </summary>
    public DesktopImage Silhouette(byte red, byte green, byte blue)
    {
        var pixels = new int[Pixels.Length];
        for (var index = 0; index < pixels.Length; index++)
        {
            var pixel = Pixels[index];

            // Perceived lightness from 0 to 1; fully dark to 0.45, fading to clear at 0.75.
            var light = ((0.2126f * ((pixel >> 16) & 0xFF)) + (0.7152f * ((pixel >> 8) & 0xFF)) + (0.0722f * (pixel & 0xFF))) / 255f;
            var ink = Math.Clamp((0.75f - light) / 0.3f, 0f, 1f);
            var alpha = (int)MathF.Round(Alpha(pixel) * ink);
            pixels[index] = alpha == 0 ? 0 : Pack(alpha, red, green, blue);
        }

        return new DesktopImage(Width, Height, pixels);
    }

    /// <summary>
    /// Lays the picture over a frame of 0xAARRGGBB pixels with its corner at (<paramref name="x"/>,
    /// <paramref name="y"/>), blending by each pixel's opacity and leaving out whatever falls off the frame.
    /// </summary>
    /// <param name="frame">The frame's pixels, row by row.</param>
    /// <param name="frameWidth">How wide the frame is; its height follows from the length.</param>
    /// <exception cref="ArgumentOutOfRangeException">The width doesn't fit the frame.</exception>
    public void DrawOnto(int[] frame, int frameWidth, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameWidth);
        if (frame.Length % frameWidth != 0)
            throw new ArgumentOutOfRangeException(nameof(frameWidth), $"A frame of {frame.Length} pixels can't be {frameWidth} wide.");

        var frameHeight = frame.Length / frameWidth;
        var left = Math.Max(0, x);
        var top = Math.Max(0, y);
        var right = (int)Math.Min(frameWidth, (long)x + Width);
        var bottom = (int)Math.Min(frameHeight, (long)y + Height);

        for (var row = top; row < bottom; row++)
        {
            var source = ((row - y) * Width) + (left - x);
            var target = (row * frameWidth) + left;
            for (var column = left; column < right; column++, source++, target++)
            {
                var pixel = Pixels[source];
                var alpha = Alpha(pixel);
                if (alpha == 255)
                {
                    frame[target] = pixel;
                }
                else if (alpha != 0)
                {
                    var under = frame[target];
                    var keep = 255 - alpha;
                    var red = ((((under >> 16) & 0xFF) * keep) + (((pixel >> 16) & 0xFF) * alpha)) / 255;
                    var green = ((((under >> 8) & 0xFF) * keep) + (((pixel >> 8) & 0xFF) * alpha)) / 255;
                    var blue = (((under & 0xFF) * keep) + ((pixel & 0xFF) * alpha)) / 255;
                    frame[target] = Pack(255, red, green, blue);
                }
            }
        }
    }

    /// <summary>Packs a pixel as 0xAARRGGBB.</summary>
    public static int Pack(int alpha, int red, int green, int blue) =>
        unchecked((alpha << 24) | (red << 16) | (green << 8) | blue);

    private static int Alpha(int pixel) => (pixel >> 24) & 0xFF;

    /// <summary>The source rows or columns a target one covers, always at least one.</summary>
    private static (int Start, int End) Span(int index, int target, int source)
    {
        var start = (int)((long)index * source / target);
        var end = (int)(((long)(index + 1) * source) + target - 1) / target;
        return (start, Math.Max(end, start + 1));
    }
}
