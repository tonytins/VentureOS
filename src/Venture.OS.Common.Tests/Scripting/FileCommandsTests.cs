using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Scripting;

/// <summary>
/// Runs the shipped file commands (<c>ls</c>, <c>cat</c>, <c>write</c>, <c>mkdir</c>, <c>rm</c>, <c>df</c>, and
/// <c>pwd</c>) through the shell, in a temporary folder.
/// </summary>
public sealed class FileCommandsTests : IDisposable
{
    private static readonly string SystemFiles = Path.Combine(AppContext.BaseDirectory, "system");

    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-filecmd-{Guid.NewGuid():N}");
    private readonly RecordingOutput output = new();
    private readonly CommandShell shell;

    public FileCommandsTests()
    {
        Directory.CreateDirectory(Path.Combine(root, "system"));
        CommandRegistry registry = new();
        registry.AddSource(new ScriptCommands(
            Path.Combine(SystemFiles, "commands"),
            new FakeSystemInfo { SystemDirectory = Path.Combine(root, "system") },
            libraryDirectory: Path.Combine(SystemFiles, "lib")));
        ShellContext context = new(output, new RecordingHost(), registry) { CurrentDirectory = root };
        shell = new CommandShell(context);
    }

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public void WritesAppendsAndPrintsAFile()
    {
        Shell("write notes.txt Hello there");
        Shell("write -a notes.txt Second line");
        Shell("cat notes.txt");

        Assert.Equal(["Hello there", "Second line"], output.Lines);
    }

    [Fact]
    public void ListsWithFoldersMarked()
    {
        Shell("mkdir docs");
        Shell("write a.txt hi");
        Shell("ls");
        Shell("ls -l");

        Assert.Equal(
            ["a.txt", "docs/", "system/", "f          3  a.txt", "d          -  docs/", "d          -  system/"],
            output.Lines);
    }

    [Fact]
    public void ListsSeveralFoldersWithHeadings()
    {
        Shell("mkdir -p one two");
        Shell("write one/x hi");
        Shell("ls one two");

        Assert.Equal([$"{root}/one:", "x", "", $"{root}/two:"], output.Lines);
    }

    [Fact]
    public void MakesAndRemovesFolders()
    {
        Shell("mkdir -p deep/er/est");
        Shell("rm deep");
        Shell("rm -r deep");
        Shell("ls");

        Assert.Equal($"rm: {root}/deep is a folder with things in it; use -r to remove it all", output.Lines[0]);
        Assert.Equal(["system/"], output.Lines[1..]);
    }

    [Fact]
    public void ExplainsProblemsAndCarriesOnWithTheRest()
    {
        Shell("write here.txt x");
        Shell("cat missing.txt here.txt");

        Assert.Equal([$"cat: there's no file at {root}/missing.txt", "x"], output.Lines);
    }

    [Fact]
    public void LeavesTheSystemFolderAlone()
    {
        Shell("write system/x.txt x");
        Shell("rm -r system");

        Assert.All(output.Lines, line => Assert.Contains("system's own folder", line));
        Assert.True(Directory.Exists(Path.Combine(root, "system")));
    }

    [Fact]
    public void ShowsTheFolderAndItsSpace()
    {
        Shell("pwd");
        Shell("df");

        Assert.Equal([root, $"{root}: 30.0 MiB free of 32.0 MiB"], output.Lines);
    }

    [Fact]
    public void ExplainsUsageMistakes()
    {
        Shell("cat");

        Assert.Equal("cat: missing <file...>", output.Lines[0]);
    }

    private void Shell(string line) => shell.Execute(line);
}
