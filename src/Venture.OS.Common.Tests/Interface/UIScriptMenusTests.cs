using Venture.OS.Interface;

namespace Venture.OS.Tests.Interface;

/// <summary>VentureOS's <c>menus</c> feature: <c>&lt;Menu&gt;</c>, <c>&lt;MenuItem&gt;</c>, and <c>&lt;MenuSeparator&gt;</c>.</summary>
public sealed class UIScriptMenusTests
{
    private const string Head = "<UIScript requires=\"menus\"><Window width=\"400\" height=\"300\"/>";

    [Fact]
    public void ReadsMenusAndTheirEntriesInOrder()
    {
        var script = UIScript.Parse(Head + """
              <Menu name="notesMenu" title="Notes" hotkey="N"/>
              <MenuItem menu="notesMenu" name="newNote" title="New Note" hotkey="N" shortcut="Ctrl+N"/>
              <Menu name="editMenu" title="Edit"/>
              <MenuSeparator menu="notesMenu"/>
              <MenuItem menu="notesMenu" name="clearAll" title="Clear All Notes" shortcut="Ctrl+Shift+k"/>
              <MenuItem menu="editMenu" name="copy" title="Copy" shortcut="Alt+c"/>
              <Button name="add" x="10" y="10" width="80" height="24" text="Add"/>
            </UIScript>
            """);

        Assert.Equal(["menus"], script.RequiredFeatures);
        Assert.Equal(["notesMenu", "editMenu"], script.Menus.Select(menu => menu.Name));

        var notes = script.Menus[0];
        Assert.Equal(("Notes", (char?)'N'), (notes.Title, notes.Hotkey));
        Assert.Equal(3, notes.Entries.Count);
        Assert.Equal(("newNote", "New Note", (char?)'N'), (notes.Entries[0].Name, notes.Entries[0].Title, notes.Entries[0].Hotkey));
        Assert.Equal(new UIShortcut(true, false, false, 'N'), notes.Entries[0].Shortcut);
        Assert.True(notes.Entries[1].IsSeparator);
        Assert.Equal("Ctrl+Shift+K", notes.Entries[2].Shortcut!.ToString());

        Assert.Equal("Alt+C", Assert.Single(script.Menus[1].Entries).Shortcut!.ToString());
        Assert.Single(script.Controls);
    }

    [Fact]
    public void LetsAMenuBeEmpty() =>
        Assert.Empty(UIScript.Parse(Head + "<Menu name='m' title='Later'/></UIScript>").Menus[0].Entries);

    [Theory]
    [InlineData("<Menu name='m' title='M'/>")]
    [InlineData("<MenuItem menu='m' name='i' title='I'/>")]
    [InlineData("<MenuSeparator menu='m'/>")]
    public void NeedsTheFeatureDeclared(string element)
    {
        var exception = Assert.Throws<UIScriptException>(() =>
            UIScript.Parse($"<UIScript><Window width='1' height='1'/>{element}</UIScript>"));

        Assert.Contains("Add requires=\"menus\"", exception.Message);
    }

    [Theory]
    [InlineData("<Menu title='M'/>", "needs name")]
    [InlineData("<Menu name='m'/>", "needs title")]
    [InlineData("<Menu name='m' title='M' hotkey='NN'/>", "hotkey=\"NN\"")]
    [InlineData("<Menu name='m' title='M' hotkey='N'/><Menu name='e' title='E' hotkey='n'/>", "Two menus use the hotkey")]
    [InlineData("<Menu name='m' title='M'/><Menu name='m' title='M2'/>", "\"m\" is used more than once")]
    [InlineData("<MenuItem menu='nope' name='i' title='I'/>", "\"i\" is in the menu \"nope\", which isn't declared")]
    [InlineData("<MenuSeparator menu='nope'/>", "A separator is in the menu \"nope\"")]
    [InlineData("<Menu name='m' title='M'/><MenuItem menu='m' title='I'/>", "needs name")]
    [InlineData("<Menu name='m' title='M'/><MenuItem menu='m' name='i' title='I' hotkey='A'/><MenuItem menu='m' name='j' title='J' hotkey='a'/>", "Two items in the M menu use the hotkey")]
    [InlineData("<Menu name='m' title='M'/><MenuItem menu='m' name='i' title='I' shortcut='Ctrl+N'/><MenuItem menu='m' name='j' title='J' shortcut='Ctrl+n'/>", "Two menu items use the shortcut Ctrl+N")]
    [InlineData("<Menu name='m' title='M'/><MenuItem menu='m' name='i' title='I' shortcut='N'/>", "needs Ctrl or Alt")]
    [InlineData("<Menu name='m' title='M'/><MenuItem menu='m' name='i' title='I' shortcut='Shift+N'/>", "needs Ctrl or Alt")]
    [InlineData("<Menu name='m' title='M'/><MenuItem menu='m' name='i' title='I' shortcut='Ctrl+Ctrl+N'/>", "shortcut=\"Ctrl+Ctrl+N\"")]
    [InlineData("<Menu name='m' title='M'/><MenuItem menu='m' name='i' title='I' shortcut='Super+N'/>", "shortcut=\"Super+N\"")]
    [InlineData("<Menu name='m' title='M'/><MenuItem menu='m' name='i' title='I' shortcut='Ctrl+F1'/>", "shortcut=\"Ctrl+F1\"")]
    [InlineData("<Menu name='m' title='M'/><MenuItem menu='m' name='go' title='Go'/><Button name='go' x='0' y='0' width='1' height='1'/>", "\"go\" is used more than once")]
    public void RefusesMalformedMenus(string elements, string message)
    {
        var exception = Assert.Throws<UIScriptException>(() => UIScript.Parse($"{Head}{elements}</UIScript>"));

        Assert.Contains(message, exception.Message);
    }
}
