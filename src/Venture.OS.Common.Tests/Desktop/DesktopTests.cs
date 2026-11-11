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
        var pixels = DesktopBackground.Render(200, 100);

        // The top corner is untouched by the glow.
        Assert.Equal(DesktopBackground.Pack(32, 86, 140), pixels[0]);
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
    public void DrawsTheSkyTheHintAndThePointer()
    {
        DesktopSession session = new(400, 300);
        RecordingPainter painter = new(400, 300);

        Assert.True(session.Update(new DesktopInput(50, 60, false)));
        session.Draw(painter);

        Assert.Equal(DesktopBackground.Pack(32, 86, 140), painter.At(399, 0));
        Assert.Equal(Black, painter.At(50, 60));
        var hint = painter.Texts[^1];
        Assert.Equal(DesktopSession.LeaveHint, hint.Text);
        Assert.Equal((400 - (DesktopSession.LeaveHint.Length * 8)) / 2, hint.X);
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
    public void EscapeLeavesTheDesktop()
    {
        DesktopSession session = new(100, 100);

        Assert.False(session.Update(new DesktopInput(1, 1, false, Keys: [new KeyPress(DesktopKey.Escape)])));
        Assert.True(session.IsFinished);
    }

    private static int Brightness(int pixel) => ((pixel >> 16) & 0xFF) + ((pixel >> 8) & 0xFF) + (pixel & 0xFF);
}
