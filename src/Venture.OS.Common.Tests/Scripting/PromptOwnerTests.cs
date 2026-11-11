using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Scripting;

/// <summary>
/// The <c>lua</c> prompt as an owner: commands it registers, <c>!</c>, and <c>venture.shell</c>.
/// </summary>
public sealed class PromptOwnerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-prompt-{Guid.NewGuid():N}");
    private readonly RecordingOutput output = new();
    private readonly CommandRegistry registry = new();
    private readonly RuntimeCommands runtime;
    private readonly CommandShell shell;

    public PromptOwnerTests()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "echo.lua"), "-- The echo script.\nprint('script: ' .. table.concat({...}, ' '))");

        registry.Register(new TestCommand("help"));
        ScriptCommands scripts = new(root);
        runtime = new RuntimeCommands(registry.IsBuiltIn, scripts);
        registry.AddSource(runtime);
        registry.AddSource(scripts);
        shell = new CommandShell(new ShellContext(output, new RecordingHost(), registry));
    }

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public void RunsItsCommandsWithBangAndRemovesThemOnExit()
    {
        Run(
            "venture.command { name = 'greet', run = function(name) print('Hello, ' .. name .. '.') end }",
            "!greet Tony",
            "exit");

        Assert.Equal([LuaRepl.Greeting, "command \"greet\"", "Hello, Tony."], output.Lines);
        Assert.False(registry.TryGet("greet", out _));
    }

    [Fact]
    public void RemovesItsCommandsWhenTheInputEnds()
    {
        Run("venture.command { name = 'greet', run = function() end }");

        Assert.Empty(runtime.Commands);
    }

    [Fact]
    public void RunsShellCommandsFromLua()
    {
        Run(
            "venture.command { name = 'twice', run = function(word) print(word) print(word) end }",
            "venture.shell('twice hi')",
            "print('after')");

        Assert.Equal([LuaRepl.Greeting, "command \"twice\"", "hi", "hi", "after"], output.Lines);
    }

    [Fact]
    public void KeepsTheLinesStopAfterANestedCommand()
    {
        var calls = 0;

        // The line calls one of the session's own commands, then loops; the stop must still reach the loop.
        Run(
            () => ++calls == 5 ? "Interrupted." : null,
            "venture.command { name = 'quick', run = function() end }",
            "venture.shell('quick') while true do end",
            "print('back')");

        Assert.Equal([LuaRepl.Greeting, "command \"quick\"", "Lua error: Interrupted.", "back"], output.Lines);
    }

    [Fact]
    public void ReplacesASystemScriptUntilExit()
    {
        Run(
            "venture.command { name = 'echo', run = function() print('mine') end }",
            "!echo a",
            "exit");
        shell.Execute("echo a");

        Assert.Equal(
            [LuaRepl.Greeting, "echo now replaces the echo script until you exit.", "command \"echo\"", "mine", "script: a"],
            output.Lines);
    }

    [Fact]
    public void SaysWhereItsCommandsComeFrom()
    {
        string? from = null;
        Run(
            "venture.command { name = 'greet', run = function() end }",
            "exit");
        Run(
            () => null,
            [
                "venture.command { name = 'greet', run = function() end }",
                "x = 1",
            ],
            afterEachLine: () => from ??= registry.TryGet("greet", out var command)
                ? ((IRuntimeCommand)command).From
                : null);

        Assert.Equal(LuaRepl.OwnerName, from);
    }

    [Fact]
    public void StopsACommandThatRunsItselfForever()
    {
        Run(
            "venture.command { name = 'again', run = function() venture.shell('again') end }",
            "!again",
            "print('still here')");

        Assert.Contains(output.Lines, line => line.Contains($"nested more than {ShellLibrary.MaxShellDepth} deep"));
        Assert.Equal("still here", output.Lines[^1]);
    }

    [Fact]
    public void ReportsAShellCommandThatThrowsAndStays()
    {
        registry.Register(new TestCommand("boom") { ExceptionToThrow = new InvalidOperationException("disk on fire") });

        Run("!boom", "print('still here')");

        Assert.Equal(
            [LuaRepl.Greeting, "Command failed (InvalidOperationException): disk on fire", "still here"],
            output.Lines);
    }

    [Fact]
    public void ExplainsWhenThereIsNoShell()
    {
        Queue<string> input = new(["!help"]);
        LuaRepl.Run(output, _ => input.TryDequeue(out var line) ? line : null, root);

        Assert.Equal([LuaRepl.Greeting, "Shell commands aren't available here."], output.Lines);
    }

    [Fact]
    public void IgnoresABareBang()
    {
        Run("!", "  !  ", "print('ok')");

        Assert.Equal([LuaRepl.Greeting, "ok"], output.Lines);
    }

    private void Run(params string[] lines) => Run(null, lines);

    private void Run(Func<string?>? stop, params string[] lines) => Run(stop, lines, afterEachLine: null);

    private void Run(Func<string?>? stop, string[] lines, Action? afterEachLine)
    {
        Queue<string> input = new(lines);
        var run = Task.Run(() => LuaRepl.Run(
            output,
            _ =>
            {
                afterEachLine?.Invoke();
                return input.TryDequeue(out var line) ? line : null;
            },
            root,
            new FakeSystemInfo(),
            stop,
            commands: runtime,
            runShell: shell.Execute));

        Assert.True(run.Wait(TimeSpan.FromSeconds(10)), "The prompt didn't finish within 10 seconds.");
        run.GetAwaiter().GetResult();
    }
}
