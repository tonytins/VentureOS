using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>
/// Drawing on a frame of 0xAARRGGBB pixels, row by row, the layout Cosmos's canvas keeps: filling, blending, and
/// copying, each cut to the frame. A painter whose drawing doesn't blend, such as the kernel's, and one that keeps
/// its own frame, such as Roundhouse's, share this, so a translucent fill lands on exactly the same values in both.
/// </summary>
public static class FrameBuffer
{
    /// <summary>
    /// Fills a rectangle with a color, laid over what's there by its alpha: an opaque color covers it, and a
    /// translucent one blends, as a shadow does.
    /// </summary>
    /// <exception cref="ArgumentException">The frame isn't <paramref name="frameWidth"/> times <paramref name="frameHeight"/>.</exception>
    public static void Fill(int[] frame, int frameWidth, int frameHeight, int x, int y, int width, int height, UIColor color)
    {
        Check(frame, frameWidth, frameHeight);
        if (width <= 0 || height <= 0 || color.Alpha == 0)
            return;

        var left = Math.Max(0, x);
        var top = Math.Max(0, y);
        var right = (int)Math.Min(frameWidth, (long)x + width);
        var bottom = (int)Math.Min(frameHeight, (long)y + height);
        if (color.Alpha == 255)
        {
            var solid = DesktopImage.Pack(255, color.Red, color.Green, color.Blue);
            for (var row = top; row < bottom; row++)
                Array.Fill(frame, solid, (row * frameWidth) + left, Math.Max(0, right - left));
            return;
        }

        var alpha = color.Alpha;
        var keep = 255 - alpha;
        for (var row = top; row < bottom; row++)
        {
            var index = (row * frameWidth) + left;
            for (var column = left; column < right; column++, index++)
            {
                var under = frame[index];
                var red = ((((under >> 16) & 0xFF) * keep) + (color.Red * alpha)) / 255;
                var green = ((((under >> 8) & 0xFF) * keep) + (color.Green * alpha)) / 255;
                var blue = (((under & 0xFF) * keep) + (color.Blue * alpha)) / 255;
                frame[index] = DesktopImage.Pack(255, red, green, blue);
            }
        }
    }

    /// <summary>Copies a block of pixels onto the frame, with no blending, leaving out what falls off it.</summary>
    /// <exception cref="ArgumentException">The frame or the block isn't the size given.</exception>
    public static void Copy(int[] frame, int frameWidth, int frameHeight, int[] pixels, int x, int y, int width, int height)
    {
        Check(frame, frameWidth, frameHeight);
        ArgumentNullException.ThrowIfNull(pixels);
        if (width <= 0 || height <= 0)
            return;
        if (pixels.Length < (long)width * height)
            throw new ArgumentException($"A {width}x{height} block needs {width * height} pixels, not {pixels.Length}.", nameof(pixels));

        var left = Math.Max(0, x);
        var right = (int)Math.Min(frameWidth, (long)x + width);
        if (right <= left)
            return;

        for (var row = Math.Max(0, y); row < Math.Min(frameHeight, (long)y + height); row++)
            Array.Copy(pixels, ((row - y) * width) + (left - x), frame, (row * frameWidth) + left, right - left);
    }

    /// <summary>Copies what's on the frame into <paramref name="into"/>; pixels off the frame read as 0.</summary>
    /// <exception cref="ArgumentException">The frame isn't the size given, or <paramref name="into"/> is too short.</exception>
    public static void Read(int[] frame, int frameWidth, int frameHeight, int[] into, int x, int y, int width, int height)
    {
        Check(frame, frameWidth, frameHeight);
        ArgumentNullException.ThrowIfNull(into);
        if (width <= 0 || height <= 0)
            return;
        if (into.Length < (long)width * height)
            throw new ArgumentException($"A {width}x{height} block needs {width * height} pixels, not {into.Length}.", nameof(into));

        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var (px, py) = (x + column, y + row);
                into[(row * width) + column] = px >= 0 && py >= 0 && px < frameWidth && py < frameHeight
                    ? frame[(py * frameWidth) + px]
                    : 0;
            }
        }
    }

    private static void Check(int[] frame, int frameWidth, int frameHeight)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentOutOfRangeException.ThrowIfNegative(frameWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(frameHeight);
        if (frame.Length < (long)frameWidth * frameHeight)
            throw new ArgumentException($"A {frameWidth}x{frameHeight} frame needs {frameWidth * frameHeight} pixels, not {frame.Length}.", nameof(frame));
    }
}
