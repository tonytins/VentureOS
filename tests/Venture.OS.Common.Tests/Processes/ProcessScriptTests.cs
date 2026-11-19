using Venture.OS.Bundles;
using Venture.OS.Processes;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Processes;

/// <summary><c>venture.processes</c> and <c>venture.kill</c>, and the shipped <c>ps</c> and <c>kill</c> commands.</summary>
public sealed class ProcessScriptTests : IDisposable
{
    private static readonly string CommandsDirectory = Path.Combine(AppContext.BaseDirectory, "system", "commands");

    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-ps-{Guid.NewGuid():N}");
    private readonly RecordingOutput output = new();
    private readonly Supervisor supervisor;
    private readonly FakeSystemInfo system;
    private TimeSpan now = TimeSpan.FromSeconds(10);

    public ProcessScriptTests()
    {
        supervisor = new Supervisor(() => now);
        system = new FakeSystemInfo { Processes = supervisor };
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void ListsTheRunningApps()
    {
        supervisor.Start(App("Files"), RestartPolicy.Always, _ => null);
        now += TimeSpan.FromSeconds(185);
        supervisor.Start(App("Notes"), RestartPolicy.Never, _ => null);
        now += TimeSpan.FromSeconds(7);

        Lua("""
            for _, p in ipairs(venture.processes()) do
              print(p.id, p.name, p.identifier, p.state, p.seconds, p.kept)
            end
            """);

        Assert.Equal(["1\tFiles\tdev.example.files\trunning\t192\ttrue", "2\tNotes\tdev.example.notes\trunning\t7\tfalse"], output.Lines);
    }

    [Fact]
    public void ListsNothingWithoutApps()
    {
        LuaRunner.Run(output, Path.GetTempPath(), lua => lua.DoString("print(venture.processes())"), new FakeSystemInfo());

        Assert.Equal(["nil"], output.Lines);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("notes")]
    [InlineData("DEV.EXAMPLE.NOTES")]
    public void KillsAnAppByIdNameOrIdentifier(string target)
    {
        var notes = supervisor.Start(App("Notes"), RestartPolicy.Never, _ => null);
        var argument = target == "1" ? "1" : $"\"{target}\"";

        Lua($"print(venture.kill({argument}))");

        Assert.Equal(["true\tfalse"], output.Lines);
        Assert.Equal(new ProcessEnding(EndingKind.Killed, "Notes was killed."), notes.Ending);
    }

    [Fact]
    public void SaysAKeptAppStartsAgain()
    {
        supervisor.Start(App("Files"), RestartPolicy.Always, _ => null);

        Lua("print(venture.kill('Files'))");

        Assert.Equal(["true\ttrue"], output.Lines);
        Assert.True(supervisor.Tick());
        Assert.Equal(2, Assert.Single(supervisor.Processes).Id);
    }

    [Fact]
    public void RefusesWhatIsNotRunning()
    {
        Lua("print(venture.kill(7)) print(venture.kill('Nothing'))");

        Assert.Equal(["nil\tthere's no running app 7; ps lists them", "nil\tthere's no running app Nothing; ps lists them"], output.Lines);
    }

    [Fact]
    public void RefusesToEndTheAppAsking()
    {
        var terminal = supervisor.Start(App("Terminal"), RestartPolicy.Never, _ => null);

        terminal.Run(() => Lua("print(venture.kill('Terminal'))"));

        Assert.Equal(["nil\tTerminal is running the code asking, and can't end from inside itself"], output.Lines);
        Assert.True(terminal.IsAlive);
    }

    [Fact]
    public void KillNeedsTheSystemScope()
    {
        supervisor.Start(App("Notes"), RestartPolicy.Never, _ => null);

        Assert.False(LuaRunner.Run(output, Path.GetTempPath(), lua => lua.DoString("venture.kill('Notes')"), system, scopes: ScriptScopes.Files));

        Assert.Single(supervisor.Processes);
        Assert.Contains(output.Lines, line => line.Contains("venture.kill needs the system scope", StringComparison.Ordinal));
    }

    [Fact]
    public void PsListsThemInColumns()
    {
        supervisor.Start(App("Files"), RestartPolicy.Always, _ => null);
        now += TimeSpan.FromSeconds(3725);
        supervisor.Start(App("System Monitor"), RestartPolicy.Never, _ => null);
        now += TimeSpan.FromSeconds(42);

        Command("ps");

        Assert.Equal([
            "  ID  NAME            STATE           UP  KEPT",
            "   1  Files           running     1h 02m  yes",
            "   2  System Monitor  running        42s  ",
        ], output.Lines);
    }

    [Fact]
    public void PsSaysWhenNothingIsRunning()
    {
        Command("ps");

        Assert.Equal(["No apps are running."], output.Lines);
    }

    [Fact]
    public void KillEndsOneAndSaysWhenItComesBack()
    {
        var notes = supervisor.Start(App("Notes"), RestartPolicy.Never, _ => null);
        supervisor.Start(App("Files"), RestartPolicy.Always, _ => null);

        Command("kill", "Notes");
        Command("kill", "files");
        Command("kill", "99");

        Assert.Equal(EndingKind.Killed, notes.Ending!.Kind);
        Assert.Equal(["Ended files. VentureOS keeps it running, so it starts again.", "kill: there's no running app 99; ps lists them"],
            output.Lines);
    }

    private void Lua(string code) =>
        Assert.True(LuaRunner.Run(output, Path.GetTempPath(), lua => lua.DoString(code), system), string.Join('\n', output.Lines));

    private void Command(string name, params string[] arguments)
    {
        ScriptCommands scripts = new(CommandsDirectory, system, libraryDirectory: Path.Combine(AppContext.BaseDirectory, "system", "lib"));
        Assert.True(scripts.TryGet(name, out var command), $"No {name}.lua.");
        CommandRegistry registry = new();
        command.Execute(new ShellContext(output, new RecordingHost(), registry), arguments);
    }

    private Bundle App(string name)
    {
        var folder = Path.Combine(root, $"{name.Replace(" ", "", StringComparison.Ordinal)}.app");
        Directory.CreateDirectory(Path.Combine(folder, "Contents", "Lua"));
        var identifier = name.Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant();
        File.WriteAllText(Path.Combine(folder, "Contents", "Info.xml"),
            $"""<App version="1"><Info identifier="dev.example.{identifier}" name="{name}"/></App>""");
        File.WriteAllText(Path.Combine(folder, "Contents", "Lua", "main.lua"), "");
        return Bundle.Read(folder.Replace('\\', '/'), BundleKind.App);
    }
}
