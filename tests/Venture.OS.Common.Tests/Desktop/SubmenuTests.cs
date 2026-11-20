using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// Submenus in the menu bar: opened beside their entry by the pointer or the keys, crossed into, closed one at a time,
/// and kept shut, shortcuts included, while disabled.
/// </summary>
public sealed class SubmenuTests
{
    private const string Document = """
        <UIScript version="1">
          <Window width="200" height="100"/>
          <Menu name="note" title="Note" hotkey="N">
            <MenuItem name="newNote" title="New Note" hotkey="N" shortcut="Ctrl+N"/>
            <Menu name="sortMenu" title="Sort By" hotkey="S">
              <MenuItem name="sortAdded" title="Order Added" hotkey="O"/>
              <MenuItem name="sortNewest" title="Newest First" hotkey="N"/>
              <MenuItem name="sortTitle" title="A to Z" hotkey="A" shortcut="Ctrl+T"/>
              <Menu name="more" title="More" hotkey="M">
                <MenuItem name="deep" title="Deep" hotkey="D" shortcut="Alt+D"/>
              </Menu>
            </Menu>
            <MenuSeparator/>
            <MenuItem name="clearAll" title="Clear All" hotkey="C"/>
          </Menu>
          <Menu name="view" title="View" hotkey="V">
            <MenuItem name="wrap" title="Wrap Lines" hotkey="W"/>
          </Menu>
        </UIScript>
        """;

    private readonly UIView view = new(UIScript.Parse(Document));
    private readonly MenuBar bar = new();
    private RecordingPainter painter = new(400, 300);

    public SubmenuTests()
    {
        List<BarMenu> menus = [];
        foreach (var menu in view.Script.Menus)
            menus.Add(new BarMenu(menu, MenuGroup.Window, view));
        bar.SetMenus(menus);
        bar.Draw(painter);
    }

    private static (int X, int Y) Middle(UIRect bounds) => ((int)(bounds.X + 10), (int)(bounds.Y + (bounds.Height / 2)));

    /// <summary>Opens Note with a click and draws it.</summary>
    private void OpenNote()
    {
        bar.UpdatePointer(20, 10, pressed: true, released: false);
        bar.UpdatePointer(20, 10, pressed: false, released: true);
        bar.Draw(painter);
    }

    /// <summary>Rests the pointer on Sort By, which opens it, and draws it.</summary>
    private void HoverSortBy()
    {
        var (x, y) = Middle(bar.EntryBounds(1)!.Value);
        bar.UpdatePointer(x, y, pressed: false, released: false);
        bar.Draw(painter);
    }

    [Fact]
    public void RestingOnASubmenusEntryOpensItBesideItsParent()
    {
        OpenNote();
        HoverSortBy();

        Assert.Equal(["Note", "Sort By"], bar.OpenMenus.Select(menu => menu.Title));
        var parent = bar.EntryBounds(1)!.Value;
        var first = bar.EntryBounds(0, level: 1)!.Value;
        Assert.True(first.X > parent.X + 100, "The submenu opens on the parent's right.");
        Assert.Equal(parent.Y, first.Y);
        Assert.True(bar.Contains((int)first.X + 5, (int)first.Y + 5));

        // Its entry stays highlighted, as the way into the submenu, while nothing in the submenu is yet.
        Assert.Null(bar.Highlighted);
    }

    [Fact]
    public void ClickingAnItemInASubmenuChoosesItForTheMenuInTheBar()
    {
        OpenNote();
        HoverSortBy();

        var (x, y) = Middle(bar.EntryBounds(2, level: 1)!.Value);
        bar.UpdatePointer(x, y, pressed: true, released: false);
        var choice = bar.UpdatePointer(x, y, pressed: false, released: true);

        Assert.Equal("sortTitle", choice?.Item.Name);
        Assert.Equal("note", choice?.Menu.Menu.Name);
        Assert.False(bar.IsOpen);
    }

    [Fact]
    public void ReleasingOnASubmenusEntryChoosesNothingAndKeepsItOpen()
    {
        OpenNote();
        var (x, y) = Middle(bar.EntryBounds(1)!.Value);
        bar.UpdatePointer(x, y, pressed: true, released: false);

        Assert.Null(bar.UpdatePointer(x, y, pressed: false, released: true));
        Assert.Equal(2, bar.OpenMenus.Count);
    }

