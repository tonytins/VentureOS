using Venture.OS.Time;

namespace Venture.OS.Tests.Time;

/// <summary>The clock: reading offsets as people write them, keeping the choice, and the time as the bar shows it.</summary>
public sealed class WallClockTests : IDisposable
{
    private static readonly DateTime Tuesday = new(2026, 11, 17, 20, 45, 30, DateTimeKind.Utc);
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"venture-clock-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
    }

    [Theory]
    [InlineData("-05:00", -300)]
    [InlineData("+5:30", 330)]
    [InlineData("-5", -300)]
    [InlineData("UTC-05:00", -300)]
    [InlineData("utc+05:45", 345)]
    [InlineData("UTC", 0)]
    [InlineData("0", 0)]
    [InlineData("+14:00", 840)]
    [InlineData("-12", -720)]
    [InlineData(" −03:00 ", -180)]
    public void ReadsOffsetsAsPeopleWriteThem(string text, int minutes)
    {
        Assert.True(WallClock.TryParseOffset(text, out var offset, out var problem), problem);
        Assert.Equal(TimeSpan.FromMinutes(minutes), offset);
    }

    [Theory]
    [InlineData("", "none was given")]
    [InlineData("5", "written like")]
    [InlineData("-5:3", "written like")]
    [InlineData("-123", "written like")]
    [InlineData("EST", "written like")]
    [InlineData("+15", "from -12:00 to +14:00")]
    [InlineData("-12:30", "from -12:00 to +14:00")]
    [InlineData("+05:20", "quarter hours")]
    public void RefusesOffsetsAClockCantHave(string text, string reason)
    {
        Assert.False(WallClock.TryParseOffset(text, out _, out var problem));
        Assert.Contains(reason, problem);
    }

    [Fact]
    public void WritesOffsetsAndZones()
    {
        Assert.Equal("-05:00", WallClock.FormatOffset(TimeSpan.FromHours(-5)));
        Assert.Equal("+05:45", WallClock.FormatOffset(TimeSpan.FromMinutes(345)));
        Assert.Equal("+00:00", WallClock.FormatOffset(TimeSpan.Zero));
        Assert.Equal("UTC-05:00", WallClock.FormatZone(TimeSpan.FromHours(-5)));
        Assert.Equal("UTC", WallClock.FormatZone(TimeSpan.Zero));
    }

    [Theory]
    [InlineData(20, 45, "Tue 8:45 PM")]
    [InlineData(0, 5, "Tue 12:05 AM")]
    [InlineData(12, 0, "Tue 12:00 PM")]
    [InlineData(9, 7, "Tue 9:07 AM")]
    public void ShowsTheTimeAsTigersBarDid(int hour, int minute, string shown) =>
        Assert.Equal(shown, WallClock.FormatMenuTime(new DateTime(2026, 11, 17, hour, minute, 0)));

    [Fact]
    public void MovesTheHardwareClockByTheOffset()
    {
        WallClock clock = new(() => Tuesday);

        clock.SetUtcOffset(TimeSpan.FromHours(-5));

        Assert.Equal(Tuesday, clock.UtcNow);
        Assert.Equal(new DateTime(2026, 11, 17, 15, 45, 30), clock.Now);
        Assert.Equal("Tue 3:45 PM, UTC-05:00", clock.Describe());
    }

    [Fact]
    public void AHardwareClockThatWasNeverSetIsntATime()
    {
        WallClock clock = new(() => DateTime.UnixEpoch);

        Assert.Null(clock.UtcNow);
        Assert.Null(clock.Now);
        Assert.Equal("the hardware clock isn't set; UTC", clock.Describe());
    }

    [Fact]
    public void RefusesSettingAnOffsetOutOfRange()
    {
        WallClock clock = new(() => Tuesday);

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.SetUtcOffset(TimeSpan.FromHours(15)));
        Assert.Equal(TimeSpan.Zero, clock.UtcOffset);
    }

    [Fact]
    public void KeepsTheChoiceAndReadsItBack()
    {
        var path = Path.Combine(folder, "settings", "time.xml");
        WallClock clock = new(() => Tuesday, path);

        Assert.Null(clock.SetUtcOffset(TimeSpan.FromMinutes(330)));

        WallClock later = new(() => Tuesday, path);
        var load = later.Load();
        Assert.Null(load.Problem);
        Assert.Equal(TimeSpan.FromMinutes(330), later.UtcOffset);
        Assert.Contains("utcOffset=\"+05:30\"", File.ReadAllText(path));
    }

    [Fact]
    public void WithoutAFileStaysOnUtc()
    {
        WallClock clock = new(() => Tuesday, Path.Combine(folder, "time.xml"));

        var load = clock.Load();

        Assert.Null(load.Problem);
        Assert.EndsWith("(nothing saved)", load.Description);
        Assert.Equal(TimeSpan.Zero, clock.UtcOffset);
    }

    [Theory]
    [InlineData("""<Time version="2" utcOffset="-05:00"/>""", "reads up to 1")]
    [InlineData("""<Time version="1" utcOffset="EST"/>""", "written like")]
    [InlineData("""<Clock version="1"/>""", "must be <Time>")]
    public void AFileThatCantBeReadKeepsTheOffsetAndSaysWhy(string text, string reason)
    {
        var path = Path.Combine(folder, "time.xml");
        Directory.CreateDirectory(folder);
        File.WriteAllText(path, text);
        WallClock clock = new(() => Tuesday, path);
        clock.SetUtcOffset(TimeSpan.FromHours(2));
        File.WriteAllText(path, text);

        var load = clock.Load();

        Assert.Contains(reason, load.Problem);
        Assert.Contains("Keeping UTC+02:00", load.Problem);
        Assert.Equal(TimeSpan.FromHours(2), clock.UtcOffset);
    }

    [Fact]
    public void SkipsAttributesItDoesntKnow()
    {
        Assert.Equal(TimeSpan.FromHours(-8), WallClock.Read("""<Time version="1" utcOffset="-08:00" zone="America/Los_Angeles"/>"""));
        Assert.Equal(TimeSpan.Zero, WallClock.Read("""<Time version="1"/>"""));
    }

    [Fact]
    public void SaysWhenTheChoiceCouldntBeSaved()
    {
        // A file where the folder should be.
        Directory.CreateDirectory(folder);
        var blocker = Path.Combine(folder, "settings");
        File.WriteAllText(blocker, "");
        WallClock clock = new(() => Tuesday, Path.Combine(blocker, "time.xml"));

        var problem = clock.SetUtcOffset(TimeSpan.FromHours(1));

        Assert.StartsWith("couldn't save the clock", problem);
        Assert.Equal(TimeSpan.FromHours(1), clock.UtcOffset);
    }
}
