using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Scripting;

public sealed class RuntimeCommandsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-runtime-{Guid.NewGuid():N}");
    private readonly RecordingOutput ownerOutput = new();
    private readonly RecordingOutput shellOutput = new();
    private readonly CommandRegistry registry = new();
    private readonly RuntimeCommands runtime;
    private readonly List<LuaOwner> owners = [];
    private Func<string?>? commandStop;

    public RuntimeCommandsTests()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "echo.lua"), "-- The echo script.\nprint('script echo')");

        registry.Register(new TestCommand("help"));
        ScriptCommands scripts = new(root, root);
        runtime = new RuntimeCommands(registry.IsBuiltIn, scripts, () => commandStop?.Invoke());
        registry.AddSource(runtime);
        registry.AddSource(scripts);
    }

    public void Dispose()
    {
        foreach (var owner in owners)
            owner.Dispose();

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void RegistersACommandTheShellCanRun()
    {
        var owner = Owner();

        Assert.True(owner.Run(lua => lua.DoString("""
            venture.command {
              name = "greet",
              summary = "Greet someone.",
              usage = "greet <name>",
              run = function(...) print("Hello, " .. (... or "world") .. ".") end,
            }
            """)));
        Shell("greet Tony");
        Shell("greet");

        Assert.Equal(["Hello, Tony.", "Hello, world."], shellOutput.Lines);
        Assert.True(registry.TryGet("greet", out var command));
        Assert.Equal("Greet someone.", command.Summary);
        Assert.Equal("greet <name>", command.Usage);
        Assert.Equal("init.lua", ((IRuntimeCommand)command).From);
        Assert.Contains("greet", registry.Commands.Select(listed => listed.Name));
    }

    [Fact]
    public void KeepsStateInTheOwnersInterpreterBetweenCalls()
    {
        var owner = Owner();
        owner.Run(lua => lua.DoString("""
            local count = 0
            venture.command { name = "count", run = function() count = count + 1 print(count) end }
            """));

        Shell("count");
        Shell("count");

        Assert.Equal(["1", "2"], shellOutput.Lines);
    }

    [Fact]
    public void SendsACommandsOutputToTheShellAndTheOwnersOwnOutputToTheOwner()
    {
        var owner = Owner();
        owner.Run(lua => lua.DoString("""
            print("loading")
            venture.command { name = "say", run = function() io.write("from ") print("the command") end }
            """));

        Shell("say");
        owner.Run(lua => lua.DoString("print('owner again')"));

        Assert.Equal(["loading", "owner again"], ownerOutput.Lines);
        Assert.Equal(["from the command"], shellOutput.Lines);
    }

    [Fact]
    public void RemovesACommandThroughItsHandle()
    {
        var owner = Owner();
        owner.Run(lua => lua.DoString("""
            local handle = venture.command { name = "brief", run = function() end }
            print(handle.name)
            handle:remove()
            handle:remove()
            """));

        Assert.Equal(["brief"], ownerOutput.Lines);
        Assert.False(registry.TryGet("brief", out _));
    }

    [Fact]
    public void RemovesEveryCommandWhenTheOwnerEnds()
    {
        var owner = Owner();
        owner.Run(lua => lua.DoString("""
            venture.command { name = "one", run = function() end }
            venture.command { name = "two", run = function() end }
            """));
        Assert.True(registry.TryGet("one", out var one));

        owner.Dispose();

        Assert.False(registry.TryGet("one", out _));
        Assert.False(registry.TryGet("two", out _));
        Assert.Empty(runtime.Commands);

        one.Execute(new ShellContext(shellOutput, new RecordingHost(), registry), []);
        Assert.Equal(["\"one\" is no longer registered."], shellOutput.Lines);
    }

    [Fact]
    public void UndoesNewestFirst()
    {
        var owner = Owner();
        List<string> order = [];
        owner.AddUndo(() => order.Add("first"));
        var second = owner.AddUndo(() => order.Add("second"));
        owner.AddUndo(() => order.Add("third"));

        second.Run();
        owner.Dispose();
        owner.Dispose();

        Assert.Equal(["second", "third", "first"], order);
        Assert.True(owner.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => owner.Run(_ => { }));
        Assert.Throws<ObjectDisposedException>(() => owner.AddUndo(() => { }));
    }

    [Fact]
    public void TheSameOwnerReplacesItsOwnCommand()
    {
        var owner = Owner();
        owner.Run(lua => lua.DoString("""
            venture.command { name = "v", run = function() print("one") end }
            venture.command { name = "v", run = function() print("two") end }
            """));

        Shell("v");

        Assert.Equal(["two"], shellOutput.Lines);
        Assert.Single(runtime.Commands);
    }

    [Theory]
    [InlineData("help", "\"help\" is a built-in command and can't be replaced.")]
    [InlineData("Bad Name", "\"Bad Name\" isn't a valid command name")]
    [InlineData("echo", "\"echo\" is a system command, and init.lua can't replace it.")]
    public void RefusesNamesItCantTake(string name, string message)
    {
        var owner = Owner();

        owner.Run(lua => lua.DoString($"print(pcall(venture.command, {{ name = '{name}', run = function() end }}))"));

        Assert.StartsWith("false\t", ownerOutput.Lines[0]);
        Assert.Contains(message, ownerOutput.Lines[0]);
        Assert.Empty(runtime.Commands);
    }

    [Fact]
    public void RefusesANameAnotherOwnerHas()
    {
        Owner().Run(lua => lua.DoString("venture.command { name = 'mine', run = function() end }"));
        var other = Owner("the lua prompt", mayReplaceScripts: true);

        other.Run(lua => lua.DoString("print(pcall(venture.command, { name = 'mine', run = function() end }))"));

        Assert.Contains("\"mine\" is already registered by init.lua.", ownerOutput.Lines[0]);
    }

    [Fact]
    public void LetsTheUserReplaceASystemScriptUntilTheOwnerEnds()
    {
        var prompt = Owner("the lua prompt", "you exit", mayReplaceScripts: true);

        prompt.Run(lua => lua.DoString("venture.command { name = 'echo', run = function() print('mine') end }"));
        Shell("echo");
        registry.TryGet("echo", out var replaced);
        prompt.Dispose();
        Shell("echo");

        Assert.Equal(["echo now replaces the echo script until you exit."], ownerOutput.Lines);
        Assert.Equal(["mine", "script echo"], shellOutput.Lines);
        Assert.Equal("the lua prompt, replacing the echo script", ((IRuntimeCommand)replaced!).From);
    }

    [Theory]
    [InlineData("venture.command('greet')", "needs a table")]
    [InlineData("venture.command({ run = function() end })", "needs a name")]
    [InlineData("venture.command({ name = 'x' })", "needs a run function for \"x\"")]
    [InlineData("venture.command({ name = 'x', run = 'not a function' })", "needs a run function")]
    public void ExplainsAMalformedRegistration(string code, string message)
    {
        var owner = Owner();

        owner.Run(lua => lua.DoString($"print(pcall(function() {code} end))"));

        Assert.Contains(message, Assert.Single(ownerOutput.Lines));
    }

    [Fact]
    public void NeedsTheShellScope()
    {
        var owner = Owner(scopes: ScriptScopes.System);

        owner.Run(lua => lua.DoString("print(pcall(venture.command, { name = 'x', run = function() end }))"));

        Assert.Contains(VentureLibrary.UndeclaredMessage("command", ScriptScopes.Shell), ownerOutput.Lines[0]);
    }

    [Fact]
    public void ExplainsWhyItDoesntWorkInOneShotCode()
    {
        LuaRunner.Run(
            ownerOutput,
            root,
            lua => lua.DoString("print(pcall(venture.command, { name = 'x', run = function() end }))"),
            new FakeSystemInfo());

        Assert.Contains(VentureLibrary.NotOwnedMessage("command"), ownerOutput.Lines[0]);
    }

    [Fact]
    public void ReportsACommandsErrorAndKeepsTheOwnerUsable()
    {
        var owner = Owner();
        owner.Run(lua => lua.DoString("""
            venture.command { name = "fail", run = function() error("broken", 0) end }
            venture.command { name = "quit", run = function() os.exit(4) end }
            venture.command { name = "fine", run = function() print("fine") end }
            """));

        Shell("fail");
        Shell("quit");
        Shell("fine");

        Assert.Equal(["Lua error: broken", "Lua exited with status 4.", "fine"], shellOutput.Lines);
    }

    [Fact]
    public async Task StopsACommandWithItsOwnStopNotTheOwnersStartupLimit()
    {
        var owner = Owner();
        owner.Run(
            lua => lua.DoString("venture.command { name = 'spin', run = function() while true do end end }"),
            () => "Stopped after 10 seconds.");

        var calls = 0;
        commandStop = () => ++calls == 3 ? "Interrupted." : null;
        var token = TestContext.Current.CancellationToken;
        var run = Task.Run(() => Shell("spin"), token);
        var finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(10), token));
        Assert.True(finished == run, "The command didn't stop.");
        await run;

        commandStop = null;
        owner.Run(lua => lua.DoString("print('owner still runs')"));

        Assert.Equal(["Lua error: Interrupted."], shellOutput.Lines);
        Assert.Equal(["owner still runs"], ownerOutput.Lines);
    }

    private LuaOwner Owner(
        string name = "init.lua",
        string lifetime = "shutdown",
        bool mayReplaceScripts = false,
        ScriptScopes scopes = ScriptScopes.All)
    {
        LuaOwner owner = new(name, lifetime, mayReplaceScripts, ownerOutput, root, new FakeSystemInfo(), scopes,
            commands: runtime);
        owners.Add(owner);
        return owner;
    }

    private void Shell(string line) =>
        new CommandShell(new ShellContext(shellOutput, new RecordingHost(), registry)).Execute(line);
}
