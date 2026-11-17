namespace Venture.OS.Desktop;

/// <summary>
/// An app's icon, ready to draw at one size: either a tile, rounded like the dock's other tiles, or a shape of its
/// own with a soft shadow under it.
/// </summary>
/// <remarks>
/// <para>Icons are drawn with full backgrounds, so, as on modern macOS, an icon whose four corners are opaque is
/// masked to a rounded tile. Pixels in exactly <see cref="ClearKey"/> are clear, which is how an icon gets a shape
/// of its own, such as a folder, from a tool without transparency; an icon whose corners are clear, by the key or
/// by a real alpha channel, keeps its shape and gets a shadow.</para>
/// <para>The key is applied before scaling, and scaling weighs pixels by opacity, so a shape's hard edge comes out
/// smooth with no fringe of the key's green.</para>
/// </remarks>
public sealed class DesktopIcon
{
    /// <summary>
    /// The color that's clear in an icon, 0xRRGGBB: pure green with one step of blue, which art is unlikely to use
    /// by chance. It applies to app icons only, never to wallpapers or other pictures.
    /// </summary>
    public const int ClearKey = 0x00FF01;

    /// <summary>A tile's corner radius at the dock's 48 pixels; other sizes scale it.</summary>
    private const int RadiusAt48 = 10;

    // The shadow under a shaped icon: how far down it falls and how dark it is at most.
    private const int ShadowDrop = 2;
    private const int ShadowAlpha = 80;

    private DesktopIcon(DesktopImage image, bool isTile)
    {
        Image = image;
        IsTile = isTile;
    }

    /// <summary>The icon at the size it was made for.</summary>
    public DesktopImage Image { get; }

    /// <summary>Whether it's masked to a rounded tile, which the dock gives a tile's shadow.</summary>
    public bool IsTile { get; }

    /// <summary>A tile's corner radius at a size, matching the dock's tiles at 48 pixels.</summary>
    public static int Radius(int size) => (int)MathF.Round(size * RadiusAt48 / 48f);

    /// <summary>Makes an app's icon at a size from the picture its bundle names.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The size isn't one a picture can have.</exception>
    public static DesktopIcon From(DesktopImage picture, int size)
    {
        ArgumentNullException.ThrowIfNull(picture);
        DesktopImage.CheckSize(size, size);

        var keyed = ClearTheKey(picture);
        var isTile = CornersOpaque(keyed);
        var scaled = keyed.Scaled(size, size);
        return new DesktopIcon(isTile ? MaskToTile(scaled) : WithShadow(scaled), isTile);
    }

    private static DesktopImage ClearTheKey(DesktopImage picture)
    {
        var key = unchecked((int)0xFF000000) | ClearKey;
        if (Array.IndexOf(picture.Pixels, key) < 0)
            return picture;

        var pixels = (int[])picture.Pixels.Clone();
        for (var index = 0; index < pixels.Length; index++)
        {
            if (pixels[index] == key)
                pixels[index] = 0;
        }

        return new DesktopImage(picture.Width, picture.Height, pixels);
    }

    private static bool CornersOpaque(DesktopImage picture)
    {
        var (right, bottom) = (picture.Width - 1, picture.Height - 1);
        return Opaque(picture, 0, 0) && Opaque(picture, right, 0) && Opaque(picture, 0, bottom) && Opaque(picture, right, bottom);
    }

    private static bool Opaque(DesktopImage picture, int x, int y) =>
        ((picture.Pixels[(y * picture.Width) + x] >> 24) & 0xFF) == 255;

    /// <summary>Clears what falls outside a rounded tile the dock's shape.</summary>
    private static DesktopImage MaskToTile(DesktopImage icon)
    {
        var pixels = (int[])icon.Pixels.Clone();
        var radius = Radius(icon.Width);
        for (var row = 0; row < icon.Height; row++)
        {
            var inset = DesktopTheme.RoundedInset(row, icon.Width, icon.Height, radius);
            for (var column = 0; column < inset; column++)
            {
                pixels[(row * icon.Width) + column] = 0;
                pixels[(row * icon.Width) + icon.Width - 1 - column] = 0;
            }
        }

        return new DesktopImage(icon.Width, icon.Height, pixels);
    }

    /// <summary>
    /// Lays the icon over a soft shadow of its own shape, a little lower, worked out once so drawing it costs
    /// nothing more. The shadow stays inside the icon's square, so it's cut where the shape reaches the bottom.
    /// </summary>
    private static DesktopImage WithShadow(DesktopImage icon)
    {
        var (width, height) = (icon.Width, icon.Height);
        var shadow = new int[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // The opacity of the shape above, softened over a 3x3 square.
                var sum = 0;
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var (sx, sy) = (x + dx, y + dy - ShadowDrop);
                        if (sx >= 0 && sy >= 0 && sx < width && sy < height)
                            sum += (icon.Pixels[(sy * width) + sx] >> 24) & 0xFF;
                    }
                }

                shadow[(y * width) + x] = DesktopImage.Pack(sum * ShadowAlpha / (9 * 255), 0, 0, 0);
            }
        }

        // The icon over its shadow; where both are partly clear, the result is too.
        var pixels = new int[width * height];
        for (var index = 0; index < pixels.Length; index++)
            pixels[index] = Over(icon.Pixels[index], shadow[index]);
        return new DesktopImage(width, height, pixels);
    }

    /// <summary>One pixel over another, both possibly translucent.</summary>
    private static int Over(int top, int under)
    {
        var topAlpha = (top >> 24) & 0xFF;
        var underAlpha = (under >> 24) & 0xFF;
        if (topAlpha == 255 || underAlpha == 0)
            return top;
        if (topAlpha == 0)
            return under;

        var alpha = topAlpha + (underAlpha * (255 - topAlpha) / 255);
        int Channel(int shift)
        {
            var over = (top >> shift) & 0xFF;
            var below = (under >> shift) & 0xFF;
            return ((over * topAlpha) + (below * underAlpha * (255 - topAlpha) / 255)) / alpha;
        }

        return DesktopImage.Pack(alpha, Channel(16), Channel(8), Channel(0));
    }
}
