using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// Palettes after GNOME's libadwaita: neutral surfaces and text, light or dark, with one accent. These build palettes
/// without switching the desktop's, so they run alongside other tests.
/// </summary>
public sealed class DesktopPaletteTests
{
    [Fact]
    public void TheLightPaletteTakesLibadwaitasColors()
    {
        var light = DesktopPalette.Light;

        Assert.False(light.IsDark);
        Assert.Equal(new UIColor(0x35, 0x84, 0xE4), light.ChosenAccent);
        Assert.Equal(new UIColor(0xFA, 0xFA, 0xFB), light.Panel);
        Assert.Equal(new UIColor(255, 255, 255), light.HeaderBar);
        Assert.Equal(new UIColor(255, 255, 255), light.InputBackground);
        Assert.Equal(new UIColor(0xEB, 0xEB, 0xED), light.SidebarBackground);
        Assert.Equal(new UIColor(255, 255, 255), light.MenuBackground);
        Assert.Equal(new UIColor(51, 51, 56), light.Text);
    }

    [Fact]
    public void TheDarkPaletteTakesLibadwaitasColors()
    {
        var dark = DesktopPalette.Create(DesktopPalette.StandardAccent, isDark: true);

        Assert.True(dark.IsDark);
        Assert.Equal(new UIColor(0x22, 0x22, 0x26), dark.Panel);
        Assert.Equal(new UIColor(0x2E, 0x2E, 0x32), dark.HeaderBar);
        Assert.Equal(new UIColor(0x1D, 0x1D, 0x20), dark.InputBackground);
        Assert.Equal(new UIColor(0x2E, 0x2E, 0x32), dark.SidebarBackground);
        Assert.Equal(new UIColor(0x36, 0x36, 0x3A), dark.MenuBackground);
        Assert.Equal(new UIColor(255, 255, 255), dark.Text);
    }

    [Fact]
    public void SurfacesStayNeutralWhateverTheAccent()
    {
        var orange = DesktopPalette.Create(new UIColor(0xED, 0x5B, 0x00), isDark: false);

        // Only the accent and what it marks take its color; the sky, shown without a wallpaper, takes its hue.
        Assert.Equal(DesktopPalette.Light.Panel, orange.Panel);
        Assert.Equal(DesktopPalette.Light.SidebarSelection, orange.SidebarSelection);
        Assert.Equal(DesktopPalette.Light.ButtonHover, orange.ButtonHover);
        Assert.Equal(DesktopPalette.Light.TitleBarTop, orange.TitleBarTop);
        Assert.Equal(DesktopPalette.Light.MenuBarBottom, orange.MenuBarBottom);
        Assert.True(orange.Selection.Red > orange.Selection.Blue);
        Assert.True(orange.SkyTop.Red > orange.SkyTop.Blue);
    }

    [Fact]
    public void TextAndLinesAreQuieterInOrder()
    {
        foreach (var palette in new[] { DesktopPalette.Light, DesktopPalette.Create(DesktopPalette.StandardAccent, isDark: true) })
        {
            var panel = Brightness(palette.Panel);
            int Distance(UIColor color) => Math.Abs(Brightness(color) - panel);

            Assert.True(Distance(palette.Text) > Distance(palette.SecondaryText));
            Assert.True(Distance(palette.SecondaryText) > Distance(palette.DisabledText));
            Assert.True(Distance(palette.Border) < Distance(palette.DisabledText));
        }
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
    public void OffersGnomesNineAccentsWithBlueFirst()
    {
        Assert.Equal(
            ["#3584E4", "#2190A4", "#3A944A", "#C88800", "#ED5B00", "#E62D42", "#D56199", "#9141AC", "#6F8396"],
            DesktopPalette.Swatches.Select(DesktopPalette.Format));
        Assert.Equal(DesktopPalette.StandardAccent, DesktopPalette.Swatches[0]);
    }

    [Theory]
    [InlineData("#3584E4", 0x35, 0x84, 0xE4)]
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
}