    [Fact]
    public void SubmenusNestAndCloseWhenThePointerRestsOnAnotherEntry()
    {
        OpenNote();
        HoverSortBy();
        var (x, y) = Middle(bar.EntryBounds(3, level: 1)!.Value);
        bar.UpdatePointer(x, y, pressed: false, released: false);
        bar.Draw(painter);
        Assert.Equal(["Note", "Sort By", "More"], bar.OpenMenus.Select(menu => menu.Title));

        // Resting on another entry of Sort By closes More.
        (x, y) = Middle(bar.EntryBounds(0, level: 1)!.Value);
        bar.UpdatePointer(x, y, pressed: false, released: false);
        bar.UpdatePointer(x, y, pressed: false, released: false);
        Assert.Equal(2, bar.OpenMenus.Count);
        Assert.Equal("sortAdded", bar.Highlighted?.Name);

        // Resting on Clear All, back in Note, closes Sort By.
        (x, y) = Middle(bar.EntryBounds(3)!.Value);
        bar.UpdatePointer(x, y, pressed: false, released: false);
        bar.UpdatePointer(x, y, pressed: false, released: false);
        Assert.Single(bar.OpenMenus);
        Assert.Equal("clearAll", bar.Highlighted?.Name);
    }

    [Fact]
    public void CrossingTheParentsOtherEntriesTowardTheSubmenuKeepsItOpen()
    {
        OpenNote();
        HoverSortBy();
        var sortBy = bar.EntryBounds(1)!.Value;
        var submenu = bar.EntryBounds(3, level: 1)!.Value;

        // From Sort By, down and right over the separator and Clear All, toward the submenu's lower rows.
        var x = (int)(sortBy.X + sortBy.Width - 40);
        var y = (int)(sortBy.Y + sortBy.Height - 2);
        for (var step = 0; step < 10 && x < submenu.X; step++)
        {
            x += 6;
            y += 3;
            bar.UpdatePointer(x, y, pressed: false, released: false);
            Assert.Equal(2, bar.OpenMenus.Count);
        }

        // Stopping over Clear All, though, lets it take over.
        var clear = Middle(bar.EntryBounds(3)!.Value);
        bar.UpdatePointer(clear.X, clear.Y, pressed: false, released: false);
        bar.UpdatePointer(clear.X, clear.Y, pressed: false, released: false);
        Assert.Single(bar.OpenMenus);
    }

    [Fact]
    public void ASubmenuWithNoRoomOnTheRightOpensOnTheLeft()
    {
        painter = new RecordingPainter(300, 300);
        bar.Draw(painter);
        OpenNote();
        HoverSortBy();

        var submenu = bar.EntryBounds(0, level: 1)!.Value;
        Assert.True(submenu.X + submenu.Width <= 300, $"The submenu runs off the screen, to {submenu.X + submenu.Width}.");
        Assert.True(submenu.X < bar.EntryBounds(1)!.Value.X + bar.EntryBounds(1)!.Value.Width - 2);
    }

    [Fact]
    public void TheArrowsOpenAndCloseSubmenus()
    {
        bar.OpenByHotkey('n');
        bar.UpdateKey(new KeyPress(DesktopKey.Down));
        Assert.Equal("sortMenu", bar.Highlighted?.Name);

        // Right opens it with its first item highlighted, and Left closes it, back on its entry.
        bar.UpdateKey(new KeyPress(DesktopKey.Right));
        Assert.Equal(2, bar.OpenMenus.Count);
        Assert.Equal("sortAdded", bar.Highlighted?.Name);
        bar.UpdateKey(new KeyPress(DesktopKey.Left));
        Assert.Single(bar.OpenMenus);
        Assert.Equal("sortMenu", bar.Highlighted?.Name);

        // Right on an item moves to the next menu in the bar, as before; Left with no submenu, back.
        bar.UpdateKey(new KeyPress(DesktopKey.Up));
        bar.UpdateKey(new KeyPress(DesktopKey.Right));
        Assert.Equal("View", bar.OpenMenu?.Menu.Title);
        bar.UpdateKey(new KeyPress(DesktopKey.Left));
        Assert.Equal("Note", bar.OpenMenu?.Menu.Title);
    }

