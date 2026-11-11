using Venture.OS.Shell;

namespace Venture.OS.Tests.Shell;

public sealed class ShellPathsTests
{
    [Theory]
    [InlineData("/mnt", "notes", "/mnt/notes")]
    [InlineData("/mnt", "notes/a.txt", "/mnt/notes/a.txt")]
    [InlineData("/mnt", "/mnt/system", "/mnt/system")]
    [InlineData("/mnt/notes", "..", "/mnt")]
    [InlineData("/mnt/notes", "../system/init.lua", "/mnt/system/init.lua")]
    [InlineData("/mnt", ".", "/mnt")]
    [InlineData("/mnt", "", "/mnt")]
    [InlineData("/mnt", "./a/./b/", "/mnt/a/b")]
    [InlineData("/mnt", "a//b", "/mnt/a/b")]
    [InlineData("/mnt", "../../..", "/")]
    [InlineData("/", "..", "/")]
    [InlineData("/", "mnt", "/mnt")]
    [InlineData("/mnt/", "notes", "/mnt/notes")]
    public void ResolvesAgainstTheCurrentFolder(string current, string path, string expected) =>
        Assert.Equal(expected, ShellPaths.Resolve(current, path));

    [Theory]
    [InlineData("/mnt/system", "/mnt/system", true)]
    [InlineData("/mnt/system/init.lua", "/mnt/system", true)]
    [InlineData("/MNT/System/a", "/mnt/system", true)]
    [InlineData("/mnt/systemic", "/mnt/system", false)]
    [InlineData("/mnt", "/mnt/system", false)]
    [InlineData("/anything", "/", true)]
    public void TellsWhetherAPathIsWithinAFolder(string path, string folder, bool expected) =>
        Assert.Equal(expected, ShellPaths.IsWithin(path, folder));

    [Fact]
    public void RejectsARelativeCurrentFolder()
    {
        Assert.Throws<ArgumentException>(() => ShellPaths.Resolve("mnt", "a"));
        Assert.Throws<ArgumentNullException>(() => ShellPaths.Resolve(null!, "a"));
        Assert.Throws<ArgumentNullException>(() => ShellPaths.Resolve("/", null!));
    }

    [Fact]
    public void TheContextStartsAtTheRootAndTakesOnlyFullPaths()
    {
        ShellContext context = new(new RecordingOutput(), new RecordingHost(), new CommandRegistry());

        Assert.Equal("/", context.CurrentDirectory);
        context.CurrentDirectory = "/mnt";
        Assert.Equal("/mnt", context.CurrentDirectory);
        Assert.Throws<ArgumentException>(() => context.CurrentDirectory = "mnt");
    }
}
