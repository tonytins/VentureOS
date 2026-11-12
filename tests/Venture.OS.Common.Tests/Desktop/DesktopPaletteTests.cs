using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// Palettes built from an accent, light or dark, after Distro Tycoon 2's appearance. These build palettes without
/// switching the desktop's, so they run alongside other tests.
/// </summary>
public sealed class DesktopPaletteTests
{
    [Fact]
    public void TheStandardLightPaletteKeepsTodaysLook()
    {
        var light = DesktopPalette.Light;

        Assert.False(light.IsDark);
        Assert.Equal(new UIColor(30, 120, 220), light.Accent);
        Assert.Equal(new UIColor(240, 243, 247), light.Panel);
        Assert.Equal(new UIColor(24, 28, 36), light.Text);
        Assert.Equal(new UIColor(255, 255, 255), light.InputBackground);
        AssertNear(new UIColor(196, 210, 226), light.TitleBarBottom);
        AssertNear(new UIColor(158, 200, 242), light.Selection);
        AssertNear(new UIColor(32, 86, 140), light.SkyTop);
        AssertNear(new UIColor(180, 211, 242), light.SkyBottom);
    }

    [Fact]
    public void TintsFollowTheAccent()
    {
        var orange = DesktopPalette.Create(new UIColor(242, 132, 24), isDark: false);

        Assert.True(orange.Selection.Red > orange.Selection.Blue);
        Assert.True(orange.TitleBarBottom.Red > orange.TitleBarBottom.Blue);
        Assert.True(orange.SkyTop.Red > orange.SkyTop.Blue);
        Assert.Equal(DesktopPalette.Light.Panel, orange.Panel);
    }

    [Fact]
    public void TheDarkPaletteIsDarkWithLightText()
    {
        var dark = DesktopPalette.Create(DesktopPalette.StandardAccent, isDark: true);

        Assert.True(dark.IsDark);
        Assert.True(Brightness(dark.Panel) < 70);
        Assert.True(Brightness(dark.InputBackground) < Brightness(dark.Panel));
        Assert.True(Brightness(dark.Text) > 200);
        Assert.True(Brightness(dark.SkyTop) < Brightness(DesktopPalette.Light.SkyTop));
        Assert.True(Brightness(dark.Selection) < Brightness(DesktopPalette.Light.Selection));
    }

    [Fact]
    public void TheAccentStaysReadable()
    {
        var yellow = new UIColor(250, 220, 30);
        var navy = new UIColor(0, 0, 160);

        var light = DesktopPalette.Create(yellow, isDark: false);
        var dark = DesktopPalette.Create(navy, isDark: true);

        // Dark enough to read on white in a light palette, light enough to read on the dark panel in a dark one.
        Assert.True(Brightness(light.Accent) <= 150);
        Assert.True(Brightness(dark.Accent) >= 120);
        Assert.Equal(yellow, light.ChosenAccent);
        Assert.Equal(navy, dark.ChosenAccent);
        Assert.Equal(new UIColor(255, 255, 255), light.HighlightedText);
    }

    [Fact]
    public void OffersDistroTycoonsSwatchesWithTheStandardAccentFirst()
    {
        Assert.Equal(8, DesktopPalette.Swatches.Count);
        AssertNear(DesktopPalette.StandardAccent, DesktopPalette.Swatches[0]);
    }

    [Theory]
    [InlineData("#1E78DC", 30, 120, 220)]
    [InlineData("#e8922a", 232, 146, 42)]
    public void ReadsHexColors(string text, byte red, byte green, byte blue)
    {
        Assert.True(DesktopPalette.TryParseColor(text, out var color));
        Assert.Equal(new UIColor(red, green, blue), color);
        Assert.Equal(text.ToUpperInvariant(), DesktopPalette.Format(color));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1E78DC")]
    [InlineData("#1E78D")]
    [InlineData("#1E78DC00")]
    [InlineData("#GG78DC")]
    [InlineData("blue")]
    public void RefusesAnythingButSixHexDigits(string text)
    {
        Assert.False(DesktopPalette.TryParseColor(text, out _));
    }

    private static int Brightness(UIColor color) =>
        (int)((0.299f * color.Red) + (0.587f * color.Green) + (0.114f * color.Blue));

    private static void AssertNear(UIColor expected, UIColor actual)
    {
        Assert.InRange(actual.Red, expected.Red - 3, expected.Red + 3);
        Assert.InRange(actual.Green, expected.Green - 3, expected.Green + 3);
        Assert.InRange(actual.Blue, expected.Blue - 3, expected.Blue + 3);
    }
}
