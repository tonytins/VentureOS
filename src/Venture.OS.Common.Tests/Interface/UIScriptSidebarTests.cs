using Venture.OS.Interface;

namespace Venture.OS.Tests.Interface;

/// <summary>VentureOS's <c>sidebar</c> feature: <c>&lt;Sidebar&gt;</c>, a list of places down the window's left.</summary>
public sealed class UIScriptSidebarTests
{
    private const string Head = """<UIScript requires="sidebar"><Window width="300" height="200"/>""";

    [Fact]
    public void ReadsASidebar()
    {
        var script = UIScript.Parse("""
            <UIScript version="1" requires="sidebar">
              <Window width="300" height="200"/>
              <Sidebar name="places" width="130"/>
            </UIScript>
            """);

        Assert.Equal(new UISidebar("places", 130, 3), script.Sidebar);
        Assert.Equal(["sidebar"], script.RequiredFeatures);
    }

    [Fact]
    public void TakesMenusToo()
    {
        var script = UIScript.Parse("""
            <UIScript requires="menus sidebar">
              <Window width="300" height="200"/>
              <Sidebar name="places" width="130"/>
              <Menu name="file" title="File"/>
            </UIScript>
            """);

        Assert.NotNull(script.Sidebar);
        Assert.Single(script.Menus);
    }

    [Fact]
    public void NeedsNone()
    {
        Assert.Null(UIScript.Parse("""<UIScript requires="sidebar"><Window width="10" height="10"/></UIScript>""").Sidebar);
        Assert.Null(UIScript.Parse("""<UIScript><Window width="10" height="10"/></UIScript>""").Sidebar);
    }

    [Fact]
    public void NeedsTheFeature()
    {
        var exception = Assert.Throws<UIScriptException>(() => UIScript.Parse("""
            <UIScript><Window width="300" height="200"/><Sidebar name="places" width="130"/></UIScript>
            """));

        Assert.Contains("<Sidebar> isn't a UIScript element", exception.Message);
        Assert.Contains("Add requires=\"sidebar\"", exception.Message);
    }

    [Fact]
    public void AnUnknownAttributeIsSkipped()
    {
        var script = UIScript.Parse($"""{Head}<Sidebar name="places" width="130" colour="red"/></UIScript>""");

        Assert.Equal([new UIIgnoredAttribute("Sidebar", "colour", 1)], script.IgnoredAttributes);
    }

    [Theory]
    [InlineData("""<Sidebar width="130"/>""", "name")]
    [InlineData("""<Sidebar name="places"/>""", "width")]
    [InlineData("""<Sidebar name="places" width="wide"/>""", "width")]
    [InlineData("""<Sidebar name="places" width="0"/>""", "positive width")]
    [InlineData("""<Sidebar name="places" width="-20"/>""", "positive width")]
    [InlineData("""<Sidebar name="a" width="100"/><Sidebar name="b" width="100"/>""", "more than one <Sidebar>")]
    [InlineData("""<Sidebar name="go" width="100"/><ToolbarItem name="go" title="Go"/>""", "\"go\"")]
    [InlineData("""<Sidebar name="list" width="100"/><List name="list" x="0" y="0" width="10" height="10"/>""", "\"list\"")]
    public void RefusesMistakes(string elements, string message)
    {
        var text = $"""{Head}{elements}</UIScript>""";

        Assert.Contains(message, Assert.Throws<UIScriptException>(() => UIScript.Parse(text)).Message);
    }
}
