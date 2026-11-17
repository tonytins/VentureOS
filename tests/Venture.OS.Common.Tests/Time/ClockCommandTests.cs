using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Scripting;
using Venture.OS.Tests.Shell;
using Venture.OS.Time;

namespace Venture.OS.Tests.Time;

/// <summary>The shipped <c>clock</c> command, which shows the time and sets the offset.</summary>
public sealed class ClockCommandTests
{
    private readonly RecordingOutput output = new();
    private readonly FakeSystemInfo system = new();

    [Fact]
    public void ShowsTheTimeAndOffset()
    {
        Command("clock");

        Assert.Equal(["Time:   Tue 8:45 PM, 2026-11-17", "Offset: UTC"], output.Lines);
    }

    [Theory]
    [InlineData("UTC-05:00")]
    [InlineData("-5")]
    [InlineData("-05:00")]
    public void SetsTheOffset(string offset)
    {
        Command("clock", offset);

        Assert.Equal(TimeSpan.FromHours(-5), system.WallClock!.UtcOffset);
        Assert.Equal(["Time:   Tue 3:45 PM, 2026-11-17", "Offset: UTC-05:00"], output.Lines);
    }

    [Fact]
    public void SaysWhyAnOffsetIsWrong()
    {
        Command("clock", "+15");

        Assert.Equal(["clock: an offset from UTC is from -12:00 to +14:00, not +15:00"], output.Lines);
        Assert.Equal(TimeSpan.Zero, system.WallClock!.UtcOffset);
    }

    [Fact]
    public void SaysWhenTheHardwareClockIsntSet()
    {
        Command("clock", system: new FakeSystemInfo { WallClock = new WallClock(() => DateTime.UnixEpoch) });

        Assert.Equal(["Time:   unknown; the hardware clock isn't set", "Offset: UTC"], output.Lines);
    }

    private void Command(string name, string? argument = null, FakeSystemInfo? system = null)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "system");
        ScriptCommands source = new(Path.Combine(folder, "commands"), system ?? this.system, libraryDirectory: Path.Combine(folder, "lib"));
        Assert.True(source.TryGet(name, out var command), $"No {name}.lua shipped.");
        command.Execute(new ShellContext(output, new RecordingHost(), new CommandRegistry()), argument is null ? [] : [argument]);
    }
}
