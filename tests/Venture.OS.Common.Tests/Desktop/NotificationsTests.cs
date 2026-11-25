using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>The notification list: posting, its limits, a flood, and the history, on a clock the test turns.</summary>
public sealed class NotificationsTests
{
    private TimeSpan now;
    private readonly Notifications notifications;

    public NotificationsTests() => notifications = new Notifications(() => now);

    private Notification Post(string text, bool show = true, string title = "Notes")
    {
        Assert.True(notifications.TryPost(title, text, title, null, show, out var posted, out var problem), problem);
        return posted;
    }

    [Fact]
    public void APostGoesIntoTheHistoryAndWaitsToShow()
    {
        now = TimeSpan.FromSeconds(7);
        var posted = Post("Saved 3 notes.");

        Assert.Equal(new Notification("Notes", "Saved 3 notes.", "Notes", null, TimeSpan.FromSeconds(7)), posted);
        Assert.Equal([posted], notifications.History);
        Assert.Equal([posted], notifications.Waiting);
        Assert.True(notifications.HasUnseen);
    }

    [Fact]
    public void OneNotShownIsOnlyKept()
    {
        Post("Kept.", show: false);

        Assert.Single(notifications.History);
        Assert.Empty(notifications.Waiting);
        Assert.True(notifications.HasUnseen);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \n ")]
    public void EmptyTextIsRefused(string text)
    {
        var version = notifications.Version;

        Assert.False(notifications.TryPost("Notes", text, null, null, true, out _, out var problem));
        Assert.Equal("a notification needs some text", problem);
        Assert.Empty(notifications.History);
        Assert.Equal(version, notifications.Version);
    }

    [Fact]
    public void LongTextIsCutWithAnEllipsisWithinTheLimit()
    {
        var posted = Post(new string('a', Notifications.MaximumTextLength - 2) + "😀😀");

        Assert.Equal(Notifications.MaximumTextLength - 1, posted.Text.Length);
        Assert.EndsWith("a…", posted.Text);
    }

    [Fact]
    public void AFloodKeepsOnlyTheNewestWaitingAndTheNewestInTheHistory()
    {
        for (var number = 1; number <= 60; number++)
            Post($"Note {number}");

        Assert.Equal(Notifications.MaximumWaiting, notifications.Waiting.Count);
        Assert.Equal("Note 51", notifications.Waiting[0].Text);
        Assert.Equal(Notifications.MaximumKept, notifications.History.Count);
        Assert.Equal(("Note 60", "Note 11"), (notifications.History[0].Text, notifications.History[^1].Text));
    }

    [Fact]
    public void ToastsAreTakenOldestFirst()
    {
        Post("First");
        Post("Second");

        Assert.Equal("First", notifications.TakeWaiting()!.Text);
        Assert.Equal("Second", notifications.TakeWaiting()!.Text);
        Assert.Null(notifications.TakeWaiting());
        Assert.Equal(2, notifications.History.Count);
    }

    [Fact]
    public void SeeingTheHistoryClearsTheDotAndWhatWaits()
    {
        Post("Saved.");

        notifications.MarkSeen();

        Assert.False(notifications.HasUnseen);
        Assert.Empty(notifications.Waiting);
        Assert.Single(notifications.History);
    }

    [Fact]
    public void ClearAllEmptiesEverything()
    {
        Post("Saved.");
        var version = notifications.Version;

        notifications.Clear();

        Assert.Empty(notifications.History);
        Assert.Empty(notifications.Waiting);
        Assert.False(notifications.HasUnseen);
        Assert.True(notifications.Version > version);
    }

    [Fact]
    public void TheHistoryLivesWithTheWindowsSoItOutlastsAVisitToTheDesktop()
    {
        WindowManager windows = new(800, 600);
        Assert.True(windows.Notifications.TryPost("Notes", "Saved.", "Notes", null, true, out _, out _));

        DesktopSession first = new(800, 600, windows: windows);
        first.Leave();
        DesktopSession second = new(800, 600, windows: windows);

        Assert.Same(windows.Notifications, second.Windows.Notifications);
        Assert.Single(second.Windows.Notifications.History);
    }

    public static TheoryData<bool, string> Palettes()
    {
        TheoryData<bool, string> palettes = [];
        foreach (var dark in new[] { false, true })
        {
            foreach (var accent in DesktopPalette.Swatches)
                palettes.Add(dark, accent.ToString());
        }

        return palettes;
    }

    /// <summary>
    /// A toast's text reads on its body over any wallpaper: laid over pure white and pure black, the text reaches
    /// 4.5 to 1 and the secondary text 3 to 1, the WCAG minimums for text and interface parts.
    /// </summary>
    [Theory]
    [MemberData(nameof(Palettes))]
    public void ToastTextIsReadableOverAnyWallpaper(bool dark, string accent)
    {
        var palette = DesktopPalette.Create(DesktopPalette.Swatches.First(swatch => swatch.ToString() == accent), dark);
        Assert.True(palette.NotificationBackground.Alpha >= 200, "the body is nearly opaque");

        foreach (var wallpaper in new[] { new UIColor(255, 255, 255), new UIColor(0, 0, 0) })
        {
            var behind = Over(palette.NotificationBackground, wallpaper);
            Assert.True(Contrast(palette.Text, behind) >= 4.5, $"text on {behind} over {wallpaper}");
            Assert.True(Contrast(palette.SecondaryText, behind) >= 3, $"secondary text on {behind} over {wallpaper}");
        }
    }

    private static UIColor Over(UIColor top, UIColor under)
    {
        int Mix(int a, int b) => ((a * top.Alpha) + (b * (255 - top.Alpha))) / 255;
        return new UIColor((byte)Mix(top.Red, under.Red), (byte)Mix(top.Green, under.Green), (byte)Mix(top.Blue, under.Blue));
    }

    private static double Contrast(UIColor a, UIColor b)
    {
        var (x, y) = (Luminance(a), Luminance(b));
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    private static double Luminance(UIColor color) =>
        (0.2126 * Linear(color.Red)) + (0.7152 * Linear(color.Green)) + (0.0722 * Linear(color.Blue));

    private static double Linear(byte channel)
    {
        var value = channel / 255.0;
        return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
