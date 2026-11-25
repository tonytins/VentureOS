using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>Toasts on the screen: when they come and go, how they slide, a click, and where they're drawn.</summary>
public sealed class ToastsTests
{
    private TimeSpan now;
    private readonly WindowManager windows = new(800, 600);
    private readonly DesktopSession session;
    private readonly RecordingPainter painter = new(800, 600);

    public ToastsTests()
    {
        session = new DesktopSession(800, 600, windows: windows, text: TestDesktopText.Create()) { Clock = () => now };
        DesktopAppearance.Use(DesktopLook.Standard);
    }

    private Toasts Toasts => session.Toasts;

    private void Post(string text) =>
        Assert.True(windows.Notifications.TryPost("Notes", text, "Notes", null, true, out _, out _));

    private bool Frame(double seconds, bool down = false, int x = 0, int y = 300)
    {
        now = TimeSpan.FromSeconds(seconds);
        return session.Update(new DesktopInput(x, y, down, Time: now));
    }

    [Fact]
    public void AToastComesInStaysFiveSecondsAndSlidesOut()
    {
        Frame(0);
        Post("Saved 3 notes.");

        Assert.True(Frame(0.01));
        Assert.Equal(["Saved 3 notes."], Toasts.Showing.Select(toast => toast.Text));
        Assert.True(Toasts.IsMoving);

        Frame(1);
        Assert.False(Toasts.IsMoving);

        Frame(5.02);
        Assert.True(Toasts.IsMoving);
        Assert.Single(Toasts.Showing);

        Frame(5.2);
        Assert.Empty(Toasts.Showing);
        Assert.Single(windows.Notifications.History);
    }

    [Fact]
    public void AToastAtRestDrawsNoFrames()
    {
        Frame(0);
        Post("Saved.");
        Frame(0.01);
        Frame(1);
        session.Draw(painter);

        Assert.False(Frame(2));
    }

    [Fact]
    public void AtMostThreeShowAndTheRestWaitTheirTurn()
    {
        Frame(0);
        foreach (var text in new[] { "One", "Two", "Three", "Four" })
            Post(text);

        Frame(0.01);
        Assert.Equal(["Three", "Two", "One"], Toasts.Showing.Select(toast => toast.Text));
        Assert.Single(windows.Notifications.Waiting);

        // As the first three start to leave, the fourth comes in.
        Frame(5.02);
        Assert.Equal("Four", Toasts.Showing[0].Text);
        Assert.Empty(windows.Notifications.Waiting);
    }

    [Fact]
    public void AClickDismissesAToastAndNeverReachesTheWindowUnderIt()
    {
        var events = new List<UIEvent>();
        var window = new DesktopWindow("Under", new UIView(UIScript.Parse("""
            <UIScript><Window width="790" height="560"/><Button name="ok" x="0" y="0" width="790" height="560"/></UIScript>
            """))) { EventHandler = (_, uiEvent) => events.Add(uiEvent) };
        windows.Open(window);
        Frame(0);
        Post("Saved.");
        Frame(0.01);
        Frame(1);
        session.Draw(painter);

        // Inside the toast: the top-right, below the bar.
        Frame(1.1, down: true, x: 790, y: 40);
        Frame(1.2, x: 790, y: 40);

        Assert.Empty(events);
        Assert.True(Toasts.IsMoving);
        Frame(1.5);
        Assert.Empty(Toasts.Showing);
    }

    [Fact]
    public void ItsDrawnInTheTopRightBelowTheBarWithItsTitleTextAndAge()
    {
        Frame(0);
        Post("Saved 3 notes.");
        Frame(0.01);
        Frame(1);

        session.Draw(painter);

        var left = 800 - Toasts.Margin - Toasts.Width;
        var title = painter.Texts.Single(text => text.Text == "Notes");
        var body = painter.Texts.Single(text => text.Text == "Saved 3 notes.");
        Assert.True(title.X > left && title.X < 800 - Toasts.Margin && title.Y > MenuBar.Height, $"the title at {title.X}, {title.Y}");
        Assert.True(body.Y > title.Y);
        Assert.Contains(painter.Texts, text => text.Text == "now" && text.Color == DesktopTheme.SecondaryText);
    }

    [Fact]
    public void LongTextShowsThreeLinesEndingInAnEllipsis()
    {
        Frame(0);
        Post(string.Join(' ', Enumerable.Repeat("word", 60)));
        Frame(1);

        session.Draw(painter);

        var lines = painter.Texts.Where(text => text.Text.StartsWith("word", StringComparison.Ordinal)).ToList();
        Assert.Equal(Toasts.MaximumLines, lines.Count);
        Assert.EndsWith("…", lines[^1].Text);
    }

    [Fact]
    public void WhileHiddenNoneComeInAndThoseShowingLeave()
    {
        Frame(0);
        Post("Saved.");
        Frame(1);

        Toasts.Update(TimeSpan.FromSeconds(1.1), hidden: true);
        Toasts.Update(TimeSpan.FromSeconds(1.5), hidden: true);
        Post("While hidden.");
        Toasts.Update(TimeSpan.FromSeconds(1.6), hidden: true);

        Assert.Empty(Toasts.Showing);
        Assert.Single(windows.Notifications.Waiting);
    }

    [Theory]
    [InlineData(0, "now")]
    [InlineData(59, "now")]
    [InlineData(60, "1 min")]
    [InlineData(3599, "59 min")]
    [InlineData(7200, "2 hr")]
    public void SaysHowLongAgoInTheDesktopsWords(int seconds, string said)
    {
        Assert.Equal(said, Toasts.Age(TimeSpan.FromSeconds(seconds), session.Text));
    }
}
