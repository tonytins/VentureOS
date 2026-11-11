using Venture.OS.Bundles;
using Venture.OS.Desktop;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Desktop;
using Venture.OS.Tests.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Bundles;

/// <summary>Opens the shipped <c>TextEdit.app</c>, one of the user's apps, on files in a temporary folder.</summary>
public sealed class TextEditAppTests : IDisposable
{
    private static readonly string BuiltIn = AppContext.BaseDirectory;

    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-textedit-{Guid.NewGuid():N}");
    private readonly RecordingOutput output = new();
    private readonly RecordingOutput events = new();
    private readonly WindowManager windows = new(1024, 768);
    private readonly AppWindows host;
    private readonly AppManager manager;

    public TextEditAppTests()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "todo.txt"), "milk\r\neggs\r\n");
        CommandRegistry registry = new();
        host = new AppWindows(windows, events);
        manager = new AppManager(
            Path.Combine(BuiltIn, "system", "apps"),
            new RuntimeCommands(registry.IsBuiltIn),
            new FakeSystemInfo(),
            windows: host,
            userAppsDirectory: Path.Combine(BuiltIn, "user", "apps"));
    }

    public void Dispose()
    {
        foreach (var app in (AppManager.RunningApp[])[.. manager.Running])
            manager.CloseApp(app.Bundle.Name, new RecordingOutput());
        Directory.Delete(root, recursive: true);
    }

    private DesktopWindow Window => windows.Windows[^1];

    [Fact]
    public void OpensNoWindowUntilAskedForOneOrAFile()
    {
        Open();
        Assert.Empty(windows.Windows);

        Assert.True(host.NewWindow("TextEdit"));
        Assert.Equal("Untitled", Window.Title);
        Assert.Equal("", Window.View.Text("text"));
        Assert.Equal("text", Window.View.FocusedControl);
    }

    [Fact]
    public void OpensAFileWithItsLineBreaksTidied()
    {
        Open();

        Assert.True(host.OpenFile("TextEdit", Path.Combine(root, "todo.txt")));

        Assert.Equal("milk\neggs\n", Window.View.Text("text"));
        Assert.Equal(Path.Combine(root, "todo.txt"), Window.View.Text("path"));
        Assert.Equal("todo.txt", Window.Title);
        Assert.Equal("Opened todo.txt.", Window.View.Text("status"));
        Assert.Empty(events.Lines);
    }

    [Fact]
    public void MarksEditsAndSaves()
    {
        Open();
        host.OpenFile("TextEdit", Path.Combine(root, "todo.txt"));

        Window.View.SetText("text", "milk\neggs\nbread\n");
        Send(new UIEvent(UIEventKind.Changed, "text", "milk\neggs\nbread\n"));
        Assert.Equal("todo.txt (edited)", Window.Title);

        Send(new UIEvent(UIEventKind.Pressed, "save"));

        Assert.Equal("milk\neggs\nbread\n", File.ReadAllText(Path.Combine(root, "todo.txt")));
        Assert.Equal("todo.txt", Window.Title);
        Assert.Equal("Saved todo.txt.", Window.View.Text("status"));
    }

    [Fact]
    public void NamesANewFileInTheTitleBarThenSavesIt()
    {
        Open();
        host.NewWindow("TextEdit");
        Window.View.SetText("text", "hello");

        Send(new UIEvent(UIEventKind.Pressed, "saveItem"));
        Assert.Equal("Type a path in the title bar, then Save.", Window.View.Text("status"));

        var path = Path.Combine(root, "hello.txt");
        Window.View.SetText("path", path);
        Send(new UIEvent(UIEventKind.Submitted, "path", path));
        Assert.Equal("A new file: Save creates hello.txt.", Window.View.Text("status"));

        Send(new UIEvent(UIEventKind.Pressed, "save"));
        Assert.Equal("hello", File.ReadAllText(path));
    }

    [Fact]
    public void EnterInTheTitleBarOpensAnExistingFile()
    {
        Open();
        host.NewWindow("TextEdit");
        var path = Path.Combine(root, "todo.txt");
        Window.View.SetText("path", path);

        Send(new UIEvent(UIEventKind.Submitted, "path", path));

        Assert.Equal("milk\neggs\n", Window.View.Text("text"));
    }

    [Fact]
    public void RefusesAFileTooLargeToEdit()
    {
        File.WriteAllText(Path.Combine(root, "big.txt"), new string('a', UIView.TextAreaLength + 1));
        Open();
        host.NewWindow("TextEdit");

        host.OpenFile("TextEdit", Path.Combine(root, "big.txt"));

        // The file gets its own window, which says why it's empty.
        Assert.Equal("big.txt is too large to edit here.", Window.View.Text("status"));
        Assert.Equal("", Window.View.Text("text"));
    }

    [Fact]
    public void TheDockGivesItAWindow()
    {
        DesktopSession session = new(1024, 768, manager, root, new HeldOutput(), windows);
        var index = session.Dock.Items.ToList().FindIndex(item => item.Name == "TextEdit");
        var icon = session.Dock.IconBounds(index);
        RecordingPainter painter = new(1024, 768);
        session.Draw(painter);

        session.Update(new DesktopInput((int)icon.X + 10, (int)icon.Y + 10, true));
        session.Update(new DesktopInput((int)icon.X + 10, (int)icon.Y + 10, false));

        Assert.True(manager.IsOpen("dev.tonytins.ventureos.textedit"));
        Assert.Equal("Untitled", Assert.Single(windows.Windows).Title);
    }

    private void Open() => Assert.True(manager.OpenApp("textedit", root, output), string.Join('\n', output.Lines));

    private void Send(UIEvent uiEvent) => Window.EventHandler!(Window, uiEvent);
}
