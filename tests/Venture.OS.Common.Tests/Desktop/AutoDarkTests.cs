using Venture.OS.Desktop;
using Venture.OS.Time;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// A look on a schedule: dark from one time until another, by the clock, switched as the minute comes. The look is
/// shared, so these run with the appearance tests.
/// </summary>
[Collection(nameof(AppearanceCollection))]
public sealed class AutoDarkTests : IDisposable
{
    private static readonly DesktopLook Evenings = DesktopLook.Standard with { Mode = LookMode.Auto };

    // 18:58 UTC, on UTC.
    private DateTime now = new(2026, 11, 17, 18, 58, 30, DateTimeKind.Utc);
    private readonly WallClock clock;

    public AutoDarkTests()
    {
        clock = new WallClock(() => now);
        DesktopAppearance.TimeOfDay = () => clock.Now?.TimeOfDay;
    }

    public void Dispose()
    {
        DesktopAppearance.TimeOfDay = () => null;
        DesktopAppearance.Use(DesktopLook.Standard);
    }

    [Fact]
    public void RefreshSwitchesAScheduledLookWhenItsTimeComes()
    {
        DesktopAppearance.Use(Evenings);
        Assert.False(DesktopTheme.Palette.IsDark);
        Assert.False(DesktopAppearance.Refresh());

        now = now.AddMinutes(2);

        Assert.True(DesktopAppearance.Refresh());
        Assert.True(DesktopTheme.Palette.IsDark);
        Assert.False(DesktopAppearance.Refresh());
    }

    [Fact]
    public void RefreshLeavesALookThatIsntScheduledAlone()
    {
        DesktopAppearance.Use(DesktopLook.Standard with { Mode = LookMode.Light });
        now = now.AddHours(3);

        Assert.False(DesktopAppearance.Refresh());
        Assert.False(DesktopTheme.Palette.IsDark);
    }

    [Fact]
    public void TheDesktopTurnsDarkAsTheMinuteComes()
    {
        DesktopAppearance.Use(Evenings);
        TimeSpan elapsed = default;
        DesktopSession session = new(800, 600) { WallClock = clock, Clock = () => elapsed };
        session.Update(new DesktopInput(400, 300, false));
        session.Draw(new RecordingPainter(800, 600));

        now = now.AddSeconds(20);
        elapsed += TimeSpan.FromMilliseconds(16);
        Assert.False(session.Update(new DesktopInput(400, 300, false, Time: elapsed)));
        Assert.False(DesktopTheme.Palette.IsDark);

        // From 18:58:50 to 19:00:00.
        now = now.AddSeconds(70);
        elapsed += TimeSpan.FromMilliseconds(16);
        Assert.True(session.Update(new DesktopInput(400, 300, false, Time: elapsed)));
        Assert.True(DesktopTheme.Palette.IsDark);
    }

    [Fact]
    public void TheClocksOffsetMovesTheSchedule()
    {
        DesktopAppearance.Use(Evenings);

        // 18:58 UTC is 13:58 at UTC-05:00, light; at UTC+01:00 it's 19:58, dark.
        clock.SetUtcOffset(TimeSpan.FromHours(-5));
        DesktopAppearance.Refresh();
        Assert.False(DesktopTheme.Palette.IsDark);

        clock.SetUtcOffset(TimeSpan.FromHours(1));
        DesktopAppearance.Refresh();
        Assert.True(DesktopTheme.Palette.IsDark);
    }
}
