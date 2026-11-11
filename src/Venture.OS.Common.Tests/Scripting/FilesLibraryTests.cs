using System.Text;
using Venture.OS.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Scripting;

public sealed class FilesLibraryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-files-{Guid.NewGuid():N}");
    private readonly string system;
    private readonly RecordingOutput output = new();

    public FilesLibraryTests()
    {
        system = Path.Combine(root, "system");
        Directory.CreateDirectory(system);
        File.WriteAllText(Path.Combine(system, "init.lua"), "-- init");
        Directory.CreateDirectory(Path.Combine(root, "notes"));
        File.WriteAllText(Path.Combine(root, "notes", "b.txt"), "bee");
        File.WriteAllText(Path.Combine(root, "A.txt"), "hello");
    }

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public void ListsAFolderSortedByName()
    {
        Run("for _, e in ipairs(venture.files.list()) do print(e.name, e.type, e.size) end");

        Assert.Equal(["A.txt\tfile\t5", "notes\tfolder\t0", "system\tfolder\t0"], output.Lines);
    }

    [Fact]
    public void ResolvesPathsFromTheCurrentFolder()
    {
        Run("""
            print(venture.files.resolve())
            print(venture.files.resolve("notes/../A.txt"))
            print(#venture.files.list("notes"))
            """);

        Assert.Equal([root, $"{root}/A.txt", "1"], output.Lines);
    }

    [Fact]
    public void DescribesOneEntry()
    {
        Run("local i = venture.files.info('A.txt') print(i.name, i.path, i.type, i.size)");

        Assert.Equal([$"A.txt\t{root}/A.txt\tfile\t5"], output.Lines);
    }

    [Fact]
    public void ReadsAndWritesBytesExactly()
    {
        Run("""
            local data = "\0\255\r\n\195\169 end"
            assert(venture.files.write("data.bin", data))
            assert(venture.files.write("data.bin", "!", true))
            local back = venture.files.read("data.bin")
            print(#back, back == data .. "!")
            """);

        Assert.Equal(["11\ttrue"], output.Lines);
        Assert.Equal([0, 255, 13, 10, 0xC3, 0xA9, 32, (byte)'e', (byte)'n', (byte)'d', (byte)'!'],
            File.ReadAllBytes(Path.Combine(root, "data.bin"))[..11]);
    }

    [Fact]
    public void KeepsTextAsUtf8()
    {
        Run("assert(venture.files.write('café.txt', 'naïve')) print(venture.files.read('café.txt'))");

        Assert.Equal(["naïve"], output.Lines);
        Assert.Equal("naïve", File.ReadAllText(Path.Combine(root, "café.txt"), Encoding.UTF8));
    }

    [Fact]
    public void MakesFoldersAndParents()
    {
        Run("""
            print(venture.files.mkdir("one"))
            print(venture.files.mkdir("one"))
            print(venture.files.mkdir("two/three"))
            print(venture.files.mkdir("two/three", true))
            print(venture.files.mkdir("two/three", true))
            """);

        Assert.Equal("true", output.Lines[0]);
        Assert.Equal($"nil\t{root}/one already exists", output.Lines[1]);
        Assert.Equal($"nil\tthere's no folder at {root}/two", output.Lines[2]);
        Assert.Equal(["true", "true"], output.Lines[3..]);
        Assert.True(Directory.Exists(Path.Combine(root, "two", "three")));
    }

    [Fact]
    public void RemovesFilesEmptyFoldersAndWithRecursiveEverything()
    {
        Directory.CreateDirectory(Path.Combine(root, "empty"));

        Run("""
            print(venture.files.remove("A.txt"))
            print(venture.files.remove("empty"))
            print(venture.files.remove("notes"))
            print(venture.files.remove("notes", true))
            print(venture.files.remove("nothing"))
            """);

        Assert.Equal(
            [
                "true",
                "true",
                $"nil\t{root}/notes is a folder with things in it",
                "true",
                $"nil\tthere's no file or folder at {root}/nothing",
            ],
            output.Lines);
        Assert.False(Directory.Exists(Path.Combine(root, "notes")));
    }

    [Theory]
    [InlineData("venture.files.write('system/x.txt', 'x')")]
    [InlineData("venture.files.write('SYSTEM/init.lua', 'x')")]
    [InlineData("venture.files.mkdir('system/new')")]
    [InlineData("venture.files.remove('system/init.lua')")]
    [InlineData("venture.files.remove('system', true)")]
    public void RefusesToChangeTheSystemFolder(string call)
    {
        Run($"print(select(2, {call}))");

        Assert.Contains("is the system's own folder", Assert.Single(output.Lines));
        Assert.True(File.Exists(Path.Combine(system, "init.lua")));
        Assert.False(File.Exists(Path.Combine(system, "x.txt")));
    }

    [Fact]
    public void RefusesToRemoveAFolderHoldingTheSystemFolder()
    {
        Run("print(select(2, venture.files.remove('.', true)))");

        Assert.Contains("holds the system's own folder", Assert.Single(output.Lines));
        Assert.True(Directory.Exists(system));
    }

    [Fact]
    public void ReadsTheSystemFolder()
    {
        Run("print(venture.files.read('system/init.lua'))");

        Assert.Equal(["-- init"], output.Lines);
    }

    [Theory]
    [InlineData("venture.files.list('A.txt')", "is a file, not a folder")]
    [InlineData("venture.files.list('nowhere')", "there's no folder at")]
    [InlineData("venture.files.info('nowhere')", "there's no file or folder at")]
    [InlineData("venture.files.read('notes')", "is a folder, not a file")]
    [InlineData("venture.files.read('nowhere')", "there's no file at")]
    [InlineData("venture.files.write('notes', 'x')", "is a folder, not a file")]
    [InlineData("venture.files.write('nowhere/x', 'x')", "there's no folder at")]
    [InlineData("venture.files.mkdir('A.txt')", "is a file")]
    public void ExplainsExpectedFailures(string call, string message)
    {
        Run($"local value, problem = {call} print(value, problem)");

        var line = Assert.Single(output.Lines);
        Assert.StartsWith("nil\t", line);
        Assert.Contains(message, line);
    }

    [Fact]
    public void ReportsSpace()
    {
        Run("local s = venture.files.space() print(s.free, s.total)");

        Assert.Equal([$"{30 * 1024 * 1024}\t{32 * 1024 * 1024}"], output.Lines);
    }

    [Fact]
    public void ReportsUnknownSpace()
    {
        Run("print(venture.files.space())", new FakeSystemInfo { SystemDirectory = system, DiskSpace = null });

        Assert.Equal([$"nil\tthe space at {root} is unknown"], output.Lines);
    }

    [Fact]
    public void RaisesForAWrongArgumentType()
    {
        Run("print((pcall(venture.files.read, {})))");

        Assert.Equal(["false"], output.Lines);
    }

    [Fact]
    public void NeedsTheFilesScope()
    {
        Run("print(pcall(venture.files.list))", scopes: ScriptScopes.System);

        Assert.Contains(VentureLibrary.UndeclaredMessage("files.list", ScriptScopes.Files), Assert.Single(output.Lines));
    }

    private void Run(string code, FakeSystemInfo? info = null, ScriptScopes scopes = ScriptScopes.All)
    {
        var finished = LuaRunner.Run(
            output, root, lua => lua.DoString(code), info ?? new FakeSystemInfo { SystemDirectory = system }, scopes: scopes);

        Assert.True(finished, string.Join('\n', output.Lines));
    }
}
