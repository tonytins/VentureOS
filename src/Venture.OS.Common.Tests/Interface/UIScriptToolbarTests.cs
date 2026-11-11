using Venture.OS.Interface;

namespace Venture.OS.Tests.Interface;

/// <summary><c>&lt;ToolbarItem&gt;</c> and <c>&lt;TitleField&gt;</c>, the unified title bar's parts.</summary>
public sealed class UIScriptToolbarTests
{
    [Fact]
    public void ReadsToolbarItemsInOrderAndATitleField()
    {
        var script = UIScript.Parse("""
            <UIScript version="1">
              <Window width="300" height="200"/>
              <ToolbarItem name="add" title="Add"/>
              <ToolbarItem name="remove" title="Remove"/>
              <TitleField name="address" placeholder="Path"/>
            </UIScript>
            """);

        Assert.Equal(
            [new UIToolbarItem("add", "Add", 3), new UIToolbarItem("remove", "Remove", 4)],
            script.ToolbarItems);
        Assert.Equal(new UITitleField("address", "Path", 5), script.TitleField);
    }

    [Fact]
    public void NeedsNeither()
    {
        var script = UIScript.Parse("""<UIScript><Window width="10" height="10"/></UIScript>""");

        Assert.Empty(script.ToolbarItems);
        Assert.Null(script.TitleField);
    }

    [Theory]
    [InlineData("""<ToolbarItem name="add"/>""", "title")]
    [InlineData("""<ToolbarItem title="Add"/>""", "name")]
    [InlineData("""<TitleField/>""", "name")]
    [InlineData("""<TitleField name="a"/><TitleField name="b"/>""", "more than one <TitleField>")]
    [InlineData("""<ToolbarItem name="ok" title="OK"/><Button name="ok" x="0" y="0" width="10" height="10"/>""", "\"ok\"")]
    [InlineData("""<TitleField name="add"/><ToolbarItem name="add" title="Add"/>""", "\"add\"")]
    public void RefusesMistakes(string elements, string message)
    {
        var text = $"""<UIScript><Window width="10" height="10"/>{elements}</UIScript>""";

        Assert.Contains(message, Assert.Throws<UIScriptException>(() => UIScript.Parse(text)).Message);
    }
}
