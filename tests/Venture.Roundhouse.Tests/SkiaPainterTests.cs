using Venture.OS.Desktop;
using Venture.OS.Interface;
using Venture.Roundhouse.Drawing;

namespace Venture.Roundhouse.Tests;

/// <summary>
/// Roundhouse's painter: everything but text lands on exactly the values VentureOS's own code gives, and text comes
/// out in VentureOS's fonts where it was asked to.
/// </summary>
[Collection(nameof(PaletteCollection))]
public sealed class SkiaPainterTests : IDisposable
{
    private const int White = unchecked((int)0xFFFFFFFF);
    private const int Red = unchecked((int)0xFFFF0000);

    private readonly SkiaFonts fonts = SkiaFonts.LoadShipped();
    private readonly SkiaPainter painter;

    public SkiaPainterTests()
    {
        painter = new SkiaPainter(100, 60, fonts);
        painter.FillRectangle(0, 0, 100, 60, new UIColor(255, 255, 255));
    }

    public void Dispose()
    {
        painter.Dispose();
        fonts.Dispose();
    }

    [Fact]
    public void FillsAndBlendsAsTheFrameBufferDoes()
    {
        painter.FillRectangle(10, 10, 5, 5, new UIColor(0, 0, 0, 60));

        var expected = Enumerable.Repeat(White, 100 * 60).ToArray();
        FrameBuffer.Fill(expected, 100, 60, 10, 10, 5, 5, new UIColor(0, 0, 0, 60));
        Assert.Equal(expected, painter.Pixels);
    }

    [Fact]
    public void DrawsPicturesAndPixelsAndReadsThemBack()
    {
        painter.DrawImage(new DesktopImage(2, 1, [Red, DesktopImage.Pack(128, 0, 0, 255)]), 0, 0);
        painter.DrawPixels([Red, Red], 98, 59, 2, 1);
        var into = new int[2];

        painter.ReadPixels(into, 0, 0, 2, 1);

        Assert.Equal(Red, into[0]);
        Assert.Equal(DesktopImage.Pack(255, 127, 127, 255), into[1]);
        Assert.Equal(Red, painter.Pixels[(59 * 100) + 99]);
    }

    [Fact]
    public void DrawsTextWhereItsAskedAndNowhereElse()
    {
        painter.DrawText("Hello", 10, 20, new UIColor(0, 0, 0), 13);

        var width = painter.MeasureText("Hello", 13);
        var line = (int)MathF.Ceiling(13 * 1.35f);
        var inked = 0;
        for (var y = 0; y < 60; y++)
        {
            for (var x = 0; x < 100; x++)
            {
                if (painter.Pixels[(y * 100) + x] == White)
                    continue;

                inked++;
                Assert.InRange(x, 9, 10 + width + 1);
                Assert.InRange(y, 20, 20 + line);
            }
        }

        Assert.True(inked > 20, $"Only {inked} pixels were inked.");
    }

    [Fact]
    public void MeasuresInVentureOSsFaces()
    {
        Assert.True(painter.MeasureText("Hello", 26) > painter.MeasureText("Hello", 13));
        Assert.Equal(0, painter.MeasureText("", 13));

        // Fira Code is monospaced, Open Sans isn't.
        Assert.Equal(painter.MeasureText("iii", 13, DesktopFont.Monospace), painter.MeasureText("WWW", 13, DesktopFont.Monospace));
        Assert.True(painter.MeasureText("iii", 13) < painter.MeasureText("WWW", 13));
    }

    [Fact]
    public void ReadsVentureOSsPngsAndRefusesWhatIsntOne()
    {
        var root = RepositoryRoot();
        var icon = SkiaImages.Load(Path.Combine(root, "rootfs", "system", "apps", "Files.app", "Contents", "Resources", "AppIcon.png"));
        List<string> problems = [];

        Assert.NotNull(icon);
        Assert.Equal((64, 64), (icon.Width, icon.Height));
        Assert.Null(SkiaImages.Load(Path.Combine(root, "README.md"), problems.Add));
        Assert.Contains("it isn't a PNG file", Assert.Single(problems));
    }

    /// <summary>The repository, found by going up from the tests to the folder with GitInfo.txt.</summary>
    internal static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "GitInfo.txt")))
            folder = folder.Parent;
        return folder?.FullName ?? throw new DirectoryNotFoundException("The repository's GitInfo.txt isn't above the tests.");
    }
}
