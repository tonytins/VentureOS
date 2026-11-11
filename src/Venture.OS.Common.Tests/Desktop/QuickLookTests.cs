using Venture.OS.Desktop;

namespace Venture.OS.Tests.Desktop;

public sealed class QuickLookTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-look-{Guid.NewGuid():N}");

    public QuickLookTests() => Directory.CreateDirectory(root);

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public void ShowsTextAsItsLinesFromTheTop()
    {
        var path = Write("shopping.txt", "milk\r\neggs\nbread\n");

        var window = Preview(path);

        Assert.Equal("shopping.txt", window.Title);
        Assert.Equal(["milk", "eggs", "bread"], window.View.List("lines")!.Items);
        Assert.Equal(0, window.View.List("lines")!.FirstVisible);
        Assert.False(window.OpenedByApp);
    }

    [Fact]
    public void ShowsOnlyTheStartOfAHugeFile()
    {
        var path = Write("big.txt", new string('a', QuickLook.MaximumBytes + 10));

        var lines = Preview(path).View.List("lines")!.Items;

        Assert.Equal(QuickLook.MaximumBytes, lines[0].Length);
        Assert.Equal("(Showing the first 64 KB of 64 KB.)", lines[^1]);
    }

    [Fact]
    public void SummarizesBinaryFilesFoldersAndApps()
    {
        File.WriteAllBytes(Path.Combine(root, "image.bin"), [1, 0, 2, 3]);
        Directory.CreateDirectory(Path.Combine(root, "photos"));
        Write("photos/a.txt", "a");
        Write("Dice.app/Contents/Info.xml", """<App version="1"><Info identifier="dev.example.dice" name="Dice" version="2.0" needs="shell"/></App>""");
        Write("Dice.app/Contents/Lua/main.lua", "");

        var binary = Preview(Path.Combine(root, "image.bin"));
        var folder = Preview(Path.Combine(root, "photos"));
        var app = Preview(Path.Combine(root, "Dice.app"));

        Assert.Equal(("File", "4 bytes"), (binary.View.Text("kind"), binary.View.Text("details")));
        Assert.Equal(("Folder", "1 item"), (folder.View.Text("kind"), folder.View.Text("details")));
        Assert.Equal("Dice", app.Title);
        Assert.Equal("Application", app.View.Text("kind"));
        Assert.Equal("dev.example.dice\nVersion 2.0\nNeeds shell", app.View.Text("details"));
    }

    [Fact]
    public void ShowsAnInterfaceLiveOrWhyItCant()
    {
        var good = Write("about.uix", """<UIScript><Window width="120" height="40"/><Text name="hello" x="0" y="0" width="100" height="20" text="Hi"/></UIScript>""");
        var bad = Write("broken.uix", "<UIScript><Nope/></UIScript>");

        Assert.Equal("Hi", Preview(good).View.Text("hello"));
        var broken = Preview(bad);
        Assert.Equal("Interface that can't be shown", broken.View.Text("kind"));
        Assert.Contains("Nope", broken.View.Text("details"));
    }

    [Fact]
    public void ExplainsAMissingFile()
    {
        Assert.False(QuickLook.TryCreate(Path.Combine(root, "gone.txt"), out _, out var problem));
        Assert.Contains("there's no file or folder at", problem);
    }

    private DesktopWindow Preview(string path)
    {
        Assert.True(QuickLook.TryCreate(path, out var window, out var problem), problem);
        return window;
    }

    private string Write(string relative, string text)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }
}
