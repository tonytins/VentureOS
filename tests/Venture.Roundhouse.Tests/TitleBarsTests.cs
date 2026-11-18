namespace Venture.Roundhouse.Tests;

/// <summary><see cref="TitleBars"/>: where the coloured bar can be the window's title bar.</summary>
public sealed class TitleBarsTests
{
    [Theory]
    [InlineData("KDE", null, true)]
    [InlineData("GNOME", null, true)]
    [InlineData("ubuntu:GNOME", null, true)]
    [InlineData("sway", null, false)]
    [InlineData("Hyprland", null, false)]
    [InlineData("KDE:i3", null, false)]
    [InlineData(null, null, false)]
    [InlineData("", null, false)]
    [InlineData("KDE", "Ubuntu", false)]
    public void TheBarMergesOnDesktopsThatDrawFloatingWindows(string? desktop, string? wsl, bool merges)
    {
        Assert.Equal(merges, TitleBars.Merges(desktop, wsl));
    }
}
