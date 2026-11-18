using Venture.Roundhouse.Bundles;

namespace Venture.Roundhouse.Tests;

/// <summary><see cref="OpenBundle"/>: a bundle's files, and their edits until they're saved.</summary>
public sealed class OpenBundleTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"roundhouse-bundle-{Guid.NewGuid():N}");
    private readonly string app;

    public OpenBundleTests()
    {
        app = Path.Combine(folder, "Calculator.app");
        var source = Path.Combine(SkiaPainterTests.RepositoryRoot(), "rootfs", "user", "apps", "Calculator.app");
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(app, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private string Main => Path.Combine(app, "Contents", "Resources", "main.uix");

    private string Lua => Path.Combine(app, "Contents", "Lua", "main.lua");

    [Fact]
    public void ABundleIsFoundFromItselfOrAnythingInIt()
    {
        Assert.Equal(app, OpenBundle.Find(app));
        Assert.Equal(app, OpenBundle.Find(Main));
        Assert.Equal(app, OpenBundle.Find(Path.Combine(app, "Contents", "Lua", "missing.lua")));
        Assert.Null(OpenBundle.Find(folder));
    }

    [Fact]
    public void ItOpensOnItsFirstInterfaceOrElseItsInfo()
    {
        Assert.Equal(Main, new OpenBundle(app).FirstFile());

        File.Delete(Main);
        Assert.Equal(Path.Combine(app, "Contents", "Info.xml"), new OpenBundle(app).FirstFile());
    }

    [Fact]
    public void ItsTreeListsFoldersFirstThenFilesByName()
    {
        var tree = new OpenBundle(app).Tree();

        Assert.Equal("Calculator.app", tree.Name);
        var contents = Assert.Single(tree.Children);
        Assert.Equal(["Lua", "Resources", "Info.xml"], contents.Children.Select(item => item.Name));
        Assert.True(contents.Children[0].IsFolder);
        Assert.Equal(Lua, contents.Children[0].Children.Single(item => item.Name == "main.lua").Path);
    }

    [Fact]
    public void ALinkedFolderIsListedButNotEnteredSoALoopEnds()
    {
        Directory.CreateSymbolicLink(Path.Combine(app, "Contents", "Loop"), app);

        var loop = new OpenBundle(app).Tree().Children[0].Children.Single(item => item.Name == "Loop");

        Assert.True(loop.IsFolder);
        Assert.Empty(loop.Children);
    }

    [Fact]
    public void AnEditIsKeptUntilSavedAndTextLikeTheSavedIsntAnEdit()
    {
        OpenBundle bundle = new(app);
        var saved = File.ReadAllText(Lua);

        bundle.Edit(Lua, saved + "\n-- edited\n");
        Assert.True(bundle.IsUnsaved(Lua));
        Assert.EndsWith("-- edited\n", bundle.Text(Lua));
        Assert.Equal(saved, File.ReadAllText(Lua));

        bundle.Edit(Lua, saved);
        Assert.False(bundle.IsUnsaved(Lua));

        bundle.Edit(Lua, "print('saved')");
        Assert.True(bundle.Save(Lua));
        Assert.Equal("print('saved')", File.ReadAllText(Lua));
        Assert.Empty(bundle.Unsaved);
        Assert.False(bundle.Save(Lua));
    }

    [Fact]
    public void OnlyTheBundlesOwnFilesAreReadOrEdited()
    {
        OpenBundle bundle = new(app);
        var outside = Path.Combine(folder, "outside.txt");
        File.WriteAllText(outside, "not the bundle's");

        Assert.Throws<ArgumentException>(() => bundle.Text(outside));
        Assert.Throws<ArgumentException>(() => bundle.Edit(Path.Combine(app, "..", "outside.txt"), "x"));
    }

    [Fact]
    public void AFileThatIsntTextIsSaidSo()
    {
        var picture = Path.Combine(app, "Contents", "Resources", "picture.bin");
        File.WriteAllBytes(picture, [0xFF, 0xFE, 0xC3, 0x28]);

        Assert.Throws<InvalidDataException>(() => new OpenBundle(app).Text(picture));
    }
}
