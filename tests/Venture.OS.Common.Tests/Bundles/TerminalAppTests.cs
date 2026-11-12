using Venture.OS.Bundles;
using Venture.OS.Desktop;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Bundles;

/// <summary>
/// Opens the shipped <c>Terminal.app</c> with a shell of a few small commands: <c>say</c> prints its words,
/// <c>wipe</c> clears the screen then prints, <c>go</c> changes folder, <c>boom</c> throws, and <c>show</c> asks
/// whether there's a console.
/// </summary>
public sealed class TerminalAppTests : IDisposable
{
    private readonly RecordingOutput output = new();
    private readonly RecordingOutput events = new();
    private readonly WindowManager windows = new(1024, 768);
    private readonly AppWindows host;
    private readonly AppManager manager;

    public TerminalAppTests()
    {
        CommandRegistry registry = new();
        registry.Register(new Say());
        registry.Register(new Wipe());
        registry.Register(new Go());
        registry.Register(new Boom());
        registry.Register(new Show());
        host = new AppWindows(windows, events);
        manager = new AppManager(
            Path.Combine(AppContext.BaseDirectory, "system", "apps"),
            new RuntimeCommands(registry.IsBuiltIn),
            new FakeSystemInfo(),
            windows: host,
            openShell: (caught, directory) =>
                new ShellContext(caught, new RecordingHost(), registry) { CurrentDirectory = directory, HasConsole = false });
        Assert.True(manager.OpenApp("Terminal", "/mnt", output), string.Join('\n', output.Lines));
    }

    public void Dispose() => manager.CloseApp("Terminal", new RecordingOutput());

    private DesktopWindow Window => windows.Windows[^1];

    [Fact]
    public void OpensWithTheVersionAndAPrompt()
    {
        Assert.Equal(
            "VentureOS 1.2.3 (Build 456)\nType \"help\" for a list of commands, and \"exit\" to close this window.",
            Window.View.Text("output"));
        Assert.Equal("/mnt>", Window.View.Text("prompt"));
        Assert.Equal("command", Window.View.FocusedControl);
    }

    [Fact]
    public void RunsACommandAndShowsWhatItPrinted()
    {
        Run("say hello there");

        Assert.EndsWith("\n/mnt> say hello there\nhello there", Window.View.Text("output"));
        Assert.Equal("", Window.View.Text("command"));
        Assert.Empty(events.Lines);
    }

    [Fact]
    public void KeepsItsOwnFolder()
    {
        Run("go /mnt/user");

        Assert.Equal("/mnt/user>", Window.View.Text("prompt"));
        Assert.True(host.NewWindow("Terminal"));
        Assert.Equal("/mnt>", Window.View.Text("prompt"));
    }

    [Fact]
    public void ClearingEmptiesTheWindow()
    {
        Run("say one");
        Run("wipe");
        Assert.Equal("fresh", Window.View.Text("output"));

        Send(new UIEvent(UIEventKind.Pressed, "clearItem"));
        Assert.Equal("", Window.View.Text("output"));
    }

    [Fact]
    public void ReportsAFailingCommandAndCarriesOn()
    {
        Run("boom");
        Run("nothing");

        var text = Window.View.Text("output")!;
        Assert.Contains("InvalidOperationException: The command fell over.", text);
        Assert.EndsWith("\"nothing\" is not a command. Type \"help\" for a list of commands.", text);
    }

    [Fact]
    public void ItsShellHasNoConsole()
    {
        Run("show");

        Assert.EndsWith("console: no", Window.View.Text("output"));
    }

    [Fact]
    public void ExitClosesTheWindow()
    {
        Run("exit");

        Assert.Single(windows.CloseRequested());
    }

    [Fact]
    public void KeepsOnlyItsNewestLines()
    {
        for (var line = 0; line < 300; line++)
            Run($"say {line}");

        var lines = Window.View.Text("output")!.Split('\n');
        Assert.Equal(500, lines.Length);
        Assert.Equal("299", lines[^1]);
    }

    private void Run(string line)
    {
        Window.View.SetText("command", line);
        Send(new UIEvent(UIEventKind.Submitted, "command", line));
    }

    private void Send(UIEvent uiEvent) => Window.EventHandler!(Window, uiEvent);

    private abstract class Command(string name) : IShellCommand
    {
        public string Name => name;
        public string Summary => name;
        public string Usage => name;
        public int MinArguments => 0;
        public int MaxArguments => 10;
        public abstract void Execute(ShellContext context, IReadOnlyList<string> arguments);
    }

    private sealed class Say() : Command("say")
    {
        public override void Execute(ShellContext context, IReadOnlyList<string> arguments) =>
            context.Output.WriteLine(string.Join(' ', arguments));
    }

    private sealed class Wipe() : Command("wipe")
    {
        public override void Execute(ShellContext context, IReadOnlyList<string> arguments)
        {
            context.Output.WriteLine("gone");
            context.Output.Clear();
            context.Output.WriteLine("fresh");
        }
    }

    private sealed class Go() : Command("go")
    {
        public override void Execute(ShellContext context, IReadOnlyList<string> arguments) =>
            context.CurrentDirectory = arguments[0];
    }

    private sealed class Boom() : Command("boom")
    {
        public override void Execute(ShellContext context, IReadOnlyList<string> arguments) =>
            throw new InvalidOperationException("The command fell over.");
    }

    private sealed class Show() : Command("show")
    {
        public override void Execute(ShellContext context, IReadOnlyList<string> arguments) =>
            context.Output.WriteLine(context.HasConsole ? "console: yes" : "console: no");
    }
}
