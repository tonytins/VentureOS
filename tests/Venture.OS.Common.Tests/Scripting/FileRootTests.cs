using Venture.OS.Scripting;

namespace Venture.OS.Tests.Scripting;

/// <summary><see cref="FileRoot"/>: VentureOS's <c>/</c> as a folder, which no path a script names leads out of.</summary>
public sealed class FileRootTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"venture-root-{Guid.NewGuid():N}");
    private readonly string root;
    private readonly string outside;

    public FileRootTests()
    {
        root = Path.Combine(folder, "root");
        outside = Path.Combine(folder, "outside");
        Directory.CreateDirectory(Path.Combine(root, "mnt", "user"));
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "secret.txt"), "the host's");
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);

    [Theory]
    [InlineData("/", "")]
    [InlineData("/mnt/user/notes.txt", "mnt/user/notes.txt")]
    [InlineData("/mnt/./user//notes.txt", "mnt/user/notes.txt")]
    [InlineData("/../../outside/secret.txt", "outside/secret.txt")]
    [InlineData("/mnt/user/../../../etc/passwd", "etc/passwd")]
    public void PathsAreFoundUnderTheRootAndDotDotStopsThere(string venturePath, string underRoot)
    {
        var expected = underRoot.Length == 0 ? new FileRoot(root).HostFolder : Path.Combine(new FileRoot(root).HostFolder, underRoot);

        Assert.Equal(expected, new FileRoot(root).ToHost(venturePath));
    }

    [Fact]
    public void ARelativePathIsRefused()
    {
        Assert.Throws<ArgumentException>(() => new FileRoot(root).ToHost("mnt/user"));
    }

    [Fact]
    public void ALinkLeadingOutsideIsRefusedWhereverItIsOnThePath()
    {
        Directory.CreateSymbolicLink(Path.Combine(root, "mnt", "user", "out"), outside);
        File.CreateSymbolicLink(Path.Combine(root, "mnt", "user", "secret.txt"), Path.Combine(outside, "secret.txt"));
        FileRoot files = new(root);

        Assert.Throws<UnauthorizedAccessException>(() => files.ToHost("/mnt/user/out"));
        Assert.Throws<UnauthorizedAccessException>(() => files.ToHost("/mnt/user/out/secret.txt"));
        Assert.Throws<UnauthorizedAccessException>(() => files.ToHost("/mnt/user/out/new.txt"));
        Assert.Throws<UnauthorizedAccessException>(() => files.ToHost("/mnt/user/secret.txt"));
    }

    [Fact]
    public void ALinkThatStaysInsideIsFollowed()
    {
        Directory.CreateSymbolicLink(Path.Combine(root, "home"), Path.Combine(root, "mnt", "user"));
        File.WriteAllText(Path.Combine(root, "mnt", "user", "notes.txt"), "mine");

        Assert.Equal("mine", File.ReadAllText(new FileRoot(root).ToHost("/home/notes.txt")));
    }

    [Fact]
    public void ARootReachedThroughALinkStillKeepsPathsInside()
    {
        var linked = Path.Combine(folder, "linked-root");
        Directory.CreateSymbolicLink(linked, root);
        Directory.CreateSymbolicLink(Path.Combine(root, "out"), outside);
        FileRoot files = new(linked);

        Assert.Equal(Path.Combine(files.HostFolder, "mnt", "user"), files.ToHost("/mnt/user"));
        Assert.Throws<UnauthorizedAccessException>(() => files.ToHost("/out/secret.txt"));
    }

    [Fact]
    public void HostPathsInsideAreShownAsScriptsNameThem()
    {
        FileRoot files = new(root);

        Assert.Equal("/", files.ToVenture(root));
        Assert.Equal("/mnt/user/apps/Notes.app", files.ToVenture(Path.Combine(root, "mnt", "user", "apps", "Notes.app")));
        Assert.Throws<ArgumentException>(() => files.ToVenture(outside));
    }

    [Fact]
    public void WithoutARootPathsPassThroughAsInTheKernel()
    {
        Assert.Equal("/mnt/user/notes.txt", FileRoot.Host(null, "/mnt/user/notes.txt"));
        Assert.Equal("/mnt/user", FileRoot.Venture(null, "/mnt/user"));
    }

    [Fact]
    public void AFolderThatIsntThereCantBeTheRoot()
    {
        Assert.Throws<DirectoryNotFoundException>(() => new FileRoot(Path.Combine(folder, "missing")));
    }
}
