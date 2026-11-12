using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// The recording painter measures 8 pixels a character, so "File" is a 50-pixel title starting at x 10, and the
/// menu's rows start at y 25: New Note to 45, the separator to 54, Clear All to 74.
/// </summary>
public sealed class MenuBarTests
{
    private const string Document = """
        <UIScript version="1" requires="menus">
          <Window width="200" height="100"/>
          <Menu name="file" title="File" hotkey="F"/>
          <MenuItem menu="file" name="newNote" title="New Note" hotkey="N" shortcut="Ctrl+N"/>
          <MenuSeparator menu="file"/>
          <MenuItem menu="file" name="clearAll" title="Clear All" hotkey="C"/>
          <Menu name="view" title="View" hotkey="V"/>
          <MenuItem menu="view" name="wrap" title="Wrap Lines" hotkey="W"/>
        </UIScript>
        """;

    private readonly UIView view = new(UIScript.Parse(Document));
    private readonly MenuBar bar = new();
    private readonly RecordingPainter painter = new(400, 300);

    public MenuBarTests()
    {
        List<BarMenu> menus = [];
        foreach (var menu in view.Script.Menus)
            menus.Add(new BarMenu(menu, MenuGroup.Window, view));
        bar.SetMenus(menus);
        bar.Draw(painter);
    }

    [Fact]
    public void ClickingATitleOpensItsMenuAndClickingAnItemChoosesIt()
    {
        Assert.Null(bar.UpdatePointer(20, 10, pressed: true, released: false));
        Assert.Null(bar.UpdatePointer(20, 10, pressed: false, released: true));
        Assert.Equal("File", bar.OpenMenu?.Menu.Title);
        bar.Draw(painter);

        bar.UpdatePointer(30, 30, pressed: true, released: false);
        var choice = bar.UpdatePointer(30, 30, pressed: false, released: true);

        Assert.Equal("newNote", choice?.Item.Name);
        Assert.False(bar.IsOpen);
    }

    [Fact]
    public void PressingDraggingAndReleasingChoosesToo()
    {
        bar.UpdatePointer(20, 10, pressed: true, released: false);
        bar.Draw(painter);
        bar.UpdatePointer(30, 60, pressed: false, released: false);

        Assert.Equal("clearAll", bar.Highlighted?.Name);
        Assert.Equal("clearAll", bar.UpdatePointer(30, 60, pressed: false, released: true)?.Item.Name);
    }

    [Fact]
    public void ReleasingOnTheSeparatorOrADisabledItemChoosesNothing()
    {
        view.SetDisabled(true, "newNote");
        bar.UpdatePointer(20, 10, pressed: true, released: false);
        bar.Draw(painter);

        Assert.Null(bar.UpdatePointer(30, 30, pressed: false, released: true));
        bar.UpdatePointer(30, 49, pressed: true, released: false);
        Assert.Null(bar.UpdatePointer(30, 49, pressed: false, released: true));
        Assert.True(bar.IsOpen);
    }

    [Fact]
    public void MovingOverAnotherTitleWhileOpenSwitchesMenus()
    {
        bar.UpdatePointer(20, 10, pressed: true, released: false);
        bar.UpdatePointer(20, 10, pressed: false, released: true);

        // "View" starts after File's 50 pixels.
        bar.UpdatePointer(70, 10, pressed: false, released: false);

        Assert.Equal("View", bar.OpenMenu?.Menu.Title);
    }

    [Fact]
    public void ClickingElsewhereOrTheOpenTitleCloses()
    {
        bar.UpdatePointer(20, 10, pressed: true, released: false);
        bar.UpdatePointer(20, 10, pressed: false, released: true);
        bar.UpdatePointer(300, 200, pressed: true, released: false);
        Assert.False(bar.IsOpen);

        bar.UpdatePointer(20, 10, pressed: true, released: false);
        bar.UpdatePointer(20, 10, pressed: false, released: true);
        bar.UpdatePointer(20, 10, pressed: true, released: false);
        Assert.False(bar.IsOpen);
    }

    [Fact]
    public void TheKeyboardOpensMovesAndChooses()
    {
        Assert.True(bar.OpenByHotkey('f'));
        Assert.Equal("newNote", bar.Highlighted?.Name);

        bar.UpdateKey(new KeyPress(DesktopKey.Down));
        Assert.Equal("clearAll", bar.Highlighted?.Name);
        bar.UpdateKey(new KeyPress(DesktopKey.Down));
        Assert.Equal("newNote", bar.Highlighted?.Name);

        bar.UpdateKey(new KeyPress(DesktopKey.Right));
        Assert.Equal("View", bar.OpenMenu?.Menu.Title);
        Assert.Equal("wrap", bar.UpdateKey(new KeyPress(DesktopKey.Enter))?.Item.Name);
    }

    [Fact]
    public void AnItemsHotkeyChoosesItAndEscapeCloses()
    {
        bar.OpenByHotkey('F');
        Assert.Equal("clearAll", bar.UpdateKey(KeyPress.Typed('c'))?.Item.Name);

        bar.OpenByHotkey('F');
        bar.UpdateKey(new KeyPress(DesktopKey.Escape));
        Assert.False(bar.IsOpen);
        Assert.False(bar.OpenByHotkey('x'));
    }

    [Fact]
    public void ArrowsSkipDisabledItems()
    {
        view.SetDisabled(true, "newNote");

        bar.OpenByHotkey('f');
        Assert.Equal("clearAll", bar.Highlighted?.Name);
        bar.UpdateKey(new KeyPress(DesktopKey.Up));
        Assert.Equal("clearAll", bar.Highlighted?.Name);
    }

    [Fact]
    public void ShortcutsMatchTheirModifiersAndSkipDisabledItems()
    {
        Assert.Equal("newNote", bar.FindShortcut(new KeyPress(DesktopKey.Character, 'n', Control: true))?.Item.Name);
        Assert.Null(bar.FindShortcut(KeyPress.Typed('n')));
        Assert.Null(bar.FindShortcut(new KeyPress(DesktopKey.Character, 'n', Control: true, Shift: true)));

        view.SetDisabled(true, "newNote");
        Assert.Null(bar.FindShortcut(new KeyPress(DesktopKey.Character, 'n', Control: true)));
    }

    [Fact]
    public void DrawsTitlesItemsShortcutsAndChecks()
    {
        view.SetSelected(true, "newNote");
        bar.OpenByHotkey('f');
        bar.Draw(painter);

        Assert.Contains(painter.Texts, text => text.Text == "File" && text.X == 19);
        Assert.Contains(painter.Texts, text => text.Text == "New Note" && text.Color == DesktopTheme.HighlightedText);
        Assert.Contains(painter.Texts, text => text.Text == "Ctrl+N");

        // The hotkey's letter is underlined: the C in Clear All.
        var underline = DesktopBackground.Pack(DesktopTheme.Text.Red, DesktopTheme.Text.Green, DesktopTheme.Text.Blue);
        var clearAll = painter.Texts.Find(text => text.Text == "Clear All");
        Assert.Equal(underline, painter.At(clearAll.X + 1, clearAll.Y + DesktopTheme.TextSize - 2));
    }

    [Fact]
    public void NewMenusCloseTheOpenOneButTheSameMenusDont()
    {
        bar.OpenByHotkey('f');
        bar.SetMenus([.. bar.Menus]);
        Assert.True(bar.IsOpen);

        bar.SetMenus([]);
        Assert.False(bar.IsOpen);
    }
}
