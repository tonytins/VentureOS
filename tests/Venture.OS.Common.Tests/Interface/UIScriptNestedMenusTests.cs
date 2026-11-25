using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Interface;

/// <summary>
/// Menus that hold their items, separators, and submenus, as the spec's Menus section has them, and the flat form's
/// rules beside them.
/// </summary>
public sealed class UIScriptNestedMenusTests
{
    // The spec's example (docs/uiscript/README.md, Menus), as it's written there.
    private const string SpecExample = """
        <UIScript version="1">
          <Window width="400" height="300"/>

          <Menu name="notesMenu" title="Notes" hotkey="N">
            <MenuItem name="newNote" title="New Note" hotkey="N" shortcut="Ctrl+N"/>
            <Menu name="sortMenu" title="Sort By" hotkey="S">
              <MenuItem name="sortByDate" title="Date" hotkey="D"/>
              <MenuItem name="sortByTitle" title="Title" hotkey="T"/>
            </Menu>
            <MenuSeparator/>
            <MenuItem name="clearAll" title="Clear All Notes" hotkey="C"/>
          </Menu>

          <List name="notes" x="10" y="10" width="380" height="250" selectable="true"/>
        </UIScript>
        """;

    private const string Head = "<UIScript><Window width='1' height='1'/>";

    [Fact]
    public void ReadsTheSpecsExample()
    {
        var script = UIScript.Parse(SpecExample);

        var notes = Assert.Single(script.Menus);
        Assert.Equal(("notesMenu", "Notes", (char?)'N'), (notes.Name, notes.Title, notes.Hotkey));
        Assert.Equal(["newNote", "sortMenu", null, "clearAll"], notes.Entries.Select(entry => entry.Name));
        Assert.Equal([false, true, false, false], notes.Entries.Select(entry => entry.IsSubmenu));
        Assert.True(notes.Entries[2].IsSeparator);

        var sort = notes.Entries[1];
        Assert.Equal(("Sort By", (char?)'S', (UIShortcut?)null), (sort.Title, sort.Hotkey, sort.Shortcut));
        Assert.Equal(["sortByDate", "sortByTitle"], sort.Submenu!.Entries.Select(entry => entry.Name));
        Assert.Null(sort.Submenu.Role);
        Assert.Equal(6, sort.Line);

        Assert.Equal(["newNote", "sortMenu", "sortByDate", "sortByTitle", null, "clearAll"], notes.AllEntries().Select(entry => entry.Name));
        Assert.Single(script.Controls);
    }

    [Fact]
    public void SubmenusNestToAnyDepth()
    {
        var script = UIScript.Parse(Head + """
            <Menu name="a" title="A"><Menu name="b" title="B"><Menu name="c" title="C"><MenuItem name="deep" title="Deep"/></Menu></Menu></Menu>
            </UIScript>
            """);

        Assert.Equal("deep", script.Menus[0].Entries[0].Submenu!.Entries[0].Submenu!.Entries[0].Name);
        Assert.Equal(["b", "c", "deep"], script.Menus[0].AllEntries().Select(entry => entry.Name));
    }

    [Fact]
    public void AMenuAndASubmenuMayBeEmpty()
    {
        var script = UIScript.Parse(Head + "<Menu name='a' title='A'></Menu><Menu name='b' title='B'><Menu name='c' title='C'/></Menu></UIScript>");

        Assert.Empty(script.Menus[0].Entries);
        Assert.Empty(script.Menus[1].Entries[0].Submenu!.Entries);
    }

    [Fact]
    public void FlatAndNestedMenusSitSideBySide()
    {
        var script = UIScript.Parse(Head + """
            <Menu name="flat" title="Flat" hotkey="F"/>
            <Menu name="nested" title="Nested" hotkey="N"><MenuItem name="b" title="B"/></Menu>
            <MenuItem menu="flat" name="a" title="A"/>
            </UIScript>
            """);

        Assert.Equal(["a"], script.Menus[0].Entries.Select(entry => entry.Name));
        Assert.Equal(["b"], script.Menus[1].Entries.Select(entry => entry.Name));
    }

