using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>
/// The desktop's sky: a gradient in the palette's sky colors with a soft glow rising from the bottom, after early Aqua.
/// </summary>
/// <remarks>
/// Cosmos's canvas doesn't blend, and the glow is translucent, so the whole picture is worked out
/// once for each palette, as pixels, and copied to the screen on every frame.
/// </remarks>
public static class DesktopBackground
{
    /// <summary>Works out the sky for a screen in the palette in use, as 0xAARRGGBB pixels row by row.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A size isn't positive.</exception>
    public static int[] Render(int width, int height) => Render(width, height, DesktopTheme.Palette);

    /// <summary>Works out the sky for a screen in a palette, as 0xAARRGGBB pixels row by row.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A size isn't positive.</exception>
    public static int[] Render(int width, int height, DesktopPalette palette)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(palette);
        var (top, bottom) = (palette.SkyTop, palette.SkyBottom);

        var pixels = new int[width * height];
        var glowX = width * 0.5f;
        var glowY = height * 1.1f;
        var glowRadius = width * 0.7f;

        for (var y = 0; y < height; y++)
        {
            var along = height == 1 ? 0 : (float)y / (height - 1);
            var rowRed = Mix(top.Red, bottom.Red, along);
            var rowGreen = Mix(top.Green, bottom.Green, along);
            var rowBlue = Mix(top.Blue, bottom.Blue, along);

            for (var x = 0; x < width; x++)
            {
                var (red, green, blue) = (rowRed, rowGreen, rowBlue);

                // A white glow that fades out from below the bottom edge.
                var glow = Fade(Distance(x, y, glowX, glowY) / glowRadius, palette.SkyGlow, 0f);
                (red, green, blue) = Over(red, green, blue, glow);

                pixels[(y * width) + x] = Pack(red, green, blue);
            }
        }

        return pixels;
    }

    /// <summary>Packs a color as an opaque 0xAARRGGBB pixel.</summary>
    public static int Pack(float red, float green, float blue) =>
        unchecked((int)0xFF000000u) | (Byte(red) << 16) | (Byte(green) << 8) | Byte(blue);

    private static int Byte(float value) => (int)MathF.Round(Math.Clamp(value, 0, 255));

    private static float Mix(float from, float to, float amount) => from + ((to - from) * amount);

    private static float Distance(float x, float y, float centerX, float centerY)
    {
        var (dx, dy) = (x - centerX, y - centerY);
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>White's opacity at a distance from 0 (center) to 1 (edge); nothing beyond the edge.</summary>
    private static float Fade(float distance, float center, float edge) =>
        distance >= 1 ? 0 : Mix(center, edge, distance);

    /// <summary>White laid over a color at an opacity.</summary>
    private static (float, float, float) Over(float red, float green, float blue, float white) =>
        (Mix(red, 255, white), Mix(green, 255, white), Mix(blue, 255, white));
}
