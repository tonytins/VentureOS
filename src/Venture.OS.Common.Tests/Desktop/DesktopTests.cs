using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

public sealed class DesktopTests
{
    private const int Black = unchecked((int)0xFF000000);
    private const int White = unchecked((int)0xFFFFFFFF);

    [Fact]
    public void TheSkyRunsFromDeepBlueAtTheTopToPaleBlueAtTheBottom()
    {
        var pixels = DesktopBackground.Render(200, 100, DesktopPalette.Light);

        // The top corner is untouched by the glow.
        var top = DesktopPalette.Light.SkyTop;
        Assert.Equal(DesktopBackground.Pack(top.Red, top.Green, top.Blue), pixels[0]);
        var (topBlue, bottomBlue) = (pixels[100] & 0xFF, pixels[(99 * 200) + 100] & 0xFF);
        Assert.True(bottomBlue > topBlue, "The bottom should be paler than the top.");
        Assert.All(pixels, pixel => Assert.Equal(0xFF, (pixel >> 24) & 0xFF));
    }

    [Fact]
    public void TheGlowBrightensTheBottomMiddle()
    {
        var pixels = DesktopBackground.Render(400, 300);
        var bottomMiddle = pixels[(299 * 400) + 200];
        var bottomLeft = pixels[299 * 400];

        Assert.True(Brightness(bottomMiddle) > Brightness(bottomLeft));
    }

    [Fact]
    public void RejectsAnEmptyScreen()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DesktopBackground.Render(0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => DesktopBackground.Render(10, -1));
    }

    [Fact]
    public void ThePointerIsAWhiteArrowWithABlackEdgeAndItsTipAtThePosition()
    {
        RecordingPainter painter = new(40, 40);

        Pointer.Draw(painter, 10, 5);

        Assert.Equal(Black, painter.At(10, 5));
        Assert.Equal(0, painter.At(11, 5));
        Assert.Equal(Black, painter.At(10, 8));
        Assert.Equal(White, painter.At(11, 8));
        Assert.Equal(11, Pointer.Width);
        Assert.Equal(17, Pointer.Height);
    }

    [Fact]
    public void DrawsTheSkyTheMenuBarAndThePointer()
    {
        DesktopSession session = new(400, 300);
        RecordingPainter painter = new(400, 300);

        Assert.True(session.Update(new DesktopInput(50, 60, false)));
        session.Draw(painter);

        var barTop = DesktopTheme.TitleBarTop;
        Assert.Equal(DesktopBackground.Pack(barTop.Red, barTop.Green, barTop.Blue), painter.At(399, 0));
        Assert.Equal(DesktopBackground.Render(400, 300)[(MenuBar.Height * 400) + 399], painter.At(399, MenuBar.Height));
        Assert.Equal(Black, painter.At(50, 60));
        Assert.Contains(painter.Texts, text => text.Text == "VentureOS" && text.Y < MenuBar.Height);
    }

    [Fact]
    public void RedrawsOnlyWhenSomethingChanged()
    {
        DesktopSession session = new(100, 100);
        RecordingPainter painter = new(100, 100);
        DesktopInput still = new(10, 10, false);

        Assert.True(session.Update(still));
        session.Draw(painter);

        Assert.False(session.Update(still));
        Assert.True(session.Update(still with { PointerX = 11 }));
        Assert.True(session.Update(still with { PointerX = 11, PointerDown = true }));
        Assert.False(session.Update(still with { PointerX = 11, PointerDown = true }));
    }

    [Fact]
    public void DrawsTheFirstFrameEvenWithoutInput()
    {
        DesktopSession session = new(100, 100);

        Assert.True(session.Update(new DesktopInput(50, 50, false)));
    }

    [Fact]
    public void KeepsThePointerOnTheScreen()
    {
        DesktopSession session = new(100, 80);

        session.Update(new DesktopInput(-20, 500, false));

        Assert.Equal((0, 79), session.PointerPosition);
    }

    [Fact]
    public void ReturnToShellLeavesTheDesktop()
    {
        DesktopSession session = new(100, 100);
        KeyPress shortcut = new(DesktopKey.Character, 'q', Control: true, Shift: true);

        session.Update(new DesktopInput(1, 1, false, Keys: [new KeyPress(DesktopKey.Escape)]));
        Assert.False(session.IsFinished);

        Assert.False(session.Update(new DesktopInput(1, 1, false, Keys: [shortcut])));
        Assert.True(session.IsFinished);
    }

    private static int Brightness(int pixel) => ((pixel >> 16) & 0xFF) + ((pixel >> 8) & 0xFF) + (pixel & 0xFF);
}
