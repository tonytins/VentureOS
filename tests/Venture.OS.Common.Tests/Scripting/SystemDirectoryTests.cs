using System.Text;
using Venture.OS.Scripting;

namespace Venture.OS.Tests.Scripting;

public sealed class SystemDirectoryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-system-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void WritesFilesAndCreatesFolders()
    {
        SystemDirectory.Install(root, [File("init.lua", "-- init"), File("commands/echo.lua", "print(...)")]);

        Assert.Equal("-- init", System.IO.File.ReadAllText(Path.Combine(root, "init.lua")));
        Assert.Equal("print(...)", System.IO.File.ReadAllText(Path.Combine(root, "commands", "echo.lua")));
    }

    [Fact]
    public void WritesBytesUnchanged()
    {
        byte[] contents = [0, 0xFF, (byte)'\r', (byte)'\n', 0xC3, 0xA9];

        SystemDirectory.Install(root, [new EmbeddedFile("data.bin", contents)]);

        Assert.Equal(contents, System.IO.File.ReadAllBytes(Path.Combine(root, "data.bin")));
    }

    [Fact]
    public void ReplacesFilesThatAreAlreadyThere()
    {
        SystemDirectory.Install(root, [File("init.lua", "old and longer")]);
        SystemDirectory.Install(root, [File("init.lua", "new")]);

        Assert.Equal("new", System.IO.File.ReadAllText(Path.Combine(root, "init.lua")));
    }

    [Fact]
    public void AcceptsARootWithATrailingSlash()
    {
        SystemDirectory.Install(root + "/", [File("init.lua", "-- init")]);

        Assert.True(System.IO.File.Exists(Path.Combine(root, "init.lua")));
    }

    [Fact]
    public void CreatesTheRootWhenThereAreNoFiles()
    {
        SystemDirectory.Install(root, []);

        Assert.True(Directory.Exists(root));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/init.lua")]
    [InlineData("../init.lua")]
    [InlineData("commands/../../init.lua")]
    [InlineData("./init.lua")]
    [InlineData("commands//echo.lua")]
    [InlineData("commands/")]
    [InlineData(@"commands\echo.lua")]
    public void RejectsInvalidPathsBeforeWritingAnything(string path)
    {
        Assert.Throws<ArgumentException>(() =>
            SystemDirectory.Install(root, [File("init.lua", "-- init"), File(path, "bad")]));

        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void RejectsPathsThatDifferOnlyInCase()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            SystemDirectory.Install(root, [File("init.lua", "a"), File("INIT.lua", "b")]));

        Assert.Contains("more than once", exception.Message);
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => SystemDirectory.Install(null!, []));
        Assert.Throws<ArgumentException>(() => SystemDirectory.Install("", []));
        Assert.Throws<ArgumentNullException>(() => SystemDirectory.Install(root, null!));
        Assert.Throws<ArgumentNullException>(() => SystemDirectory.Install(root, [null!]));
        Assert.Throws<ArgumentNullException>(() => SystemDirectory.Install(root, [new EmbeddedFile("a.lua", null!)]));
    }

    private static EmbeddedFile File(string path, string text) => new(path, Encoding.UTF8.GetBytes(text));
}
