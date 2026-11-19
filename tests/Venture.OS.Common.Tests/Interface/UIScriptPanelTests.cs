using Venture.OS.Interface;

namespace Venture.OS.Tests.Interface;

/// <summary>Panels (<c>role</c> and <c>edge</c> on <c>&lt;Window&gt;</c>) and the <c>shelf</c> feature.</summary>
public sealed class UIScriptPanelTests
{
    [Fact]
    public void AnOrdinaryWindowIsAWindow()
    {
        var script = UIScript.Parse("""<UIScript><Window width="300" height="200"/></UIScript>""");

        Assert.Equal(UIWindowRole.Window, script.Window.Role);
        Assert.Null(script.Window.Edge);
    }

    [Theory]
    [InlineData("top", UIEdge.Top)]
    [InlineData("bottom", UIEdge.Bottom)]
    public void ReadsAPanelAndItsEdge(string written, UIEdge edge)
    {
        var script = UIScript.Parse($"""<UIScript><Window width="400" height="72" role="panel" edge="{written}"/></UIScript>""");

        Assert.Equal(UIWindowRole.Panel, script.Window.Role);
        Assert.Equal(edge, script.Window.Edge);
        Assert.Empty(script.IgnoredAttributes);
    }

    [Fact]
    public void ARoleItDoesNotKnowIsAnOrdinaryWindowAndSaysSo()
    {
        var script = UIScript.Parse("""<UIScript><Window width="300" height="200" role="sheet"/></UIScript>""");

        Assert.Equal(UIWindowRole.Window, script.Window.Role);
        Assert.Equal(new UIIgnoredAttribute("Window", "role", 1), Assert.Single(script.IgnoredAttributes));
    }

    [Fact]
    public void RefusesAnEdgeItDoesNotKnow()
    {
        var problem = Assert.Throws<UIScriptException>(() =>
            UIScript.Parse("""<UIScript><Window width="300" height="72" role="panel" edge="left"/></UIScript>"""));

        Assert.Contains("A panel's edge is top or bottom, not \"left\"", problem.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""<Window width="300" height="72" role="panel"/>""", "A panel needs an edge")]
    [InlineData("""<Window width="300" height="72" role="panel" edge="bottom" resizable="true"/>""", "can't be resizable")]
    [InlineData("""<Window width="300" height="72" role="panel" edge="bottom" subtitle="Hi"/>""", "no subtitle")]
    [InlineData("""<Window width="300" height="72" role="panel" edge="bottom"/><ToolbarItem name="go" title="Go"/>""", "no toolbar items")]
    [InlineData("""<Window width="300" height="72" role="panel" edge="bottom"/><TitleField name="address"/>""", "no title field")]
    public void RefusesWhatAPanelCannotHave(string elements, string reason)
    {
        var problem = Assert.Throws<UIScriptException>(() => UIScript.Parse($"<UIScript>{elements}</UIScript>"));

        Assert.Contains(reason, problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesAPanelWithASidebar()
    {
        Assert.Throws<UIScriptException>(() => UIScript.Parse("""
            <UIScript requires="sidebar"><Window width="300" height="72" role="panel" edge="bottom"/><Sidebar name="s" width="80"/></UIScript>
            """));
    }

    [Fact]
    public void ATopPanelMayHaveMenus()
    {
        var script = UIScript.Parse("""
            <UIScript requires="menus"><Window width="1024" height="24" role="panel" edge="top"/><Menu name="file" title="File"/></UIScript>
            """);

        Assert.Equal(UIEdge.Top, script.Window.Edge);
        Assert.Single(script.Menus);
    }

    [Fact]
    public void ReadsAShelfInADocumentThatRequiresIt()
    {
        var script = UIScript.Parse("""
            <UIScript requires="shelf">
              <Window width="400" height="72" role="panel" edge="bottom"/>
              <Shelf name="apps" x="0" y="0" width="400" height="72"/>
            </UIScript>
            """);

        var shelf = Assert.Single(script.Controls);
        Assert.Equal(("apps", UIControlType.Shelf), (shelf.Name, shelf.Type));
        Assert.Equal(new UIFrame(0, 0, 400, 72, null), shelf.Frame);
        Assert.Contains(UIScript.ShelfFeature, UIScript.SupportedFeatures);
    }

    [Fact]
    public void RefusesAShelfWithoutTheFeatureAndSaysHow()
    {
        var problem = Assert.Throws<UIScriptException>(() => UIScript.Parse("""
            <UIScript><Window width="400" height="72"/><Shelf name="apps" x="0" y="0" width="400" height="72"/></UIScript>
            """));

        Assert.Contains("Add requires=\"shelf\" to <UIScript> to use a shelf.", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AShelfDoesNotHaveToBeInAPanel()
    {
        var script = UIScript.Parse("""
            <UIScript requires="shelf"><Window width="400" height="200"/><Shelf name="apps" x="0" y="0" width="400" height="72"/></UIScript>
            """);

        Assert.Equal(UIWindowRole.Window, script.Window.Role);
        Assert.Single(script.Controls);
    }
}
