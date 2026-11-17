using Venture.OS.Desktop;

namespace Venture.OS.Tests.Desktop;

/// <summary>Pictures: checking their size, scaling them, and laying them over a frame.</summary>
public sealed class DesktopImageTests
{
    private const int Red = unchecked((int)0xFFFF0000);
    private const int Blue = unchecked((int)0xFF0000FF);
    private const int White = unchecked((int)0xFFFFFFFF);
    private const int Clear = 0;

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, -1)]
    [InlineData(DesktopImage.MaxSide + 1, 1)]
    [InlineData(4096, 4096)]
    public void TurnsAwaySizesOutOfRange(int width, int height) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => DesktopImage.CheckSize(width, height));

    [Fact]
    public void NeedsAsManyPixelsAsItsSize()
    {
        var problem = Assert.Throws<ArgumentException>(() => new DesktopImage(2, 2, [Red, Red, Red]));

        Assert.Contains("4 pixels, not 3", problem.Message);
    }

    [Fact]
    public void KnowsWhetherItsOpaque()
    {
        Assert.True(new DesktopImage(2, 1, [Red, Blue]).IsOpaque);
        Assert.False(new DesktopImage(2, 1, [Red, Clear]).IsOpaque);
    }

    [Fact]
    public void ShrinkingAveragesThePixelsEachCovers()
    {
        // Two red and two blue columns become one of each.
        DesktopImage image = new(4, 2, [Red, Red, Blue, Blue, Red, Red, Blue, Blue]);

        var half = image.Scaled(2, 1);

        Assert.Equal([Red, Blue], half.Pixels);
        Assert.Equal(DesktopImage.Pack(255, 127, 0, 127), image.Scaled(1, 1).Pixels[0]);
    }

    [Fact]
    public void ShrinkingDoesntBleedClearPixelsColor()
    {
        // A clear pixel that happens to be stored as black doesn't darken the red beside it.
        DesktopImage image = new(2, 1, [Red, Clear]);

        var pixel = image.Scaled(1, 1).Pixels[0];

        Assert.Equal(DesktopImage.Pack(128, 255, 0, 0), pixel);
    }

    [Fact]
    public void GrowingRepeatsPixels()
    {
        DesktopImage image = new(2, 1, [Red, Blue]);

        Assert.Equal([Red, Red, Blue, Blue], image.Scaled(4, 1).Pixels);
    }

    [Fact]
    public void ScalingToItsOwnSizeKeepsIt()
    {
        DesktopImage image = new(1, 1, [Red]);

        Assert.Same(image, image.Scaled(1, 1));
        Assert.Same(image, image.Covering(1, 1));
    }

    [Fact]
    public void CoveringCutsTheOverhangFromTheMiddle()
    {
        // A 4x1 strip covering a 2x1 screen keeps its middle two pixels.
        DesktopImage image = new(4, 1, [Red, Blue, White, Red]);

        Assert.Equal([Blue, White], image.Covering(2, 1).Pixels);

        // Covering a taller screen scales up until the height fits, then cuts the width.
        Assert.Equal([Blue, White, Blue, White], image.Covering(2, 2).Pixels);
    }

    [Fact]
    public void DrawingBlendsByOpacityAndClipsToTheFrame()
    {
        int[] frame = [White, White, White, White];
        DesktopImage image = new(2, 1, [Red, DesktopImage.Pack(128, 0, 0, 255)]);

        image.DrawOnto(frame, 2, 1, 1);

        // The red pixel lands at (1, 1); the half-clear blue one falls off the right edge.
        Assert.Equal([White, White, White, Red], frame);

        image.DrawOnto(frame, 2, -1, 0);
        Assert.Equal(DesktopImage.Pack(255, 127, 127, 255), frame[0]);
        Assert.Equal(White, frame[1]);
    }

    [Fact]
    public void DrawingNeedsAFrameItsWidthDivides() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new DesktopImage(1, 1, [Red]).DrawOnto(new int[5], 2, 0, 0));

    [Fact]
    public void ReadsAPngsSizeFromItsHeader()
    {
        var size = DesktopImage.ReadPngSize(Png(64, 32), out var problem);

        Assert.Equal((64, 32), size);
        Assert.Null(problem);
    }

    [Fact]
    public void TurnsAwayWhatIsntAPngOrIsTooBig()
    {
        Assert.Null(DesktopImage.ReadPngSize("GIF89a, not a PNG at all"u8, out var notPng));
        Assert.Equal("it isn't a PNG file", notPng);

        Assert.Null(DesktopImage.ReadPngSize(Png(5000, 10), out var tooWide));
        Assert.Contains("5000x10", tooWide);

        Assert.Null(DesktopImage.ReadPngSize(Png(0, 10), out _));
        Assert.Null(DesktopImage.ReadPngSize(Png(10, 10).AsSpan(0, 20), out _));
    }

    [Fact]
    public void ReadsTheShippedIconsAndWallpaperHeaders()
    {
        var system = Path.Combine(AppContext.BaseDirectory, "system");
        var user = Path.Combine(AppContext.BaseDirectory, "user");
        string[] icons =
        [
            Path.Combine(system, "apps", "Files.app", "Contents", "Resources", "AppIcon.png"),
            Path.Combine(system, "apps", "Terminal.app", "Contents", "Resources", "AppIcon.png"),
            Path.Combine(user, "apps", "Notes.app", "Contents", "Resources", "AppIcon.png"),
            Path.Combine(user, "apps", "TextEdit.app", "Contents", "Resources", "AppIcon.png"),
        ];

        foreach (var icon in icons)
            Assert.Equal((64, 64), DesktopImage.ReadPngSize(File.ReadAllBytes(icon), out _));

        // Pre-scaled to the screen QEMU gives, so the desktop needn't scale it at boot.
        var wallpaper = Path.Combine(system, "wallpapers", "earthset.png");
        Assert.Equal((1024, 768), DesktopImage.ReadPngSize(File.ReadAllBytes(wallpaper), out _));
    }

    /// <summary>The start of a PNG file: its signature and the size in its header.</summary>
    private static byte[] Png(uint width, uint height)
    {
        byte[] file = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R',
            0, 0, 0, 0, 0, 0, 0, 0, 8, 6, 0, 0, 0];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(16), width);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(20), height);
        return file;
    }
}
