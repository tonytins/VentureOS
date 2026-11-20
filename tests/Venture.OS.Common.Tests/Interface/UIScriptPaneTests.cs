using Venture.OS.Interface;

namespace Venture.OS.Tests.Interface;

/// <summary>The <c>panes</c> feature: <c>&lt;Pane&gt;</c> rows in a sidebar, and <c>pane</c> on controls.</summary>
public sealed class UIScriptPaneTests
{
    private const string Head = """<UIScript requires="sidebar panes"><Window width="300" height="200"/><Sidebar name="side" width="100"/>""";

    [Fact]
    public void ReadsPanesInOrderAndWhichPaneEachControlIsIn()
    {
        var script = UIScript.Parse(Head + """
            <Pane name="general" title="General"/>
            <Pane name="advanced" title="Advanced"/>
            <Text name="always" x="0" y="0" width="10" height="10"/>
            <Button name="go" x="0" y="0" width="10" height="10" pane="advanced"/>
            </UIScript>
            """);

        Assert.Equal([("general", "General"), ("advanced", "Advanced")], script.Panes.Select(pane => (pane.Name, pane.Title)));
        Assert.Null(script.Control("always")!.Pane);
        Assert.Equal("advanced", script.Control("go")!.Pane);
        Assert.Empty(script.IgnoredAttributes);
    }

    [Fact]
    public void NeedsNoRequires()
    {
        var script = UIScript.Parse("""
            <UIScript><Window width="300" height="200"/><Sidebar name="side" width="100"/><Pane name="p" title="P"/></UIScript>
            """);

        Assert.Equal("p", Assert.Single(script.Panes).Name);
    }

    [Fact]
    public void AControlsPaneIsReadEvenWithoutPanesAndMustNameOne()
    {
        // pane was skipped without the old panes module; now it's always read, so one naming no pane refuses.
        Assert.Throws<UIScriptException>(() => UIScript.Parse("""
            <UIScript><Window width="300" height="200"/><Text name="t" x="0" y="0" width="10" height="10" pane="p"/></UIScript>
            """));
    }

    [Fact]
    public void RefusesPanesWithoutASidebar()
    {
        var problem = Assert.Throws<UIScriptException>(() => UIScript.Parse("""
            <UIScript requires="sidebar panes"><Window width="300" height="200"/><Pane name="p" title="P"/></UIScript>
            """));

        Assert.Contains("needs a <Sidebar>", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesAControlInAPaneThatIsNotDeclared()
    {
        var problem = Assert.Throws<UIScriptException>(() => UIScript.Parse(Head + """
            <Pane name="general" title="General"/><Text name="t" x="0" y="0" width="10" height="10" pane="genral"/></UIScript>
            """));

        Assert.Contains("\"t\" has pane=\"genral\", which isn't declared.", problem.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""<Pane name="p" title="P"/><Pane name="p" title="Q"/>""")]
    [InlineData("""<Pane name="side" title="P"/>""")]
    [InlineData("""<Pane name="p" title="P"/><Text name="p" x="0" y="0" width="10" height="10"/>""")]
    public void APaneSharesTheDocumentsNames(string elements)
    {
        Assert.Throws<UIScriptException>(() => UIScript.Parse(Head + elements + "</UIScript>"));
    }

    [Theory]
    [InlineData("""<Pane title="P"/>""")]
    [InlineData("""<Pane name="p"/>""")]
    public void APaneNeedsANameAndATitle(string element)
    {
        Assert.Throws<UIScriptException>(() => UIScript.Parse(Head + element + "</UIScript>"));
    }

    [Fact]
    public void TheShippedSettingsDeclaresItsPanes()
    {
        var script = UIScript.Load(Path.Combine(AppContext.BaseDirectory, "system", "apps", "Settings.app", "Contents", "Resources", "main.uix"));

        Assert.Equal(["Appearance", "Dock", "Clock", "Developer"], script.Panes.Select(pane => pane.Title));
        Assert.All(script.Controls, control => Assert.NotNull(control.Pane));
    }

    [Fact]
    public void APaneHoldsItsControlsWhichAreDrawnInDocumentOrder()
    {
        var script = UIScript.Parse(Head + """
            <Pane name="general" title="General">
              <Text name="label" x="0" y="0" width="10" height="10"/>
              <Button name="go" x="0" y="0" width="10" height="10"/>
            </Pane>
            <Pane name="advanced" title="Advanced"/>
            <Button name="flat" x="0" y="0" width="10" height="10" pane="advanced"/>
            <Text name="always" x="0" y="0" width="10" height="10"/>
            </UIScript>
            """);

        Assert.Equal(["label", "go", "flat", "always"], script.Controls.Select(control => control.Name));
        Assert.Equal(["general", "general", "advanced", null], script.Controls.Select(control => control.Pane));
    }

    [Theory]
    [InlineData("<Pane name='p' title='P'><Button name='b' x='0' y='0' width='1' height='1' pane='p'/></Pane>", "\"b\" is inside the pane \"p\", so it doesn't name one with pane")]
    [InlineData("<Pane name='p' title='P'><Button name='a' x='0' y='0' width='1' height='1'/></Pane><Button name='b' x='0' y='0' width='1' height='1' pane='p'/>", "\"b\" names the pane \"p\", which holds its controls inside it")]
    [InlineData("<Pane name='p' title='P'><Visual name='v' image='i' x='0' y='0' width='1' height='1'/></Pane>", "<Visual> can't be inside <Pane>; a pane holds controls")]
    [InlineData("<Pane name='p' title='P'><Pane name='q' title='Q'/></Pane>", "<Pane> can't be inside <Pane>")]
    [InlineData("<Pane name='p' title='P'><Style name='s'/></Pane>", "<Style> can't be inside <Pane>")]
    public void RefusesWhatTheSpecRefusesOfNestedPanes(string elements, string message) =>
        Assert.Contains(message, Assert.Throws<UIScriptException>(() => UIScript.Parse(Head + elements + "</UIScript>")).Message, StringComparison.Ordinal);

    [Fact]
    public void NestedPanesNeedASidebarToo()
    {
        var problem = Assert.Throws<UIScriptException>(() => UIScript.Parse(
            "<UIScript><Window width='1' height='1'/><Pane name='p' title='P'><Button name='b' x='0' y='0' width='1' height='1'/></Pane></UIScript>"));

        Assert.Contains("needs a <Sidebar>", problem.Message, StringComparison.Ordinal);
    }
}
