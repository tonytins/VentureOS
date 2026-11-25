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
    public void NeedsNoRequires()
    {
        var script = UIScript.Parse("""
            <UIScript><Window width="300" height="200"/><Sidebar name="places" width="130"/></UIScript>
            """);

        Assert.Equal("places", script.Sidebar!.Name);
    }

    [Fact]
    public void AnUnknownAttributeIsSkipped()
    {
        var script = UIScript.Parse($"""{Head}<Sidebar name="places" width="130" colour="red"/></UIScript>""");

        Assert.Equal([new UIIgnoredAttribute("Sidebar", "colour", 1)], script.IgnoredAttributes);
    }

    [Fact]
    public void ASidebarMayHoldControlsAndVisualsInPlaceOfRows()
    {
        var script = UIScript.Parse($"""
            {Head}
            <Image name="art" path="art.png"/>
            <Sidebar name="player" width="200" edge="right">
              <Visual name="cover" image="art" x="20" y="10" width="160" height="160"/>
              <Button name="stop" x="70" y="180" width="60" height="24" text="Stop"/>
            </Sidebar>
            <List name="stations" x="10" y="10" width="280" height="180"/>
            </UIScript>
            """);

        Assert.True(script.Sidebar!.HoldsContent);
        Assert.True(script.Visuals.Single().InSidebar);
        Assert.True(script.Control("stop")!.InSidebar);
        Assert.False(script.Control("stations")!.InSidebar);
        Assert.Empty(script.IgnoredAttributes);
    }

    [Fact]
    public void AnEmptySidebarIsAListOfRows() =>
        Assert.False(UIScript.Parse($"""{Head}<Sidebar name="places" width="130"></Sidebar></UIScript>""").Sidebar!.HoldsContent);

    [Theory]
    [InlineData(null, UIEdge.Left)]
    [InlineData("left", UIEdge.Left)]
    [InlineData("right", UIEdge.Right)]
    public void ReadsWhichEdgeItSitsOn(string? edge, UIEdge expected)
    {
        var attribute = edge is null ? "" : $" edge=\"{edge}\"";
        var script = UIScript.Parse($"{Head}<Sidebar name=\"player\" width=\"200\"{attribute}/></UIScript>");

        Assert.Equal(expected, script.Sidebar!.Edge);
        Assert.Empty(script.IgnoredAttributes);
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
    [InlineData("""<Sidebar name="places" width="100" edge="top"/>""", "left or right, not \"top\"")]
    [InlineData("""<Sidebar name="s" width="100"><Button name="b" x="0" y="0" width="1" height="1"/></Sidebar><Pane name="p" title="P"/>""", "a sidebar has one or the other")]
    [InlineData("""<Sidebar name="s" width="100"><Pane name="p" title="P"/></Sidebar>""", "<Pane> can't be inside <Sidebar>; a sidebar holds controls and visuals")]
    [InlineData("""<Sidebar name="s" width="100"><Image name="i" path="i.png"/></Sidebar>""", "<Image> can't be inside <Sidebar>")]
    [InlineData("""<Sidebar name="s" width="100"><Button name="b" x="0" y="0" width="1" height="1" pane="p"/></Sidebar><Pane name="p" title="P"/>""", "inside the sidebar, so it can't be in a pane")]
    public void RefusesMistakes(string elements, string message)
    {
        var text = $"""{Head}{elements}</UIScript>""";

        Assert.Contains(message, Assert.Throws<UIScriptException>(() => UIScript.Parse(text)).Message);
    }
}
