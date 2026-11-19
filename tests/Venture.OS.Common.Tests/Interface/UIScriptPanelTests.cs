using Venture.OS.Interface;

namespace Venture.OS.Tests.Interface;

/// <summary>Panels (<c>&lt;Panel&gt;</c> in place of <c>&lt;Window&gt;</c>) and the <c>shelf</c> feature.</summary>
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
        var script = UIScript.Parse($"""<UIScript><Panel width="400" height="72" edge="{written}"/></UIScript>""");

        Assert.Equal(UIWindowRole.Panel, script.Window.Role);
        Assert.Equal(edge, script.Window.Edge);
        Assert.Empty(script.IgnoredAttributes);
    }

    [Fact]
    public void AWindowsRoleAndEdgeAreNoLongerReadAndSaySo()
    {
        var script = UIScript.Parse("""<UIScript><Window width="300" height="72" role="panel" edge="bottom"/></UIScript>""");

        Assert.Equal(UIWindowRole.Window, script.Window.Role);
        Assert.Null(script.Window.Edge);
        Assert.Equivalent(
            new[] { new UIIgnoredAttribute("Window", "role", 1), new UIIgnoredAttribute("Window", "edge", 1) },
            script.IgnoredAttributes, strict: true);
    }

    [Fact]
    public void APanelNeedsAnEdge()
    {
        var problem = Assert.Throws<UIScriptException>(() =>
            UIScript.Parse("""<UIScript><Panel width="300" height="72"/></UIScript>"""));

        Assert.Contains("edge", problem.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""<Window width="300" height="200"/><Panel width="300" height="72" edge="top"/>""")]
    [InlineData("""<Panel width="300" height="72" edge="top"/><Panel width="300" height="72" edge="bottom"/>""")]
    public void RefusesMoreThanOneWindowOrPanel(string elements)
    {
        var problem = Assert.Throws<UIScriptException>(() => UIScript.Parse($"<UIScript>{elements}</UIScript>"));

        Assert.Contains("more than one <Window> or <Panel>", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APanelSkipsWhatOnlyAWindowHasAndSaysSo()
    {
        var script = UIScript.Parse("""
            <UIScript><Panel width="300" height="72" edge="bottom" resizable="true" subtitle="Hi" minWidth="10" minHeight="10"/></UIScript>
            """);

        Assert.False(script.Window.IsResizable);
        Assert.Null(script.Window.Subtitle);
        Assert.Null(script.Window.MinimumSize);
        Assert.Equal(["minHeight", "minWidth", "resizable", "subtitle"], script.IgnoredAttributes.Select(ignored => ignored.Attribute).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void RefusesAnEdgeItDoesNotKnow()
    {
        var problem = Assert.Throws<UIScriptException>(() =>
            UIScript.Parse("""<UIScript><Panel width="300" height="72" edge="left"/></UIScript>"""));

        Assert.Contains("A panel's edge is top or bottom, not \"left\"", problem.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""<Panel width="300" height="72" edge="bottom"/><ToolbarItem name="go" title="Go"/>""", "no toolbar items")]
    [InlineData("""<Panel width="300" height="72" edge="bottom"/><TitleField name="address"/>""", "no title field")]
    public void RefusesWhatAPanelCannotHave(string elements, string reason)
    {
        var problem = Assert.Throws<UIScriptException>(() => UIScript.Parse($"<UIScript>{elements}</UIScript>"));

        Assert.Contains(reason, problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesAPanelWithASidebar()
    {
        Assert.Throws<UIScriptException>(() => UIScript.Parse("""
            <UIScript requires="sidebar"><Panel width="300" height="72" edge="bottom"/><Sidebar name="s" width="80"/></UIScript>
            """));
    }

    [Fact]
    public void ATopPanelMayHaveMenus()
    {
        var script = UIScript.Parse("""
            <UIScript requires="menus"><Panel width="1024" height="24" edge="top"/><Menu name="file" title="File"/></UIScript>
            """);

        Assert.Equal(UIEdge.Top, script.Window.Edge);
        Assert.Single(script.Menus);
    }

    [Fact]
    public void ReadsAShelfInADocumentThatRequiresIt()
    {
        var script = UIScript.Parse("""
            <UIScript requires="shelf">
              <Panel width="400" height="72" edge="bottom"/>
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