    [Fact]
    public void EnterAndHotkeysOpenSubmenusAndChooseInsideThem()
    {
        bar.OpenByHotkey('n');
        Assert.Null(bar.UpdateKey(KeyPress.Typed('s')));
        Assert.Equal("Sort By", bar.OpenMenus[^1].Title);

        // A hotkey picks in the innermost menu: N is Newest First here, not New Note.
        Assert.Equal("sortNewest", bar.UpdateKey(KeyPress.Typed('n'))?.Item.Name);
        Assert.False(bar.IsOpen);

        bar.OpenByHotkey('n');
        bar.UpdateKey(KeyPress.Typed('s'));
        bar.UpdateKey(new KeyPress(DesktopKey.Up));
        Assert.Equal("more", bar.Highlighted?.Name);
        Assert.Null(bar.UpdateKey(new KeyPress(DesktopKey.Enter)));
        Assert.Equal("deep", bar.UpdateKey(new KeyPress(DesktopKey.Enter))?.Item.Name);
    }

    [Fact]
    public void APointerRestingOffTheMenusKeepsWhatTheKeysHighlighted()
    {
        // The desktop passes the pointer on every frame while a menu is open, wherever it rests.
        bar.UpdatePointer(300, 250, pressed: false, released: false);
        bar.OpenByHotkey('n');
        bar.UpdateKey(KeyPress.Typed('s'));
        bar.UpdatePointer(300, 250, pressed: false, released: false);
        bar.UpdatePointer(300, 250, pressed: false, released: false);
        Assert.Equal("sortAdded", bar.Highlighted?.Name);

        // Moving it, still off the menus, lets the highlight go.
        bar.UpdatePointer(310, 250, pressed: false, released: false);
        Assert.Null(bar.Highlighted);
        Assert.Equal(2, bar.OpenMenus.Count);
    }

    [Fact]
    public void EscapeClosesTheInnermostMenuFirst()
    {
        bar.OpenByHotkey('n');
        bar.UpdateKey(KeyPress.Typed('s'));
        bar.UpdateKey(KeyPress.Typed('m'));
        Assert.Equal(3, bar.OpenMenus.Count);

        bar.UpdateKey(new KeyPress(DesktopKey.Escape));
        Assert.Equal(2, bar.OpenMenus.Count);
        Assert.Equal("more", bar.Highlighted?.Name);
        bar.UpdateKey(new KeyPress(DesktopKey.Escape));
        bar.UpdateKey(new KeyPress(DesktopKey.Escape));
        Assert.False(bar.IsOpen);
    }

    [Fact]
    public void ShortcutsReachIntoSubmenus()
    {
        Assert.Equal("sortTitle", bar.FindShortcut(new KeyPress(DesktopKey.Character, 't', Control: true))?.Item.Name);
        Assert.Equal("deep", bar.FindShortcut(new KeyPress(DesktopKey.Character, 'd', Alt: true))?.Item.Name);

        view.SetDisabled(true, "deep");
        Assert.Null(bar.FindShortcut(new KeyPress(DesktopKey.Character, 'd', Alt: true)));
    }

    [Fact]
    public void ADisabledSubmenuCantBeOpenedAndItsShortcutsDontFire()
    {
        view.SetDisabled(true, "sortMenu");

        Assert.Null(bar.FindShortcut(new KeyPress(DesktopKey.Character, 't', Control: true)));
        Assert.Null(bar.FindShortcut(new KeyPress(DesktopKey.Character, 'd', Alt: true)));

        OpenNote();
        HoverSortBy();
        Assert.Single(bar.OpenMenus);

        bar.OpenByHotkey('n');
        bar.UpdateKey(KeyPress.Typed('s'));
        Assert.Single(bar.OpenMenus);
        bar.UpdateKey(new KeyPress(DesktopKey.Down));
        Assert.Equal("clearAll", bar.Highlighted?.Name);
    }

    [Fact]
    public void ASubmenusEntryHasAnArrowAndNoShortcut()
    {
        OpenNote();
        HoverSortBy();

        var entry = bar.EntryBounds(1)!.Value;
        var arrow = DesktopBackground.Pack(DesktopTheme.HighlightedText.Red, DesktopTheme.HighlightedText.Green, DesktopTheme.HighlightedText.Blue);
        var arrowX = (int)(entry.X + entry.Width - 14);
        var arrowY = (int)entry.Y + ((int)entry.Height - 7) / 2;
        Assert.Equal(arrow, painter.At(arrowX, arrowY + 3));
        Assert.Contains(painter.Texts, text => text.Text == "Sort By" && text.Color == DesktopTheme.HighlightedText);
        Assert.Contains(painter.Texts, text => text.Text == "Ctrl+T");
    }
}
