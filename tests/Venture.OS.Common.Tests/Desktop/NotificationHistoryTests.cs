using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>The bell and the history it opens: opening and closing, stacks, Clear All, scrolling, and the dot.</summary>
public sealed class NotificationHistoryTests
{
    private TimeSpan now;
    private readonly WindowManager windows = new(800, 600);
    private readonly DesktopSession session;
    private readonly RecordingPainter painter = new(800, 600);

    public NotificationHistoryTests()
    {
        session = new DesktopSession(800, 600, windows: windows, text: TestDesktopText.Create()) { Clock = () => now };
        DesktopAppearance.Use(DesktopLook.Standard);
        Frame();
    }

    private NotificationHistory History => session.History;

    private void Post(string app, string text) =>
        Assert.True(windows.Notifications.TryPost(app, text, app, null, true, out _, out _));

    private bool Frame(bool down = false, int x = 10, int y = 300, int scroll = 0, KeyPress[]? keys = null)
    {
        now += TimeSpan.FromSeconds(0.05);
        var changed = session.Update(new DesktopInput(x, y, down, scroll, keys, now));
        painter.Texts.Clear();
        session.Draw(painter);
        return changed;
    }

    private void Click(int x, int y)
    {
        Frame(down: true, x: x, y: y);
        Frame(x: x, y: y);
    }

    // The bell, as the bar last drew it: left of the clock, right of the menus.
    private (int X, int Y) Bell()
    {
        for (var x = 799; x > 400; x--)
        {
            if (session.MenuBar.OnBell(x, 10))
                return (x, 10);
        }

        Assert.Fail("the bar has no bell");
        return default;
    }

    [Fact]
    public void TheBellOpensAndClosesTheHistory()
    {
        var (x, y) = Bell();

        Click(x, y);
        Assert.True(History.IsOpen);
        Assert.True(session.MenuBar.BellOn);

        Click(x, y);
        Assert.False(History.IsOpen);
    }

    [Fact]
    public void EscapeOrAClickOutsideClosesItAndTheClickGoesNoFurther()
    {
        var events = new List<UIEvent>();
        windows.Open(new DesktopWindow("Under", new UIView(UIScript.Parse("""
            <UIScript><Window width="300" height="200"/><Button name="ok" x="0" y="0" width="300" height="200"/></UIScript>
            """))) { EventHandler = (_, uiEvent) => events.Add(uiEvent) });
        History.Open();
        Frame(keys: [new KeyPress(DesktopKey.Escape)]);
        Assert.False(History.IsOpen);

        History.Open();
        Click(400, 300);
        Assert.False(History.IsOpen);
        Assert.Empty(events);
    }

    [Fact]
    public void TheVentureOSMenuOpensItWithCtrlShiftN()
    {
        Frame(keys: [new KeyPress(DesktopKey.Character, 'n', Control: true, Shift: true)]);
        Assert.True(History.IsOpen);

        Frame(keys: [new KeyPress(DesktopKey.Character, 'n', Control: true, Shift: true)]);
        Assert.False(History.IsOpen);
    }

    [Fact]
    public void TheDotShowsWhatArrivedUnseenUntilTheHistoryOpens()
    {
        Assert.False(session.MenuBar.BellDot);

        Post("Notes", "Saved.");
        Frame();
        Assert.True(session.MenuBar.BellDot);

        History.Open();
        Frame();
        Assert.False(session.MenuBar.BellDot);
    }

    [Fact]
    public void WhileItsOpenNoToastsShowAndArrivalsGoStraightIntoIt()
    {
        History.Open();
        Post("Notes", "Saved.");
        Frame();

        Assert.Empty(session.Toasts.Showing);
        Assert.Empty(windows.Notifications.Waiting);
        Assert.Single(History.Groups);
        Assert.Contains(painter.Texts, text => text.Text == "Saved.");
    }

    [Fact]
    public void EachAppsNotificationsStackNewestFirstWithACount()
    {
        Post("Terminal", "Terminal stopped.");
        Post("Notes", "Saved.");
        Post("Terminal", "Terminal stopped again.");
        History.Open();
        Frame();

        Assert.Equal(["Terminal", "Notes"], History.Groups.Select(group => group.App));
        Assert.Equal(["Terminal stopped again.", "Terminal stopped."], History.Groups[0].Notifications.Select(n => n.Text));
        Assert.Contains(painter.Texts, text => text.Text == "Terminal stopped again.");
        Assert.DoesNotContain(painter.Texts, text => text.Text == "Terminal stopped.");
        Assert.Contains(painter.Texts, text => text.Text == "1 more from Terminal");
    }

    [Fact]
    public void AClickOnAStackExpandsItAndAnotherCollapsesIt()
    {
        Post("Terminal", "Terminal stopped.");
        Post("Terminal", "Terminal stopped again.");
        History.Open();
        Frame();
        var card = painter.Texts.Single(text => text.Text == "Terminal stopped again.");

        Click(card.X, card.Y);
        Assert.True(History.IsOpen);
        Assert.Contains(painter.Texts, text => text.Text == "Terminal stopped.");
        Assert.DoesNotContain(painter.Texts, text => text.Text.Contains("more from", StringComparison.Ordinal));

        Click(card.X, card.Y);
        Assert.DoesNotContain(painter.Texts, text => text.Text == "Terminal stopped.");
    }

    [Fact]
    public void ClearAllEmptiesItAndItSaysSo()
    {
        Post("Notes", "Saved.");
        History.Open();
        Frame();
        var clear = painter.Texts.Single(text => text.Text == "Clear All");

        Click(clear.X + 2, clear.Y + 2);

        Assert.True(History.IsOpen);
        Assert.Empty(windows.Notifications.History);
        Assert.Contains(painter.Texts, text => text.Text == "No notifications");
    }

    [Fact]
    public void ItScrollsWhenThereIsMoreThanFits()
    {
        for (var number = 0; number < 20; number++)
            Post($"App {number}", "Something happened.");
        History.Open();
        Frame();

        Frame(x: 700, y: 300, scroll: -3);

        Assert.True(History.Scrolled > 0);
        Frame(x: 700, y: 300, scroll: 100);
        Assert.Equal(0, History.Scrolled);
    }

    [Fact]
    public void ItsCardsSitInTheTopRightBelowTheBar()
    {
        Post("Notes", "Saved.");
        History.Open();
        Frame();

        var heading = painter.Texts.Single(text => text.Text == "Notifications");
        Assert.True(heading.X > 800 - NotificationHistory.Margin - NotificationCard.Width && heading.Y > MenuBar.Height);
    }
}