    [Fact]
    public void HotkeysOnlyClashWithinOneMenu()
    {
        // The submenu's D is its own; so is its parent's.
        var script = UIScript.Parse(Head + """
            <Menu name="m" title="M"><MenuItem name="a" title="A" hotkey="D"/><Menu name="s" title="S" hotkey="S"><MenuItem name="b" title="B" hotkey="d"/></Menu></Menu>
            </UIScript>
            """);

        Assert.Equal(2, script.Menus[0].Entries.Count);
    }

    [Theory]
    [InlineData("<Menu name='m' title='M'><MenuItem menu='m' name='i' title='I'/></Menu>", "\"i\" is inside the menu \"m\", so it doesn't name one with menu")]
    [InlineData("<Menu name='m' title='M'><MenuSeparator menu='m'/></Menu>", "A separator is inside the menu \"m\"")]
    [InlineData("<Menu name='m' title='M'><Menu name='s' title='S' role='file'/></Menu>", "A submenu can't have a role")]
    [InlineData("<Menu name='m' title='M'><MenuItem name='i' title='I'/></Menu><MenuItem menu='m' name='j' title='J'/>", "\"j\" names the menu \"m\", which holds its items inside it")]
    [InlineData("<Menu name='m' title='M'><Menu name='s' title='S'/></Menu><MenuItem menu='m' name='j' title='J'/>", "\"j\" names the menu \"m\", which holds its items inside it")]
    [InlineData("<Menu name='m' title='M'><Menu name='s' title='S'/></Menu><MenuItem menu='s' name='j' title='J'/>", "\"j\" names the submenu \"s\"")]
    [InlineData("<Menu name='m' title='M'><Button name='b' x='0' y='0' width='1' height='1'/></Menu>", "<Button> can't be inside <Menu>")]
    [InlineData("<Menu name='m' title='M'><MenuItem name='i' title='I'><MenuItem name='j' title='J'/></MenuItem></Menu>", "<MenuItem> can't be inside <MenuItem>; only a <Menu>, a <Pane>, a <Sidebar>, or a <Grid> holds other elements")]
    [InlineData("<Region name='r' x='0' y='0' width='1' height='1'><Menu name='m' title='M'/></Region>", "<Menu> can't be inside <Region>")]
    [InlineData("<Menu name='m' title='M'><MenuItem name='a' title='A' hotkey='S'/><Menu name='s' title='S' hotkey='s'/></Menu>", "Two entries in the M menu use the hotkey s")]
    [InlineData("<Menu name='m' title='M'><MenuItem name='a' title='A'/><Menu name='s' title='S'><MenuItem name='a' title='A'/></Menu></Menu>", "\"a\" is used more than once")]
    [InlineData("<Menu name='m' title='M'><Menu name='m' title='Again'/></Menu>", "\"m\" is used more than once")]
    [InlineData("<Menu name='m' title='M'><MenuItem name='a' title='A' shortcut='Ctrl+K'/><Menu name='s' title='S'><MenuItem name='b' title='B' shortcut='Ctrl+k'/></Menu></Menu>", "Two menu items use the shortcut Ctrl+K")]
    public void RefusesWhatTheSpecRefuses(string elements, string message)
    {
        var exception = Assert.Throws<UIScriptException>(() => UIScript.Parse(Head + elements + "</UIScript>"));

        Assert.Contains(message, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ItemsInASubmenuAreDisabledAndCheckedByName()
    {
        UIView view = new(UIScript.Parse(SpecExample));

        view.SetDisabled(true, "sortByDate");
        view.SetSelected(true, "sortByTitle");
        view.SetDisabled(true, "sortMenu");

        Assert.True(view.IsDisabled("sortByDate"));
        Assert.True(view.IsSelected("sortByTitle"));
        Assert.True(view.IsDisabled("sortMenu"));
    }
}
