using Venture.OS.Bundles;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Bundles;

/// <summary>
/// Opens the shipped <c>Notes.app</c> from <c>src/Venture.OS/system/apps</c>, copied next to the tests.
/// </summary>
public sealed class NotesAppTests : IDisposable
{
    private static readonly string SystemFiles = Path.Combine(AppContext.BaseDirectory, "system");

    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-notes-{Guid.NewGuid():N}");
    private readonly RecordingOutput output = new();
    private readonly CommandShell shell;
    private readonly AppManager manager;

    public NotesAppTests()
    {
        Directory.CreateDirectory(root);
        CommandRegistry registry = new();
        RuntimeCommands runtime = new(registry.IsBuiltIn);
        registry.AddSource(runtime);
        shell = new CommandShell(new ShellContext(output, new RecordingHost(), registry) { CurrentDirectory = root });
        manager = new AppManager(
            Path.Combine(SystemFiles, "apps"),
            runtime,
            new FakeSystemInfo(),
            Path.Combine(SystemFiles, "lib"),
            shell.Execute);
    }

    public void Dispose()
    {
        manager.CloseApp("Notes", new RecordingOutput());
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void ShipsAReadableBundle()
    {
        var listing = Assert.Single(manager.Available(), listing => listing.Folder == "Notes.app");

        Assert.NotNull(listing.Bundle);
        Assert.Equal("dev.tonytins.ventureos.notes", listing.Bundle.Identifier);
        Assert.Equal(ScriptScopes.Shell | ScriptScopes.Files, listing.Bundle.Needs);
        Assert.Empty(listing.Bundle.IgnoredAttributes);
    }

    [Fact]
    public void AddsListsDeletesAndClearsNotes()
    {
        Open();

        shell.Execute("note");
        shell.Execute("note buy milk");
        shell.Execute("note call the \"train\" office");
        shell.Execute("note -l");
        shell.Execute("note --delete 1");
        shell.Execute("note");
        shell.Execute("note --clear");
        shell.Execute("note --list");

        Assert.Equal(
            [
                "Notes is open. Type \"note --help\" to use it.",
                "No notes yet. Add one with \"note <text>\".",
                "Noted (1).",
                "Noted (2).",
                "  1  buy milk",
                "  2  call the train office",
                "Deleted: buy milk",
                "  1  call the train office",
                "Deleted 1 note.",
                "No notes yet. Add one with \"note <text>\".",
            ],
            output.Lines);
    }

    [Fact]
    public void KeepsNotesInTheFolderItOpenedFromAfterACdAndAReopen()
    {
        Open();
        shell.Execute("note first");
        Directory.CreateDirectory(Path.Combine(root, "elsewhere"));
        shell.Execute("cd elsewhere");
        shell.Execute("note second");
        manager.CloseApp("Notes", output);

        Open();
        shell.Execute("note -l");

        Assert.Equal("first\nsecond\n", File.ReadAllText(Path.Combine(root, "notes.txt")));
        Assert.Equal(["  1  first", "  2  second"], output.Lines[^2..]);
    }

    [Theory]
    [InlineData("note -d 5")]
    [InlineData("note -d 1.5")]
    public void ExplainsAMissingNote(string line)
    {
        Open();
        shell.Execute("note only one");

        shell.Execute(line);

        Assert.StartsWith("note: there's no note", output.Lines[^1]);
    }

    [Fact]
    public void RemovesItsCommandWhenItCloses()
    {
        Open();
        manager.CloseApp("Notes", output);

        shell.Execute("note hello");

        Assert.Equal("\"note\" is not a command. Type \"help\" for a list of commands.", output.Lines[^1]);
    }

    private void Open() => Assert.True(manager.OpenApp("Notes", root, output), string.Join('\n', output.Lines));
}
