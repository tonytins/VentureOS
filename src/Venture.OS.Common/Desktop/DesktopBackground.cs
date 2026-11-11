using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>
/// The desktop's sky: a blue gradient, a soft glow rising from the bottom, and a few glassy bubbles, after Distro
/// Tycoon 2's desktop and early Aqua.
/// </summary>
/// <remarks>
/// Cosmos's canvas doesn't blend, and the glow and bubbles are translucent, so the whole picture is worked out
/// once, as pixels, and copied to the screen on every frame.
/// </remarks>
public static class DesktopBackground
{
    /// <summary>The top of the sky, a deep blue.</summary>
    public static readonly UIColor Top = new(32, 86, 140);

    /// <summary>The bottom of the sky, a pale blue.</summary>
    public static readonly UIColor Bottom = new(180, 211, 242);

    // Where each bubble sits and how big it is, as fractions of the screen's width and height.
    private static readonly (float X, float Y, float Radius)[] Bubbles =
    [
        (0.08f, 0.78f, 0.07f), (0.17f, 0.90f, 0.035f), (0.30f, 0.70f, 0.02f),
        (0.72f, 0.84f, 0.05f), (0.84f, 0.66f, 0.09f), (0.93f, 0.90f, 0.03f),
    ];

    /// <summary>Works out the sky for a screen, as 0xAARRGGBB pixels row by row.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A size isn't positive.</exception>
    public static int[] Render(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var pixels = new int[width * height];
        var glowX = width * 0.5f;
        var glowY = height * 1.1f;
        var glowRadius = width * 0.7f;

        for (var y = 0; y < height; y++)
        {
            var along = height == 1 ? 0 : (float)y / (height - 1);
            var rowRed = Mix(Top.Red, Bottom.Red, along);
            var rowGreen = Mix(Top.Green, Bottom.Green, along);
            var rowBlue = Mix(Top.Blue, Bottom.Blue, along);

            for (var x = 0; x < width; x++)
            {
                var (red, green, blue) = (rowRed, rowGreen, rowBlue);

                // A white glow that fades out from below the bottom edge.
                var glow = Fade(Distance(x, y, glowX, glowY) / glowRadius, 0.55f, 0f);
                (red, green, blue) = Over(red, green, blue, glow);

                foreach (var (bubbleX, bubbleY, bubbleRadius) in Bubbles)
                {
                    var centerX = width * bubbleX;
                    var centerY = height * bubbleY;
                    var radius = width * bubbleRadius;

                    // Clear in the middle and whiter at the rim, with a small shine up and to the left.
                    var body = Distance(x, y, centerX, centerY) / radius;
                    if (body <= 1)
                        (red, green, blue) = Over(red, green, blue, Fade(body, 0.04f, 0.28f));

                    var shine = Distance(x, y, centerX - (radius * 0.35f), centerY - (radius * 0.4f)) / (radius * 0.4f);
                    if (shine <= 1)
                        (red, green, blue) = Over(red, green, blue, Fade(shine, 0.6f, 0f));
                }

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
