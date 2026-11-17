using Venture.OS.Desktop;
using Venture.OS.Time;

namespace Venture.OS.Tests.Desktop;

/// <summary>The time on the right of the menu bar, as Tiger showed it, redrawn once a minute.</summary>
public sealed class MenuClockTests
{
    private DateTime now = new(2026, 11, 17, 20, 45, 30, DateTimeKind.Utc);
    private TimeSpan elapsed;

    [Fact]
    public void ShowsTheTimeOnTheRightOfTheBar()
    {
        var (session, clock) = Open();
        clock.SetUtcOffset(TimeSpan.FromHours(-5));
        RecordingPainter painter = new(800, 600);

        Frame(session);
        session.Draw(painter);

        Assert.Equal("Tue 3:45 PM", session.MenuBar.Status);
        var (text, x, y, _, _, _) = Assert.Single(painter.Texts, drawn => drawn.Text == "Tue 3:45 PM");
        Assert.True(y < MenuBar.Height);
        Assert.Equal(800 - 14 - (text.Length * 8), x);
    }

    [Fact]
    public void RedrawsWhenTheMinuteTurnsAndNotBefore()
    {
        var (session, _) = Open();
        Frame(session);
        session.Draw(new RecordingPainter(800, 600));

        now = now.AddSeconds(20);
        Assert.False(Frame(session));

        now = now.AddSeconds(15);
        Assert.True(Frame(session));
        Assert.Equal("Tue 8:46 PM", session.MenuBar.Status);
    }

    [Fact]
    public void AChangedOffsetShowsOnTheNextFrame()
    {
        var (session, clock) = Open();
        Frame(session);
        session.Draw(new RecordingPainter(800, 600));

        clock.SetUtcOffset(TimeSpan.FromHours(9));

        Assert.True(Frame(session));
        Assert.Equal("Wed 5:45 AM", session.MenuBar.Status);
    }

    [Fact]
    public void NoTimeShowsWhenTheHardwareClockIsntSet()
    {
        now = DateTime.UnixEpoch;
        var (session, _) = Open();
        RecordingPainter painter = new(800, 600);

        Frame(session);
        session.Draw(painter);

        Assert.Null(session.MenuBar.Status);
        Assert.DoesNotContain(painter.Texts, text => text.Text.EndsWith(" AM") || text.Text.EndsWith(" PM"));
    }

    private (DesktopSession Session, WallClock Clock) Open()
    {
        WallClock clock = new(() => now);
        return (new DesktopSession(800, 600) { WallClock = clock, Clock = () => elapsed }, clock);
    }

    private bool Frame(DesktopSession session)
    {
        elapsed += TimeSpan.FromMilliseconds(16);
        return session.Update(new DesktopInput(400, 300, false, Time: elapsed));
    }
}
