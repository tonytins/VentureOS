using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Scripting;

/// <summary>
/// Runs the command scripts that ship in <c>src/Venture.OS/system/commands</c>, copied next to the tests.
/// </summary>
public sealed class SystemScriptTests
{
    private static readonly string CommandsDirectory = Path.Combine(AppContext.BaseDirectory, "system", "commands");

    private readonly RecordingOutput output = new();

    [Fact]
    public void EveryShippedScriptHasASummaryAndUsage()
    {
        var commands = Source(new FakeSystemInfo()).Commands;

        Assert.NotEmpty(commands);
        foreach (var command in commands)
        {
            Assert.NotEqual(ScriptCommands.NoSummary, command.Summary);
            Assert.StartsWith(command.Name, command.Usage);
        }
    }

    [Fact]
    public void EchoJoinsItsArguments()
    {
        Run("echo", new FakeSystemInfo(), "a  b", "c");

        Assert.Equal(["a  b c"], output.Lines);
    }

    [Fact]
    public void EchoWithoutArgumentsPrintsAnEmptyLine()
    {
        Run("echo", new FakeSystemInfo());

        Assert.Equal([""], output.Lines);
    }

    [Fact]
    public void InfoShowsTheVersionUptimeAndMemory()
    {
        Run("info", new FakeSystemInfo());

        Assert.Equal(
            [
                "VentureOS 1.2.3 (Build 456)",
                "Commit:  abc1234",
                "Cosmos:  3.0.94",
                "Uptime:  01:02:05",
                "Memory:  100 MiB free of 512 MiB",
            ],
            output.Lines);
    }

    [Fact]
    public void InfoShowsDaysOfUptime()
    {
        Run("info", new FakeSystemInfo { Uptime = new TimeSpan(2, 3, 4, 5) });

        Assert.Contains("Uptime:  2d 03:04:05", output.Lines);
    }

    [Fact]
    public void InfoShowsUnknownValues()
    {
        Run("info", new FakeSystemInfo { Uptime = null, Memory = null });

        Assert.Contains("Uptime:  unknown", output.Lines);
        Assert.Contains("Memory:  unknown", output.Lines);
    }

    [Fact]
    public void NetShowsTheAdapterAndAddress()
    {
        Run("net", new FakeSystemInfo());

        Assert.Equal(
            [
                "Device:   e1000e",
                "MAC:      52:54:00:12:34:56",
                "Link up:  yes",
                "Ready:    no",
                "Address:  10.0.2.15",
                "Subnet:   255.255.255.0",
                "Gateway:  10.0.2.2",
            ],
            output.Lines);
    }

    [Fact]
    public void NetShowsWhenIpv4IsNotConfigured()
    {
        Run("net", new FakeSystemInfo { Network = new("e1000e", "mac", true, true, null, null, null) });

        Assert.Equal("IPv4:     not configured", output.Lines[^1]);
        Assert.Equal(5, output.Lines.Count);
    }

    [Fact]
    public void NetShowsWhenThereIsNoDevice()
    {
        Run("net", new FakeSystemInfo { Network = null });

        Assert.Equal(["No network device."], output.Lines);
    }

    [Fact]
    public void InitPrintsTheHelpHint()
    {
        var init = Path.Combine(AppContext.BaseDirectory, "system", "init.lua");

        var finished = LuaRunner.Run(output, Path.GetTempPath(), lua => lua.DoFile(init), new FakeSystemInfo());

        Assert.True(finished);
        Assert.Equal(["Type \"help\" for a list of commands."], output.Lines);
    }

    private ScriptCommands Source(ISystemInfo system) => new(CommandsDirectory, system);

    private void Run(string name, ISystemInfo system, params string[] arguments)
    {
        Assert.True(Source(system).TryGet(name, out var command), $"No {name}.lua in {CommandsDirectory}.");

        CommandRegistry registry = new();
        command.Execute(new ShellContext(output, new RecordingHost(), registry), arguments);
    }
}
