using Venture.OS.Bundles;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Bundles;

public sealed class AppManagerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-apps-{Guid.NewGuid():N}");
    private readonly string apps;
    private readonly RecordingOutput output = new();
    private readonly CommandRegistry registry = new();
    private readonly ShellContext context;
    private readonly CommandShell shell;
    private readonly AppManager manager;

    public AppManagerTests()
    {
        apps = Path.Combine(root, "system", "apps");
        Directory.CreateDirectory(apps);
        File.WriteAllText(Path.Combine(root, "data.txt"), "hello");

        registry.Register(new TestCommand("help"));
        RuntimeCommands runtime = new(registry.IsBuiltIn);
        registry.AddSource(runtime);
        context = new ShellContext(output, new RecordingHost(), registry) { CurrentDirectory = root };
        shell = new CommandShell(context);
        manager = new AppManager(apps, runtime, new FakeSystemInfo(), runShell: shell.Execute);
    }

    public void Dispose()
    {
        foreach (var app in manager.Running.ToList())
            manager.CloseApp(app.Bundle.Identifier, output);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void OpensAnAppWhoseCommandsWorkUntilItCloses()
    {
        App("Notes", "files shell", """
            venture.command { name = "note", run = function(...) print("noted: " .. table.concat({...}, " ")) end }
            print(venture.app.name .. " is ready")
            """);

        Assert.True(manager.OpenApp("notes", root, output));
        shell.Execute("note buy milk");
        Assert.True(registry.TryGet("note", out var note));
        Assert.Equal("Notes", ((IRuntimeCommand)note).From);

        Assert.True(manager.CloseApp("Notes", output));
        shell.Execute("note again");

        Assert.Equal(["Notes is ready", "noted: buy milk", "\"note\" is not a command. Type \"help\" for a list of commands."],
            output.Lines);
        Assert.Empty(manager.Running);
    }

    [Theory]
    [InlineData("Notes")]
    [InlineData("NOTES")]
    [InlineData("notes.app")]
    [InlineData("Jotter")]
    public void FindsAnAppByFolderOrInfoName(string target)
    {
        App("Notes", "", "", name: "Jotter");

        Assert.True(manager.OpenApp(target, root, output));
        Assert.True(manager.IsOpen("dev.example.notes"));
    }

    [Fact]
    public void OpensOnlyOnce()
    {
        App("Notes", "", "print('opened')");

        manager.OpenApp("Notes", root, output);
        Assert.False(manager.OpenApp("notes", root, output));

        Assert.Equal(["opened", "Notes is already open."], output.Lines);
        Assert.Single(manager.Running);
    }

    [Fact]
    public void OpensAnAppElsewhereByPathAndSaysWhatItNeeds()
    {
        App("Mine", "files shell", "print('mine')", folder: root);

        Assert.True(manager.OpenApp("./Mine.app", root, output));

        Assert.Equal(["Mine needs shell and files.", "mine"], output.Lines);
    }

    [Fact]
    public void DoesntMentionNeedsForSystemApps()
    {
        App("Notes", "files", "");

        manager.OpenApp("Notes", root, output);

        Assert.Empty(output.Lines);
    }

    [Fact]
    public void UndoesAnAppThatFailsToOpen()
    {
        App("Broken", "shell", """
            venture.command { name = "half", run = function() end }
            error("not today", 0)
            """);

        Assert.False(manager.OpenApp("Broken", root, output));

        Assert.Equal("Lua error: not today", output.Lines[0]);
        Assert.Equal("open: Broken didn't open; see the error above.", output.Lines[^1]);
        Assert.False(registry.TryGet("half", out _));
        Assert.Empty(manager.Running);
    }

    [Fact]
    public async Task StopsAMainScriptThatNeverFinishes()
    {
        App("Spin", "", "while true do end");
        var calls = 0;
        AppManager stopping = new(apps, new RuntimeCommands(registry.IsBuiltIn), startupStop: () => () => ++calls == 3 ? "Stopped after 10 seconds." : null);

        var token = TestContext.Current.CancellationToken;
        var run = Task.Run(() => stopping.OpenApp("Spin", root, output), token);

        var finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(10), token));
        Assert.True(finished == run, "The app's main script didn't stop.");
        Assert.False(await run);
        Assert.Equal("Lua error: Stopped after 10 seconds.", output.Lines[0]);
    }

    [Fact]
    public void HoldsAnAppToItsNeeds()
    {
        App("Nosy", "", "print(pcall(venture.files.list)) print(pcall(venture.command, { name = 'x', run = print }))");

        manager.OpenApp("Nosy", root, output);

        Assert.Contains(VentureLibrary.UndeclaredMessage("files.list", ScriptScopes.Files), output.Lines[0]);
        Assert.Contains(VentureLibrary.UndeclaredMessage("command", ScriptScopes.Shell), output.Lines[1]);
    }

    [Fact]
    public void RunsFromTheCurrentFolderAndKnowsItsBundle()
    {
        var path = App("Reader", "files", """
            print(venture.files.read("data.txt"))
            print(venture.app.identifier, venture.app.version)
            print(venture.app.resource("main.uix"))
            print(pcall(venture.app.resource, "../Info.xml"))
            """);

        manager.OpenApp("Reader", root, output);

        Assert.Equal("hello", output.Lines[0]);
        Assert.Equal("dev.example.reader\t1.0", output.Lines[1]);
        Assert.Equal($"{path}/Contents/Resources/main.uix", output.Lines[2]);
        Assert.StartsWith("false\tventure.app.resource needs a path inside Contents/Resources", output.Lines[3]);
    }

    [Fact]
    public void LoadsItsOwnModulesThenTheSystemLibrary()
    {
        var library = Path.Combine(AppContext.BaseDirectory, "system", "lib");
        AppManager withLibrary = new(apps, new RuntimeCommands(registry.IsBuiltIn), libraryDirectory: library);
        var path = App("Modular", "", """
            local words = require("words")
            print(words.greeting, type(require("venture.args").parse))
            """);
        File.WriteAllText(Path.Combine(path, "Contents", "Lua", "words.lua"), "return { greeting = 'hi from a module' }");

        Assert.True(withLibrary.OpenApp("Modular", root, output));

        Assert.Equal(["hi from a module\tfunction"], output.Lines);
    }

    [Fact]
    public void WontCloseAnAppFromItsOwnRunningCode()
    {
        App("Selfish", "shell", "venture.command { name = 'quit-me', run = function() venture.shell('close Selfish') end }");
        registry.Register(new CloseProbe(manager));
        manager.OpenApp("Selfish", root, output);

        shell.Execute("quit-me");

        Assert.Equal(["close: Selfish can't close while its own code is running."], output.Lines);
        Assert.True(manager.IsOpen("dev.example.selfish"));
    }

    [Fact]
    public void ExplainsWhatItCantOpenOrClose()
    {
        Directory.CreateDirectory(Path.Combine(apps, "Empty.app"));

        manager.OpenApp("Nothing", root, output);
        manager.OpenApp("Empty", root, output);
        manager.OpenApp("./missing/Thing.app", root, output);
        manager.CloseApp("Notes", output);

        Assert.Equal(
            [
                "open: there's no app called Nothing. Type \"apps\" to see them.",
                "open: Empty.app has no Contents/Info.xml, so it isn't a bundle.",
                "open: there's no app called ./missing/Thing.app. Type \"apps\" to see them.",
                "close: Notes isn't open.",
            ],
            output.Lines);
    }

    [Fact]
    public void ListsTheAppsFolderWithAnyThatCantBeRead()
    {
        App("Notes", "", "");
        Directory.CreateDirectory(Path.Combine(apps, "Broken.app"));
        Directory.CreateDirectory(Path.Combine(apps, "readme"));

        var listings = manager.Available();

        Assert.Equal(["Broken.app", "Notes.app"], listings.Select(listing => listing.Folder));
        Assert.Null(listings[0].Bundle);
        Assert.Contains("isn't a bundle", listings[0].Problem);
        Assert.Equal("Notes", listings[1].Bundle!.Name);
    }

    [Theory]
    [InlineData(ScriptScopes.None, "nothing extra")]
    [InlineData(ScriptScopes.Files, "files")]
    [InlineData(ScriptScopes.Files | ScriptScopes.Shell, "shell and files")]
    [InlineData(ScriptScopes.All, "system, network, shell, files and ui")]
    public void DescribesNeeds(ScriptScopes needs, string expected) =>
        Assert.Equal(expected, AppManager.DescribeNeeds(needs));

    private string App(string folderName, string needs, string main, string? name = null, string? folder = null)
    {
        var path = Path.Combine(folder ?? apps, $"{folderName}.app");
        Directory.CreateDirectory(Path.Combine(path, "Contents", "Lua"));
        File.WriteAllText(Path.Combine(path, "Contents", "Info.xml"),
            $"<App version='1'><Info identifier='dev.example.{folderName.ToLowerInvariant()}' name='{name ?? folderName}' version='1.0' needs='{needs}'/></App>");
        File.WriteAllText(Path.Combine(path, "Contents", "Lua", "main.lua"), main);
        return path;
    }

    /// <summary>A built-in close, as the kernel has, so an app's code can try to close itself.</summary>
    private sealed class CloseProbe(AppManager manager) : IShellCommand
    {
        public string Name => "close";
        public string Summary => "Close an app.";
        public string Usage => "close <app>";
        public int MinArguments => 1;
        public int MaxArguments => 1;

        public void Execute(ShellContext context, IReadOnlyList<string> arguments) =>
            manager.CloseApp(arguments[0], context.Output);
    }
}
