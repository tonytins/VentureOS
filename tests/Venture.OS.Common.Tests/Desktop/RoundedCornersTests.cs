using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// Windows' rounded corners: outside the curve, what's behind shows; along it, the outline. They follow the look,
/// so they run with the appearance tests.
/// </summary>
[Collection(nameof(AppearanceCollection))]
public sealed class RoundedCornersTests : IDisposable
{
    private const int Red = unchecked((int)0xFFFF0000);
    private const int R = DesktopWindow.CornerRadius;

    private readonly WindowManager manager = new(800, 600);
    private readonly RecordingPainter painter = new(800, 600);
    private readonly DesktopWindow window;

    public RoundedCornersTests()
    {
        window = new DesktopWindow("Pad", new UIView(UIScript.Parse("<UIScript><Window width='300' height='100'/></UIScript>")));
        manager.Open(window);
    }

    public void Dispose() => DesktopAppearance.Use(DesktopLook.Standard);

    [Fact]
    public void WhatsBehindShowsOutsideTheCurve()
    {
        Draw();

        // The very corners are the red behind; a little in along the diagonal, the window.
        Assert.Equal(Red, painter.At(window.X, window.Y));
        Assert.Equal(Red, painter.At(window.X + window.Width - 1, window.Y));
        Assert.Equal(Red, painter.At(window.X, window.Y + window.Height - 1));
        Assert.NotEqual(Red, painter.At(window.X + R, window.Y + R));
        Assert.NotEqual(Red, painter.At(window.X + 4, window.Y + 4));
    }

    [Fact]
    public void TheCurvesEdgeIsTheOutline()
    {
        Draw();

        // Where the curve meets the top edge, the outline continues it: the edge's pixel, and the curve's beside it,
        // almost wholly covered.
        var border = DesktopTheme.Border;
        Assert.Equal(DesktopImage.Pack(255, border.Red, border.Green, border.Blue), painter.At(window.X + R, window.Y));
        var beside = painter.At(window.X + R - 1, window.Y);
        Assert.InRange((beside >> 8) & 0xFF, border.Green - 4, border.Green + 4);
        Assert.InRange(beside & 0xFF, border.Blue - 4, border.Blue + 4);
    }

    [Fact]
    public void SquareCornersCoverWhatsBehind()
    {
        DesktopAppearance.Use(DesktopLook.Standard with { RoundedCorners = false });

        Draw();

        Assert.NotEqual(Red, painter.At(window.X, window.Y));
        Assert.NotEqual(Red, painter.At(window.X + window.Width - 1, window.Y + window.Height - 1));
    }

    [Fact]
    public void AWindowBehindAnotherIsRoundedToo()
    {
        DesktopWindow front = new("Front", new UIView(UIScript.Parse("<UIScript><Window width='100' height='60'/></UIScript>")));
        manager.Open(front);
        front.X = 700;

        Draw();

        Assert.Equal(Red, painter.At(window.X, window.Y));
    }

    private void Draw()
    {
        Array.Fill(painter.Pixels, Red);
        manager.Draw(painter);
    }
}
