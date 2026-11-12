using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// The unified title bar: toolbar items, the title field, and minimize. The recording painter measures 8 pixels a
/// character, so the toolbar is laid out from the right: Remove (6 letters, 68 wide), then Add (3, 44 wide).
/// </summary>
public sealed class WindowFrameTests
{
    private const string Document = """
        <UIScript version="1">
          <Window width="300" height="100"/>
          <ToolbarItem name="add" title="Add"/>
          <ToolbarItem name="remove" title="Remove"/>
          <TitleField name="address" placeholder="Path"/>
          <TextField name="field" x="10" y="10" width="100" height="20"/>
        </UIScript>
        """;

    private readonly WindowManager manager = new(800, 600);
    private readonly RecordingPainter painter = new(800, 600);
    private readonly List<UIEvent> events = [];
    private readonly DesktopWindow window;

    public WindowFrameTests()
    {
        window = new DesktopWindow("Files", new UIView(UIScript.Parse(Document)))
        {
            EventHandler = (_, uiEvent) => events.Add(uiEvent),
        };
        manager.Open(window);
        manager.Draw(painter);
    }

    [Fact]
    public void LaysOutToolbarItemsFromTheRightAndTheTitleFieldBeforeThem()
    {
        var remove = window.ToolbarItemBounds(1);
        var add = window.ToolbarItemBounds(0);

        Assert.Equal(window.X + window.Width - 8 - 68, (int)remove.X);
        Assert.Equal((int)remove.X - 6 - 44, (int)add.X);
        Assert.Equal((FramePart.ToolbarItem, 0), window.Hit((int)add.X + 3, (int)add.Y + 3));
        Assert.Equal((FramePart.TitleField, -1), window.Hit((int)window.TitleFieldBounds.X + 3, (int)add.Y + 3));
        Assert.Equal((FramePart.Close, -1), window.Hit((int)window.CloseButton.X + 6, (int)window.CloseButton.Y + 6));
        Assert.Equal((FramePart.Minimize, -1), window.Hit((int)window.MinimizeButton.X + 6, (int)window.MinimizeButton.Y + 6));
        Assert.Equal((FramePart.None, -1), window.Hit(window.X + 5, window.Y + 60));
    }

    [Fact]
    public void AToolbarItemReportsAPressOnRelease()
    {
        var add = window.ToolbarItemBounds(0);

        Click((int)add.X + 3, (int)add.Y + 3);

        Assert.Equal([new UIEvent(UIEventKind.Pressed, "add")], events);
    }

    [Fact]
    public void ADisabledItemOrAPressThatLeavesDoesNothing()
    {
        var add = window.ToolbarItemBounds(0);
        window.View.SetDisabled(true, "remove");
        var remove = window.ToolbarItemBounds(1);

        Click((int)remove.X + 3, (int)remove.Y + 3);
        Frame((int)add.X + 3, (int)add.Y + 3, down: true);
        Frame(window.X + 5, window.Y + 60, down: false);

        Assert.Empty(events);
    }

    [Fact]
    public void TheTitleFieldEditsAndSubmits()
    {
        window.View.SetText("address", "/mnt");
        var field = window.TitleFieldBounds;
        Click((int)field.X + 3, (int)field.Y + 3);
        Assert.True(window.IsEditingTitle);

        Frame(0, 0, keys: [new KeyPress(DesktopKey.Character, '/'), KeyPress.Typed('u'), new KeyPress(DesktopKey.Enter)]);

        Assert.False(window.IsEditingTitle);
        Assert.Equal("/mnt/u", window.View.Text("address"));
        Assert.Equal([new UIEvent(UIEventKind.Submitted, "address", "/mnt/u")], events);
        Assert.Equal("", window.View.Text("field"));
    }

    [Fact]
    public void AToolbarItemActsOnWhatWasTyped()
    {
        string? typed = null;
        window.EventHandler = (self, _) => typed = self.View.Text("address");
        var field = window.TitleFieldBounds;
        Click((int)field.X + 3, (int)field.Y + 3);
        Frame(0, 0, keys: [KeyPress.Typed('/'), KeyPress.Typed('a')]);

        var add = window.ToolbarItemBounds(0);
        Click((int)add.X + 3, (int)add.Y + 3);

        Assert.Equal("/a", typed);
        Assert.False(window.IsEditingTitle);
    }

