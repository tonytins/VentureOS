using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// Apps' status items in the menu bar, left of the bell: a button, one with a menu, and one with a panel of controls,
/// each a window of its app's that the bar draws and the window manager doesn't.
/// </summary>
public sealed class StatusItemTests
{
    private const string Button = """<UIScript><StatusItem name="ping" title="Ping"/></UIScript>""";

    private const string WithMenu = """
        <UIScript><StatusItem name="timer" title="Timer"/>
          <Menu name="timerMenu" title="Timer"><MenuItem name="stop" title="Stop"/></Menu>
        </UIScript>
        """;

    private const string WithPanel = """
        <UIScript><StatusItem name="mixer" title="Mixer" width="200" height="60"/>
          <Slider name="volume" x="10" y="20" width="180" height="20" maxValue="100"/>
        </UIScript>
        """;

    private TimeSpan now;
    private readonly WindowManager windows = new(800, 600);
    private readonly DesktopSession session;
    private readonly RecordingPainter painter = new(800, 600);
    private readonly List<(string App, UIEvent Event)> events = [];

    public StatusItemTests()
    {
        DesktopAppearance.Use(DesktopLook.Standard);
        session = new DesktopSession(800, 600, windows: windows, text: TestDesktopText.Create()) { Clock = () => now };
        Frame();
    }

    private DesktopWindow Item(string document, string app)
    {
        var script = UIScript.Parse(document);
        DesktopWindow window = new(script.StatusItem!.Title ?? "", new UIView(script))
        {
            App = app,
            EventHandler = (sender, uiEvent) => events.Add((sender.App!, uiEvent)),
        };
        windows.Open(window);
        Frame();
        return window;
    }

    private void Frame(bool down = false, int x = 400, int y = 300, KeyPress[]? keys = null)
    {
        now += TimeSpan.FromSeconds(0.05);
        session.Update(new DesktopInput(x, y, down, 0, keys, now));
        painter.Texts.Clear();
        session.Draw(painter);
    }

    private void Click(int x, int y)
    {
        Frame(down: true, x: x, y: y);
        Frame(x: x, y: y);
    }

    // The item's place in the bar, as last drawn.
    private UIRect Bounds(DesktopWindow window)
    {
        for (var index = 0; index < session.MenuBar.Menus.Count; index++)
        {
            if (session.MenuBar.Menus[index].View == window.View)
                return session.MenuBar.TitleBounds(index) ?? default;
        }

        Assert.Fail($"{window.Title} isn't in the bar");
        return default;
    }

    private void ClickOn(DesktopWindow window)
    {
        var bounds = Bounds(window);
        Click((int)(bounds.X + (bounds.Width / 2)), 10);
    }

    [Fact]
    public void AButtonShowsLeftOfTheBellAndIsPressed()
    {
        var ping = Item(Button, "Pinger");

        var bounds = Bounds(ping);
        var title = painter.Texts.Single(text => text.Text == "Ping");
        Assert.True(bounds.Width > 0 && bounds.X > 400, $"at {bounds.X}");
        Assert.True(title.X >= bounds.X && title.Y < MenuBar.Height);
        Assert.False(session.MenuBar.OnBell((int)bounds.X + 1, 10));

        ClickOn(ping);

        Assert.Equal([("Pinger", new UIEvent(UIEventKind.Pressed, "ping"))], events);
        Assert.False(session.MenuBar.IsOpen);
    }

    [Fact]
    public void ItsNeverAWindowItsDrawnOnlyInTheBar()
    {
        var window = new DesktopWindow("Notes", new UIView(UIScript.Parse("""<UIScript><Window width="200" height="100"/></UIScript>""")));
        windows.Open(window);
        Item(Button, "Pinger");

        Assert.Same(window, windows.Focused);
        Assert.Equal(1, painter.Texts.Count(text => text.Text == "Ping"));
    }

    [Fact]
    public void AMenuDropsDownAndItsItemGoesToTheItemsApp()
    {
        var timer = Item(WithMenu, "Timer");

        ClickOn(timer);
        Assert.True(session.MenuBar.IsOpen);
        Assert.Equal(MenuGroup.Status, session.MenuBar.OpenMenu!.Group);

        var stop = session.MenuBar.EntryBounds(0)!.Value;
        Click((int)(stop.X + 10), (int)(stop.Y + (stop.Height / 2)));

        Assert.Equal([("Timer", new UIEvent(UIEventKind.Pressed, "stop"))], events);
    }

    [Fact]
    public void APanelOpensBelowItsItemAndItsControlsWork()
    {
        var mixer = Item(WithPanel, "Mixer");

        ClickOn(mixer);
        Assert.Same(mixer, session.OpenStatusPanel);

        // The slider's track, inside the panel, which sits below the bar with its right edge under the item's.
        var item = Bounds(mixer);
        var left = (int)(item.X + item.Width - 200);
        Click(left + 10 + 9 + 81, MenuBar.Height + 4 + 30);

        Assert.Contains(events, happened => happened.Event is { Kind: UIEventKind.Changed, Control: "volume" });
        Assert.Contains(events, happened => happened.Event is { Kind: UIEventKind.Committed, Control: "volume", Value: 50 });
        Assert.Same(mixer, session.OpenStatusPanel);
    }

    [Fact]
    public void APanelClosesOnItsItemEscapeOrAClickOutside()
    {
        var mixer = Item(WithPanel, "Mixer");

        ClickOn(mixer);
        ClickOn(mixer);
        Assert.Null(session.OpenStatusPanel);

        ClickOn(mixer);
        Frame(keys: [new KeyPress(DesktopKey.Escape)]);
        Assert.Null(session.OpenStatusPanel);

        ClickOn(mixer);
        Click(100, 400);
        Assert.Null(session.OpenStatusPanel);
        Assert.Empty(events);
    }

    [Fact]
    public void OpeningAPanelClosesTheHistory()
    {
        var mixer = Item(WithPanel, "Mixer");
        session.History.Open();

        ClickOn(mixer);

        Assert.False(session.History.IsOpen);
        Assert.Same(mixer, session.OpenStatusPanel);
    }

    [Fact]
    public void AnItemLeavesTheBarWhenItsWindowCloses()
    {
        var ping = Item(Button, "Pinger");

        windows.Close(ping);
        Frame();

        Assert.DoesNotContain(painter.Texts, text => text.Text == "Ping");
        Assert.DoesNotContain(session.MenuBar.Menus, menu => menu.Group == MenuGroup.Status);
    }

    [Fact]
    public void ANewTitleShowsInTheBar()
    {
        var ping = Item(Button, "Pinger");

        ping.Title = "Pong";
        Frame();

        Assert.Contains(painter.Texts, text => text.Text == "Pong");
    }

    [Fact]
    public void ItemsPastTheBarsRoomDontShowTheFirstOpenedNearestTheBell()
    {
        List<DesktopWindow> items = [];
        for (var number = 0; number < 30; number++)
            items.Add(Item($"""<UIScript><StatusItem name="i{number}" title="Item {number}"/></UIScript>""", $"App{number}"));

        Assert.True(Bounds(items[0]).X > Bounds(items[1]).X);
        Assert.Equal(0, Bounds(items[^1]).Width);
        Assert.Contains(painter.Texts, text => text.Text == "Item 0");
    }
}
