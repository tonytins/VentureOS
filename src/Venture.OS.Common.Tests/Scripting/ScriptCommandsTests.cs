using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Scripting;

public sealed class ScriptCommandsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-scripts-{Guid.NewGuid():N}");
    private readonly string commandsDirectory;
    private readonly RecordingOutput output = new();
    private readonly CommandRegistry registry = new();

    public ScriptCommandsTests()
    {
        commandsDirectory = Path.Combine(root, "commands");
        Directory.CreateDirectory(commandsDirectory);
    }

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public void FindsAScriptByName()
    {
        Script("hello", "print('hi')");

        Assert.True(Source().TryGet("hello", out var command));
        Assert.Equal("hello", command.Name);
    }

    [Fact]
    public void ReportsMissingScripts() => Assert.False(Source().TryGet("nope", out _));

    [Theory]
    [InlineData("../escape")]
    [InlineData("Hello")]
    [InlineData("a/b")]
    [InlineData("")]
    public void LooksUpOnlyValidNames(string name)
    {
        File.WriteAllText(Path.Combine(root, "escape.lua"), "print('escaped')");

        Assert.False(Source().TryGet(name, out _));
    }

    [Fact]
    public void ReadsTheSummaryAndUsageFromTheLeadingComments()
    {
        Script("echo", "-- Print the arguments.\n-- Usage: echo [text...]\n-- More detail.\nprint(...)\n-- Usage: ignored");

        Source().TryGet("echo", out var command);

        Assert.Equal("Print the arguments.", command!.Summary);
        Assert.Equal("echo [text...]", command.Usage);
    }

    [Fact]
    public void DefaultsTheSummaryAndUsage()
    {
        Script("bare", "print('no comments')");

        Source().TryGet("bare", out var command);

        Assert.Equal(ScriptCommands.NoSummary, command!.Summary);
        Assert.Equal("bare [arguments]", command.Usage);
    }

    [Fact]
    public void RunsTheScriptWithItsArguments()
    {
        Script("echo", "print(table.concat({...}, ' '))");
        Source().TryGet("echo", out var command);

        command!.Execute(Context(), ["a  b", "c"]);

        Assert.Equal(["a  b c"], output.Lines);
    }

    [Fact]
    public void ReportsAScriptErrorInsteadOfThrowing()
    {
        Script("broken", "error('it broke')");
        Source().TryGet("broken", out var command);

        command!.Execute(Context(), []);

        Assert.Contains("it broke", Assert.Single(output.Lines, line => line.StartsWith("Lua error: ")));
    }

    [Fact]
    public void GivesAScriptOnlyTheScopesItNeeds()
    {
        Script("peek", "-- Needs: system\nprint(pcall(venture.uptime)) print((pcall(venture.net)))");
        Source(new FakeSystemInfo()).TryGet("peek", out var command);

        command!.Execute(Context(), []);

        Assert.Equal(ScriptScopes.System, ((IScriptCommand)command).Needs);
        Assert.Equal(["true\t3725", "false"], output.Lines);
    }

    [Fact]
    public void RefusesToRunAScriptThatNeedsAnUnknownScope()
    {
        Script("future", "-- Needs: network, teleporter\nprint('ran')");
        Source().TryGet("future", out var command);

        command!.Execute(Context(), []);

        var line = Assert.Single(output.Lines);
        Assert.StartsWith("future can't run. Its -- Needs: line: ", line);
        Assert.Contains("teleporter", line);
        Assert.NotNull(((IScriptCommand)command).HeaderError);
    }

    [Fact]
    public void RunsFromTheShellsCurrentFolder()
    {
        var folder = Path.Combine(root, "work");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "data.lua"), "return 'found it'");
        Script("where", "print(dofile('data.lua'))");
        Source().TryGet("where", out var command);
        var context = Context();
        context.CurrentDirectory = folder;

        command!.Execute(context, []);

        Assert.Equal(["found it"], output.Lines);
    }

    [Fact]
    public void ListsValidScriptsOnly()
    {
        Script("beta", "-- Second.");
        Script("alpha", "-- First.");
        File.WriteAllText(Path.Combine(commandsDirectory, "notes.txt"), "not a script");
        File.WriteAllText(Path.Combine(commandsDirectory, "bad_name.lua"), "print()");

        var names = Source().Commands.Select(command => command.Name).Order(StringComparer.Ordinal);

        Assert.Equal(["alpha", "beta"], names);
    }

    [Fact]
    public void HasNoCommandsWhenTheDirectoryIsMissing()
    {
        ScriptCommands source = new(Path.Combine(root, "missing"));

        Assert.Empty(source.Commands);
        Assert.False(source.TryGet("echo", out _));
    }

    [Fact]
    public void FindsScriptsAddedAfterItWasCreated()
    {
        var source = Source();
        Assert.False(source.TryGet("late", out _));

        Script("late", "print('here')");

        Assert.True(source.TryGet("late", out _));
    }

    [Fact]
    public void BuiltInCommandsWinOverScripts()
    {
        TestCommand builtIn = new("echo");
        registry.Register(builtIn);
        Script("echo", "-- From a script.");
        registry.AddSource(Source());

        Assert.True(registry.TryGet("echo", out var found));
        Assert.Same(builtIn, found);
        Assert.Same(builtIn, Assert.Single(registry.Commands));
    }

    [Fact]
    public void TheShellRunsScriptCommandsByAnyCase()
    {
        Script("echo", "print(table.concat({...}, ' '))");
        registry.AddSource(Source());

        new CommandShell(Context()).Execute(@"ECHO ""hello there""");

        Assert.Equal(["hello there"], output.Lines);
    }

    [Fact]
    public void RejectsInvalidArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new ScriptCommands(null!));
        Assert.Throws<ArgumentException>(() => new ScriptCommands(""));
        Assert.Throws<ArgumentNullException>(() => Source().TryGet(null!, out _));
    }

    private ScriptCommands Source(ISystemInfo? system = null) => new(commandsDirectory, system);

    private ShellContext Context() => new(output, new RecordingHost(), registry);

    private void Script(string name, string code) =>
        File.WriteAllText(Path.Combine(commandsDirectory, $"{name}.lua"), code);
}