    [Fact]
    public void EscapeOrAClickElsewherePutsTheTitleBack()
    {
        window.View.SetText("address", "/mnt");
        var field = window.TitleFieldBounds;

        Click((int)field.X + 3, (int)field.Y + 3);
        Frame(0, 0, keys: [KeyPress.Typed('x'), new KeyPress(DesktopKey.Escape)]);
        Assert.Equal("/mnt", window.View.Text("address"));

        Click((int)field.X + 3, (int)field.Y + 3);
        Frame(0, 0, keys: [KeyPress.Typed('y')]);
        Click(window.X + 20, window.Y + 40);

        Assert.False(window.IsEditingTitle);
        Assert.Equal("/mnt", window.View.Text("address"));
        Assert.Empty(events);
    }

    [Fact]
    public void APlainTitleIsCentredInTheWindow()
    {
        DesktopWindow plain = new("Files", new UIView(UIScript.Parse("""<UIScript><Window width="300" height="100"/></UIScript>""")));
        manager.Open(plain);
        painter.Texts.Clear();
        manager.Draw(painter);

        // "Files" is 40 wide, and fits clear of the buttons in the window's middle.
        Assert.Contains(painter.Texts, text => text.Text == "Files" && text.X == plain.X + ((300 - 40) / 2));
    }

    [Fact]
    public void AnIdleAddressIsPlainTextCentredInTheRoomLeft()
    {
        window.View.SetText("address", "/mnt");
        painter.Texts.Clear();
        manager.Draw(painter);

        // The room runs from after the buttons (X + 53) to before Add (X + 166), 113 wide. The middle of the window
        // is too close to the toolbar, so the title is centred in that room: 48 wide, the least a title takes.
        var bounds = window.TitleFieldBounds;
        Assert.Equal((window.X + 53 + ((113 - 48) / 2), 48), ((int)bounds.X, (int)bounds.Width));
        Assert.Contains(painter.Texts, text => text.Text == "/mnt" && text.X == (int)bounds.X + ((48 - 32) / 2));

        // The bar beside the title still drags the window.
        Assert.Equal((FramePart.Bar, -1), window.Hit(window.X + 60, window.Y + 10));
    }

    [Fact]
    public void ALongAddressKeepsItsEnd()
    {
        window.View.SetText("address", "/mnt/user/apps/Calculator.app/Contents");
        painter.Texts.Clear();
        manager.Draw(painter);

        // 103 pixels of room for text after the padding: the ellipsis and 11 characters.
        Assert.Contains(painter.Texts, text => text.Text == "…pp/Contents");
    }

    [Fact]
    public void AnEmptyAddressShowsItsPlaceholder()
    {
        Assert.Contains(painter.Texts, text => text.Text == "Path" && text.Color == DesktopTheme.SecondaryText);
    }

    [Fact]
    public void EditingTheAddressTakesAllTheRoom()
    {
        window.View.SetText("address", "/mnt");
        manager.Draw(painter);
        var field = window.TitleFieldBounds;

        Click((int)field.X + 3, (int)field.Y + 3);

        Assert.True(window.IsEditingTitle);
        Assert.Equal((window.X + 53, 113), ((int)window.TitleFieldBounds.X, (int)window.TitleFieldBounds.Width));
        Assert.Equal((FramePart.TitleField, -1), window.Hit(window.X + 60, window.Y + 10));
    }

    [Fact]
    public void MinimizingHidesTheWindowUntilItOpensAgain()
    {
        DesktopWindow other = new("Other", new UIView(UIScript.Parse("""<UIScript><Window width="100" height="50"/></UIScript>""")));
        manager.Open(other);
        manager.Open(window);
        manager.Draw(painter);
        var minimize = window.MinimizeButton;

        Click((int)minimize.X + 6, (int)minimize.Y + 6);

        Assert.True(window.IsMinimized);
        Assert.Same(other, manager.Focused);
        Assert.NotSame(window, manager.WindowAt(window.X + 5, window.Y + 60));

        manager.Open(window);
        Assert.False(window.IsMinimized);
        Assert.Same(window, manager.Focused);
    }

    private void Click(int x, int y)
    {
        Frame(x, y, down: true);
        Frame(x, y, down: false);
    }

    private void Frame(int x, int y, bool down = false, KeyPress[]? keys = null)
    {
        manager.Update(new DesktopInput(x, y, down, Keys: keys));
        manager.Draw(painter);
    }
}
