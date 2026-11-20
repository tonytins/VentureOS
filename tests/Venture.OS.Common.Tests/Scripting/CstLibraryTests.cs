using Venture.OS.Bundles;
using Venture.OS.Desktop;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Scripting;

/// <summary>
/// <c>venture.cst</c> and <c>window:string</c>: an app's text from its own tables and the shared ones, as written or
/// with its placeholders filled, and <c>nil</c> and why when there's none.
/// </summary>
public sealed class CstLibraryTests : IDisposable
{
    private const string Document = """
        <UIScript version="1">
          <Window width="200" height="120"/>
          <String name="count" table="1" index="2"/>
          <String name="gone" table="1" index="9"/>
          <Text name="label" x="10" y="10" width="180" height="20" string="count"/>
        </UIScript>
        """;

    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-cst-{Guid.NewGuid():N}");
    private readonly RecordingOutput output = new();
    private readonly WindowManager windows = new(800, 600);
    private readonly AppManager manager;

    public CstLibraryTests()
    {
        var system = Path.Combine(root, "system");
        Directory.CreateDirectory(Path.Combine(system, "strings"));
        File.WriteAllText(Path.Combine(system, "strings", "_100_common.cst"), "1 ^OK^\n");
        Directory.CreateDirectory(Path.Combine(root, "apps"));
        CommandRegistry registry = new();
        manager = new AppManager(
            Path.Combine(root, "apps"),
            new RuntimeCommands(registry.IsBuiltIn),
            new FakeSystemInfo(),
            windows: new AppWindows(windows, new RecordingOutput()) { SystemFolder = system });
    }

    public void Dispose()
    {
        if (manager.IsOpen("dev.example.pad"))
            manager.CloseApp("Pad", new RecordingOutput());
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void GetGivesTheTextAsWritten()
    {
        Open("""
            print(venture.cst.get(1, 1))
            print(venture.cst.get(1, 2))
            print(venture.cst.get(1, "ok button"))
            print(venture.cst.get(100, 1))
            """);

        Assert.Equal(["Notes", "%d notes by %s", "Accept", "OK"], output.Lines);
    }

    [Fact]
    public void FormatFillsThePlaceholdersInOrder()
    {
        Open("""
            print(venture.cst.format(1, 2, 3, "Tony"))
            print(venture.cst.format(1, 2, 2.5, "Tony"))
            print(venture.cst.format(1, 2, 3))
            print(venture.cst.format(1, 1))
            """);

        // A placeholder with no value left stays as written, so the mistake shows.
        Assert.Equal(["3 notes by Tony", "2.5 notes by Tony", "3 notes by %s", "Notes"], output.Lines);
    }

    [Fact]
    public void MissingTextIsNilAndWhy()
    {
        Open("""
            print(venture.cst.get(1, 9))
            print(venture.cst.format(7, 1, "x"))
            print(venture.cst.get(1, 3))
            """);

        Assert.Equal("nil\tthere's no text for table 1, key 9", output.Lines[0]);
        Assert.Equal("nil\tthere's no text for table 7, key 1", output.Lines[1]);
        // What the tables found while looking is the reason, such as a reference to nothing.
        Assert.StartsWith("nil\tTable 1, key 3 refers to table 1, key 8, which isn't there", output.Lines[2], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("venture.cst.format(1, 2, {})", "venture.cst.format: argument 3 must be text or a number, not a table")]
    [InlineData("venture.cst.format(1, 2, 3, nil)", "venture.cst.format: argument 4 must be text or a number, not nil")]
    [InlineData("venture.cst.get(1)", "venture.cst.get needs a key")]
    [InlineData("venture.cst.get(-1, 1)", "a table is numbered 0 or more")]
    public void MistakesInTheCodeAreErrors(string call, string message)
    {
        Open($"print(pcall(function() return {call} end))");

        Assert.StartsWith("false", output.Lines[0], StringComparison.Ordinal);
        Assert.Contains(message, output.Lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void AWindowFillsAStringItsDocumentNames()
    {
        Open("""
            local window = venture.ui.open("main.uix")
            window:setText("label", window:string("count", 4, "Ada"))
            print(window:text("label"))
            print(window:string("gone"))
            print(pcall(window.string, window, "nope"))
            """);

        Assert.Equal("4 notes by Ada", output.Lines[0]);
        Assert.Equal("nil\tthere's no text for the string \"gone\"", output.Lines[1]);
        Assert.Equal("false\twindow:string: main.uix has no <String name=\"nope\">", output.Lines[2]);
    }

    [Fact]
    public void OnlyAppsHaveIt()
    {
        RecordingOutput script = new();

        Assert.True(LuaRunner.Run(script, "/mnt", lua => lua.DoString("print(pcall(venture.cst.get, 1, 1))"), new FakeSystemInfo()));
        Assert.Equal([$"false\t{CstLibrary.NotAppMessage}"], script.Lines);
    }

    private void Open(string main)
    {
        var contents = Path.Combine(root, "apps", "Pad.app", "Contents");
        Directory.CreateDirectory(Path.Combine(contents, "Lua"));
        Directory.CreateDirectory(Path.Combine(contents, "Resources"));
        File.WriteAllText(Path.Combine(contents, "Info.xml"), """<App version="1"><Info identifier="dev.example.pad" name="Pad"/></App>""");
        File.WriteAllText(Path.Combine(contents, "Lua", "main.lua"), main + "\n");
        File.WriteAllText(Path.Combine(contents, "Resources", "main.uix"), Document);
        File.WriteAllText(Path.Combine(contents, "Resources", "_1_pad.cst"),
            "1 ^Notes^\n2 ^%d notes by %s^\nok button ^Accept^\n3 ^\u0002<cst><cst_id>1</cst_id><string_id>8</string_id></cst>^\n");

        Assert.True(manager.OpenApp("Pad", root, output), string.Join('\n', output.Lines));
    }
}
