using Venture.OS.Desktop;

namespace Venture.OS.Tests.Desktop;

/// <summary>App icons: full backgrounds masked to a tile, and the key color making a shape of its own.</summary>
public sealed class DesktopIconTests
{
    private const int White = unchecked((int)0xFFFFFFFF);
    private const int Blue = unchecked((int)0xFF3D8BFD);
    private const int Key = unchecked((int)0xFF000000) | DesktopIcon.ClearKey;

    [Fact]
    public void AFullBackgroundIsMaskedToARoundedTile()
    {
        var icon = DesktopIcon.From(Square(64, White), 48);

        Assert.True(icon.IsTile);
        Assert.Equal(48, icon.Image.Width);
        Assert.Equal(0, At(icon, 0, 0));
        Assert.Equal(0, At(icon, 47, 47));
        Assert.Equal(White, At(icon, 24, 24));

        // The mask follows the dock's tile outline exactly: clear up to each row's inset, opaque from it.
        var inset = DesktopTheme.RoundedInset(0, 48, 48, DesktopIcon.Radius(48));
        Assert.Equal(0, At(icon, inset - 1, 0));
        Assert.Equal(White, At(icon, inset, 0));
    }

    [Fact]
    public void TheKeyColorIsClearAndMakesAShape()
    {
        // A blue block on the key, with key all round it.
        var pixels = Enumerable.Repeat(Key, 64 * 64).ToArray();
        for (var y = 16; y < 40; y++)
        {
            for (var x = 16; x < 48; x++)
                pixels[(y * 64) + x] = Blue;
        }

        var icon = DesktopIcon.From(new DesktopImage(64, 64, pixels), 64);

        Assert.False(icon.IsTile);
        Assert.Equal(0, At(icon, 0, 0));
        Assert.Equal(Blue, At(icon, 30, 30));

        // Under the block's bottom edge is a soft black shadow, and nothing of the key's green.
        var shadow = At(icon, 30, 40);
        Assert.InRange((shadow >> 24) & 0xFF, 1, 254);
        Assert.Equal(0, shadow & 0xFFFFFF);
        Assert.DoesNotContain(icon.Image.Pixels, pixel => (pixel & 0xFFFFFF) == DesktopIcon.ClearKey && (pixel >> 24) != 0);
    }

    [Fact]
    public void OnlyTheExactKeyIsClear()
    {
        // Pure green, one step from the key, stays.
        const int Green = unchecked((int)0xFF00FF00);

        var icon = DesktopIcon.From(Square(4, Green), 4);

        Assert.True(icon.IsTile);
        Assert.Equal(Green, At(icon, 2, 2));
    }

    [Fact]
    public void AnIconWithClearCornersKeepsItsShape()
    {
        var pixels = Enumerable.Repeat(White, 16).ToArray();
        pixels[0] = 0;

        Assert.False(DesktopIcon.From(new DesktopImage(4, 4, pixels), 4).IsTile);
    }

    [Fact]
    public void TheCornerRadiusScalesWithTheSize()
    {
        Assert.Equal(10, DesktopIcon.Radius(48));
        Assert.Equal(13, DesktopIcon.Radius(64));
    }

    [Fact]
    public void TheDockGivesATileATilesShadowAndAShapeNone()
    {
        Dock dock = new(800, 600);
        var shape = Enumerable.Repeat(0, 64 * 64).ToArray();
        dock.SetItems(
        [
            new DockItem("Tile", "Tile", IsOpen: false, Icon: DesktopIcon.From(Square(64, White), Dock.IconSize)),
            new DockItem("Shape", "Shape", IsOpen: false, Icon: DesktopIcon.From(new DesktopImage(64, 64, shape), Dock.IconSize)),
        ]);
        RecordingPainter painter = new(800, 600);
        Array.Fill(painter.Pixels, White);

        dock.Draw(painter);

        // Just below each icon's bottom edge, in the middle: the tile's shadow darkens the shelf; the clear shape
        // casts none.
        var tile = dock.IconBounds(0);
        var empty = dock.IconBounds(1);
        Assert.NotEqual(painter.At((int)empty.X + 24, (int)empty.Y + 49), painter.At((int)tile.X + 24, (int)tile.Y + 49));
        Assert.Equal(2, painter.Images.Count);
    }

    private static DesktopImage Square(int size, int pixel) => new(size, size, Enumerable.Repeat(pixel, size * size).ToArray());

    private static int At(DesktopIcon icon, int x, int y) => icon.Image.Pixels[(y * icon.Image.Width) + x];
}
