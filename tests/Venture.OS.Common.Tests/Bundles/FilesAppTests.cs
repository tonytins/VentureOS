using Venture.OS.Bundles;
using Venture.OS.Desktop;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Bundles;

/// <summary>
/// Opens the shipped <c>Files.app</c> from <c>rootfs/system/apps</c> on a temporary folder holding a text
/// file, a folder, and an app.
/// </summary>
public sealed class FilesAppTests : IDisposable
{
    private static readonly string SystemFiles = Path.Combine(AppContext.BaseDirectory, "system");

    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-files-{Guid.NewGuid():N}");
    private readonly RecordingOutput output = new();
    private readonly RecordingOutput events = new();
    private readonly WindowManager windows = new(1024, 768);
    private readonly AppWindows host;
    private readonly AppManager manager;

    public FilesAppTests()
    {
        Directory.CreateDirectory(Path.Combine(root, "photos"));
        File.WriteAllText(Path.Combine(root, "notes.txt"), "milk\neggs\n");
        File.WriteAllText(Path.Combine(root, "photos", "beach.txt"), "sand\n");
        var dice = Path.Combine(root, "Dice.app", "Contents");
        Directory.CreateDirectory(Path.Combine(dice, "Lua"));
        File.WriteAllText(Path.Combine(dice, "Info.xml"), """<App version="1"><Info identifier="dev.example.dice" name="Dice"/></App>""");
        File.WriteAllText(Path.Combine(dice, "Lua", "main.lua"), "");

        CommandRegistry registry = new();
        host = new AppWindows(windows, events);
        manager = new AppManager(
            Path.Combine(SystemFiles, "apps"),
            new RuntimeCommands(registry.IsBuiltIn),
            new FakeSystemInfo(),
            Path.Combine(SystemFiles, "lib"),
            windows: host,
            userAppsDirectory: Path.Combine(AppContext.BaseDirectory, "user", "apps"));
        Assert.True(manager.OpenApp("Files", root, output), string.Join('\n', output.Lines));
    }

    public void Dispose()
    {
        foreach (var app in (AppManager.RunningApp[])[.. manager.Running])
            manager.CloseApp(app.Bundle.Name, new RecordingOutput());
        Directory.Delete(root, recursive: true);
    }

    private DesktopWindow Window => windows.Windows[^1];

    [Fact]
    public void OpensAWindowOnTheFolderItStartedIn()
    {
        Assert.Equal(Path.GetFileName(root), Window.Title);
        Assert.Equal(root, Window.View.Text("address"));
        Assert.Equal(["Dice.app", "notes.txt", "photos/"], Window.View.List("entries")!.Items);
        Assert.Equal("3 items", Window.View.Text("status"));
        Assert.True(Window.View.IsDisabled("openItem"));
        Assert.True(Window.View.IsDisabled("lookItem"));
        Assert.Equal(["Home", "User", "Apps", "System"], Window.View.List("bookmarks")!.Items);
        Assert.Equal(-1, Window.View.List("bookmarks")!.SelectedIndex);
        Assert.Empty(events.Lines);
    }

    [Fact]
    public void OpensAFolderAndGoesBackUp()
    {
        Select(2);
        Assert.False(Window.View.IsDisabled("openItem"));

        Send(new UIEvent(UIEventKind.Pressed, "openItem"));
        Assert.Equal(["beach.txt"], Window.View.List("entries")!.Items);
        Assert.Equal("photos", Window.Title);
        Assert.Equal("1 item", Window.View.Text("status"));

        Send(new UIEvent(UIEventKind.Pressed, "enclosing"));
        Assert.Equal(root, Window.View.Text("address"));
        Assert.Empty(events.Lines);
    }

    [Fact]
    public void ADoubleClickOpensWhatItLandsOn()
    {
        Select(2);

        Send(new UIEvent(UIEventKind.Submitted, "entries", "photos/", Index: 2));

        Assert.Equal("photos", Window.Title);
    }

    [Fact]
    public void TheAddressFieldGoesToATypedFolder()
    {
        Window.View.SetText("address", "photos");
        Send(new UIEvent(UIEventKind.Submitted, "address", "photos"));
        Assert.Equal(Path.Combine(root, "photos"), Window.View.Text("address"));

        Window.View.SetText("address", root);
        Send(new UIEvent(UIEventKind.Submitted, "address", root));
        Assert.Equal(root, Window.View.Text("address"));
        Assert.Equal("3 items", Window.View.Text("status"));
    }

    [Fact]
    public void TheAddressNeedsNoGoButton()
    {
        Assert.Empty(Window.View.Script.ToolbarItems);
    }

    [Fact]
    public void AMissingFolderSaysWhyAndKeepsTheCurrentOne()
    {
        Window.View.SetText("address", "nowhere");

        Send(new UIEvent(UIEventKind.Submitted, "address", "nowhere"));

        Assert.Equal(root, Window.View.Text("address"));
        Assert.Contains("nowhere", Window.View.Text("status"));
        Assert.Equal(3, Window.View.List("entries")!.Items.Count);
    }

    [Fact]
    public void OpeningATextFileHandsItToTextEdit()
    {
        Select(1);

        Send(new UIEvent(UIEventKind.Pressed, "openItem"));

        Assert.True(manager.IsOpen("dev.tonytins.ventureos.textedit"));
        var edit = windows.Windows[^1];
        Assert.Equal("TextEdit", edit.App);
        Assert.Equal("milk\neggs\n", edit.View.Text("text"));
        Assert.Empty(events.Lines);
    }

    [Fact]
    public void QuickLookShowsAFile()
    {
        var files = Window;
        Select(1);

        Send(new UIEvent(UIEventKind.Pressed, "lookItem"));

        var preview = windows.Windows[^1];
        Assert.NotSame(files, preview);
        Assert.Equal("notes.txt", preview.Title);
        Assert.Equal(["milk", "eggs"], preview.View.List("lines")!.Items);
        Assert.False(preview.OpenedByApp);
    }

    [Fact]
    public void OpeningAnAppLaunchesItOnce()
    {
        Select(0);

        Send(new UIEvent(UIEventKind.Pressed, "openItem"));
        Assert.True(manager.IsOpen("dev.example.dice"));

        // Opening it again brings it forward instead of a second "already open".
        Send(new UIEvent(UIEventKind.Pressed, "openItem"));
        Assert.Equal(2, manager.Running.Count);
        Assert.Empty(events.Lines);
    }

    [Fact]
    public void NewWindowOpensAnotherAndCloseWindowAsksToClose()
    {
        Assert.True(host.HasNewWindow("Files"));
        Assert.True(host.NewWindow("Files"));
        Assert.Equal(2, windows.Windows.Count);

        Send(new UIEvent(UIEventKind.Pressed, "closeWindow"));

        Assert.Single(windows.CloseRequested());
    }

    private void Select(int index)
    {
        Window.View.List("entries")!.SelectedIndex = index;
        Send(new UIEvent(UIEventKind.Selected, "entries", Index: index));
    }

    private void Send(UIEvent uiEvent) => Window.EventHandler!(Window, uiEvent);
}
