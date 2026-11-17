using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// Header bars: toolbar items with images at either end, a subtitle under the title, and the bar growing for
/// either, as GNOME's do.
/// </summary>
public sealed class HeaderBarTests
{
    [Fact]
    public void ReadsToolbarImagesPlacementsAndTheSubtitle()
    {
        var script = UIScript.Parse("""
            <UIScript version="1">
              <Window width="200" height="100" subtitle="2 items"/>
              <Image name="addIcon" path="icons/add.png"/>
              <ToolbarItem name="back" title="Back" placement="start"/>
              <ToolbarItem name="add" title="Add" image="addIcon" placement="end"/>
              <ToolbarItem name="share" title="Share"/>
            </UIScript>
            """);

        Assert.Equal("2 items", script.Window.Subtitle);
        Assert.Equal(
            [
                new UIToolbarItem("back", "Back", 4, null, UIToolbarPlacement.Start),
                new UIToolbarItem("add", "Add", 5, "addIcon", UIToolbarPlacement.End),
                new UIToolbarItem("share", "Share", 6),
            ],
            script.ToolbarItems);
        Assert.Empty(script.IgnoredAttributes);
    }

    [Theory]
    [InlineData("<ToolbarItem name='a' title='A' placement='middle'/>", "placement is start or end, not \"middle\"")]
    [InlineData("<ToolbarItem name='a' title='A' image='nope'/>", "image=\"nope\", which isn't declared")]
    public void RefusesAToolbarItemItCantPlaceOrDraw(string item, string reason)
    {
        var problem = Assert.Throws<UIScriptException>(() => UIScript.Parse($"<UIScript><Window width='1' height='1'/>{item}</UIScript>"));

        Assert.Contains(reason, problem.Message);
    }

    [Fact]
    public void APlainBarIsTheThemesHeight()
    {
        var window = Window("<ToolbarItem name='add' title='Add'/>");

        Assert.Equal(DesktopTheme.TitleBarHeight, window.BarHeight);
        Assert.Equal(30, DesktopTheme.TitleBarHeight);
        Assert.Equal(DesktopTheme.TitleBarHeight + 100, window.Height);
    }

    [Fact]
    public void ASubtitleOrToolbarImageMakesItTaller()
    {
        var withSubtitle = Window("", subtitle: "2 items");
        var withImage = Window("<Image name='i' path='i.png'/><ToolbarItem name='add' title='Add' image='i'/>");

        foreach (var window in new[] { withSubtitle, withImage })
        {
            Assert.Equal(DesktopTheme.TallTitleBarHeight, window.BarHeight);
            Assert.Equal(DesktopTheme.TallTitleBarHeight + 100, window.Height);
            Assert.Equal(window.Y + DesktopTheme.TallTitleBarHeight, window.ContentOrigin.Y);
            Assert.Equal(DesktopTheme.TallTitleBarHeight, (int)window.TitleBar.Height);
        }
    }

    [Fact]
    public void ClearingTheSubtitleMakesItPlainAgain()
    {
        var window = Window("", subtitle: "2 items");

        window.View.Subtitle = null;

        Assert.Equal(DesktopTheme.TitleBarHeight, window.BarHeight);
    }

    private static DesktopWindow Window(string items, string? subtitle = null)
    {
        var attribute = subtitle is null ? "" : $" subtitle='{subtitle}'";
        return new DesktopWindow("Test", new UIView(UIScript.Parse($"<UIScript><Window width='200' height='100'{attribute}/>{items}</UIScript>")))
        {
            X = 50,
            Y = 40,
        };
    }
}
