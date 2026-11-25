using Venture.OS.Desktop;

namespace Venture.OS.Vdb;

/// <summary>
/// What the <c>ui</c> feature needs from a running desktop: its windows, a way to press keys in one, and the
/// screen's pixels. A session without one doesn't offer <c>ui</c>, so a VentureOS that can't show its desktop says
/// so in its <c>hello</c> rather than answering every command with an error.
/// </summary>
/// <remarks>
/// The session reaches only app windows through it: those an app opened, alerts included, and never panels or the
/// system's own windows, such as the Trust alert. That rule is the session's, so every host keeps it.
/// </remarks>
public interface IVdbDesktop
{
    /// <summary>The desktop's windows, which a window is brought to the front through, as a click would.</summary>
    WindowManager Windows { get; }

    /// <summary>Presses <paramref name="keys"/> in <paramref name="window"/>, which is in front, on the desktop's next frame.</summary>
    /// <returns>Why they can't be pressed, or <see langword="null"/> when they will be.</returns>
    string? PressKeys(DesktopWindow window, IReadOnlyList<KeyPress> keys);

    /// <summary>The screen as it was last drawn, or <see langword="null"/> when it can't be read.</summary>
    VdbScreenshot? Screenshot();
}

/// <summary>The screen's pixels, three bytes each, red, green, then blue, a row at a time from the top.</summary>
public sealed record VdbScreenshot(int Width, int Height, byte[] Rgb)
{
    /// <summary>The largest screen a screenshot is sent of, so its size can't be made to run out of memory.</summary>
    public const int MaximumPixels = 4096 * 4096;

    /// <summary>
    /// The screen from the desktop's frame, one <c>0xAARRGGBB</c> number a pixel, as <see cref="FrameBuffer"/> keeps it;
    /// alpha is dropped, since the screen has none.
    /// </summary>
    /// <returns>The screenshot, or <see langword="null"/> when the frame isn't the size given or is too large.</returns>
    public static VdbScreenshot? FromFrame(int[] frame, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (width <= 0 || height <= 0 || (long)width * height > MaximumPixels || frame.LongLength != (long)width * height)
            return null;

        var rgb = new byte[frame.Length * 3];
        for (int index = 0, at = 0; index < frame.Length; index++, at += 3)
        {
            var pixel = frame[index];
            rgb[at] = (byte)(pixel >> 16);
            rgb[at + 1] = (byte)(pixel >> 8);
            rgb[at + 2] = (byte)pixel;
        }

        return new VdbScreenshot(width, height, rgb);
    }

    /// <summary>Why the pixels don't make the picture they say, or <see langword="null"/>.</summary>
    public string? Problem() =>
        Width <= 0 || Height <= 0 || (long)Width * Height > MaximumPixels ? $"the screen's size, {Width} by {Height}, isn't one a screenshot can be"
        : Rgb.LongLength != (long)Width * Height * 3 ? $"the screen is {Width} by {Height}, but its pixels are {Rgb.Length} bytes"
        : null;
}
