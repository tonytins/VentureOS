using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>The look as chosen: light, dark, or on a schedule, and how its parts are written.</summary>
public sealed class DesktopLookTests
{
    private static readonly DesktopLook Evenings = DesktopLook.Standard with { Mode = LookMode.Auto };

    [Theory]
    [InlineData(20, true)]
    [InlineData(2, true)]
    [InlineData(19, true)]
    [InlineData(7, false)]
    [InlineData(12, false)]
    public void AScheduleRunsDarkOvernight(int hour, bool dark) =>
        Assert.Equal(dark, Evenings.IsDarkAt(TimeSpan.FromHours(hour)));

    [Theory]
    [InlineData(9, false)]
    [InlineData(13, true)]
    [InlineData(17, false)]
    public void AScheduleCanRunDarkInTheDay(int hour, bool dark)
    {
        var afternoons = Evenings with { DarkFrom = TimeSpan.FromHours(13), DarkUntil = TimeSpan.FromHours(17) };

        Assert.Equal(dark, afternoons.IsDarkAt(TimeSpan.FromHours(hour)));
    }

    [Fact]
    public void LightAndDarkIgnoreTheTimeAndAScheduleWithoutOneIsLight()
    {
        Assert.False((DesktopLook.Standard with { Mode = LookMode.Light }).IsDarkAt(TimeSpan.FromHours(23)));
        Assert.True((DesktopLook.Standard with { Mode = LookMode.Dark }).IsDarkAt(TimeSpan.FromHours(12)));
        Assert.False(Evenings.IsDarkAt(null));
    }

    [Fact]
    public void TheStandardLooksWallpaperIsOneTheSystemShipsAndItsAccentIsTheStandardOne()
    {
        // What the standard look is, the owner's to choose, isn't pinned here: only that it can be shown.
        var look = DesktopLook.Standard;

        Assert.True(
            Directory.Exists(Path.Combine(AppContext.BaseDirectory, "system", "wallpapers", look.Wallpaper + ".wallpaper")),
            $"{look.Wallpaper} isn't among the system's wallpapers");
        Assert.Equal(DesktopPalette.StandardAccent, look.Accent);
    }

    [Fact]
    public void AutoWithoutATimeIsLight() =>
        Assert.Same(DesktopPalette.Light, (DesktopLook.Standard with { Mode = LookMode.Auto }).PaletteAt(null));

    [Theory]
    [InlineData("00:00", 0, 0)]
    [InlineData("19:30", 19, 30)]
    [InlineData("23:59", 23, 59)]
    public void ReadsAndWritesTimes(string text, int hours, int minutes)
    {
        Assert.True(DesktopLook.TryParseTime(text, out var time));
        Assert.Equal(new TimeSpan(hours, minutes, 0), time);
        Assert.Equal(text, DesktopLook.FormatTime(time));
    }

    [Theory]
    [InlineData("7:00")]
    [InlineData("24:00")]
    [InlineData("12:60")]
    [InlineData("7pm")]
    [InlineData(null)]
    public void RefusesTimesItCantRead(string? text) => Assert.False(DesktopLook.TryParseTime(text, out _));

    [Theory]
    [InlineData("Earthset", true)]
    [InlineData("sky", true)]
    [InlineData("Blue Hour", true)]
    [InlineData("../system", false)]
    [InlineData(".hidden", false)]
    [InlineData("a/b", false)]
    [InlineData(" padded", false)]
    [InlineData("", false)]
    public void KnowsAWallpapersName(string name, bool valid) => Assert.Equal(valid, DesktopLook.IsWallpaperName(name));

    [Fact]
    public void ReadsAndWritesModes()
    {
        foreach (var mode in new[] { LookMode.Light, LookMode.Dark, LookMode.Auto })
        {
            Assert.True(DesktopLook.TryParseMode(DesktopLook.FormatMode(mode), out var parsed));
            Assert.Equal(mode, parsed);
        }

        Assert.False(DesktopLook.TryParseMode("dim", out _));
    }

    [Fact]
    public void ReadsAFileFromBeforeTheLookGrewWithTheRestStandard()
    {
        var look = DesktopAppearance.Read("""<Appearance version="1" mode="dark" accent="#E8922A"/>""");

        Assert.Equal(DesktopLook.Standard with { Mode = LookMode.Dark, Accent = new UIColor(232, 146, 42) }, look);
    }

    [Fact]
    public void ReadsEveryPartAndWritesThemBack()
    {
        var look = new DesktopLook(
            LookMode.Auto, new UIColor(0x21, 0x90, 0xA4), RoundedCorners: false, "sky", new TimeSpan(20, 30, 0), new TimeSpan(6, 15, 0));

        var text = DesktopAppearance.Write(look);

        Assert.Contains("""mode="auto" accent="#2190A4" corners="square" wallpaper="sky" darkFrom="20:30" darkUntil="06:15" """.TrimEnd(), text);
        Assert.Equal(look, DesktopAppearance.Read(text));
        Assert.Equal(look, DesktopAppearance.Read("""<Appearance mode="auto" accent="#2190A4" corners="square" wallpaper="sky" darkFrom="20:30" darkUntil="06:15" glow="soft"/>"""));
    }

    [Theory]
    [InlineData(ScrollBarStyle.Classic)]
    [InlineData(ScrollBarStyle.Automatic)]
    public void WritesScrollBarsAndReadsThemBack(ScrollBarStyle style)
    {
        var look = DesktopLook.Standard with { ScrollBars = style };

        var text = DesktopAppearance.Write(look);

        Assert.Contains($"""scrollbars="{DesktopLook.FormatScrollBars(style)}" """.TrimEnd(), text);
        Assert.Equal(look, DesktopAppearance.Read(text));
    }

    [Theory]
    [InlineData("""<Appearance mode="dark"/>""")]
    [InlineData("""<Appearance mode="dark" scrollbars="overlay"/>""")]
    public void ScrollBarsStayClassicWhenTheFileDoesntSayOrSaysSomethingNewer(string text)
    {
        Assert.Equal(ScrollBarStyle.Classic, DesktopAppearance.Read(text).ScrollBars);
    }

    [Theory]
    [InlineData("""<Appearance corners="round"/>""", "rounded or square")]
    [InlineData("""<Appearance wallpaper="../x"/>""", "isn't a wallpaper's name")]
    [InlineData("""<Appearance darkFrom="7pm"/>""", "darkFrom is a time")]
    [InlineData("""<Appearance darkUntil="25:00"/>""", "darkUntil is a time")]
    [InlineData("""<Appearance mode="night"/>""", "light, dark, or auto")]
    public void RefusesAPartItCantRead(string text, string reason)
    {
        var problem = Assert.Throws<FormatException>(() => DesktopAppearance.Read(text));

        Assert.Contains(reason, problem.Message);
    }
}
