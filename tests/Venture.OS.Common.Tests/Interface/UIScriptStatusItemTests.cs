using Venture.OS.Interface;

namespace Venture.OS.Tests.Interface;

/// <summary>
/// <c>&lt;StatusItem&gt;</c>, a root in place of <c>&lt;Window&gt;</c> or <c>&lt;Panel&gt;</c>, after the sidebar: empty
/// it's a button, with menus a drop-down, with controls a panel.
/// </summary>
public sealed class UIScriptStatusItemTests
{
    [Fact]
    public void ReadsAButtonWithItsTitleAndImage()
    {
        var script = UIScript.Parse("""
            <UIScript version="1">
              <Image name="cloud" path="cloud.png"/>
              <StatusItem name="weather" title="72°" image="cloud"/>
            </UIScript>
            """);

        Assert.Equal(UIWindowRole.StatusItem, script.Window.Role);
        Assert.Equal(new UIStatusItem("weather", "72°", "cloud", 3), script.StatusItem);
        Assert.Empty(script.Controls);
        Assert.Empty(script.IgnoredAttributes);
    }

    [Fact]
    public void ReadsOneWithMenusOrOneWithAPanelOfControls()
    {
        var menus = UIScript.Parse("""
            <UIScript><StatusItem name="timer" title="5:00"/>
              <Menu name="timerMenu" title="Timer"><MenuItem name="stop" title="Stop"/></Menu>
            </UIScript>
            """);
        var panel = UIScript.Parse("""
            <UIScript><StatusItem name="mixer" title="Mix" width="200" height="80"/>
              <Slider name="volume" x="10" y="10" width="180" height="20"/>
            </UIScript>
            """);

        Assert.Single(menus.Menus);
        Assert.Equal(new UISize(200, 80), panel.Window.Size);
        Assert.Single(panel.Controls);
    }

    [Theory]
    [InlineData("""<StatusItem name="s"/>""", "needs a title, an image, or both")]
    [InlineData("""<StatusItem name="s" image="missing"/>""", "which isn't an <Image>")]
    [InlineData("""<StatusItem name="s" title="t"/><Menu name="m" title="M"><MenuItem name="i" title="I"/></Menu><Button name="b" x="0" y="0" width="10" height="10"/>""", "holds menus and controls")]
    [InlineData("""<StatusItem name="s" title="t"/><Button name="b" x="0" y="0" width="10" height="10"/>""", "needs a width and height")]
    [InlineData("""<StatusItem name="s" title="t"/><ToolbarItem name="x" title="X"/>""", "no toolbar items")]
    [InlineData("""<StatusItem name="s" title="t"/><Sidebar name="side" width="100"/>""", "can't have a sidebar")]
    [InlineData("""<StatusItem name="s" title="t"/><Window width="10" height="10"/>""", "more than one <Window>, <Panel>, or <StatusItem>")]
    [InlineData("""<StatusItem name="s" title="t"/><Button name="s" x="0" y="0" width="10" height="10"/>""", "\"s\"")]
    public void RefusesWhatAStatusItemCantBe(string elements, string reason)
    {
        var problem = Assert.Throws<UIScriptException>(() => UIScript.Parse($"<UIScript>{elements}</UIScript>"));

        Assert.Contains(reason, problem.Message, StringComparison.Ordinal);
    }
}
