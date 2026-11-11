using Venture.OS.Bundles;
using Venture.OS.Scripting;

namespace Venture.OS.Tests.Bundles;

public sealed class BundleTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-bundle-{Guid.NewGuid():N}");

    public BundleTests() => Directory.CreateDirectory(root);

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public void ReadsAnApp()
    {
        var path = App("Notes.app", """
            <?xml version="1.0" encoding="UTF-8"?>
            <App version="1">
              <Info identifier="dev.tonytins.notes" name="Notes" version="1.0" needs="files shell"
                    icon="Contents/Resources/AppIcon.png"/>
            </App>
            """);

        var bundle = Bundle.Read(path, BundleKind.App);

        Assert.Equal(BundleKind.App, bundle.Kind);
        Assert.Equal(path, bundle.Path);
        Assert.Equal("dev.tonytins.notes", bundle.Identifier);
        Assert.Equal("Notes", bundle.Name);
        Assert.Equal("1.0", bundle.Version);
        Assert.Equal(ScriptScopes.Files | ScriptScopes.Shell, bundle.Needs);
        Assert.Equal($"{path}/Contents/Lua/main.lua", bundle.Main);
        Assert.Equal($"{path}/Contents/Resources/AppIcon.png", bundle.Icon);
        Assert.Empty(bundle.IgnoredAttributes);
    }

    [Fact]
    public void TakesAMainScriptFromInfo()
    {
        var path = App("Tool.app", "<App><Info identifier='dev.example.tool' name='Tool' main='Contents/Lua/start.lua'/></App>",
            main: "Contents/Lua/start.lua");

        var bundle = Bundle.Read(path, BundleKind.App);

        Assert.Equal($"{path}/Contents/Lua/start.lua", bundle.Main);
        Assert.Equal(ScriptScopes.None, bundle.Needs);
        Assert.Null(bundle.Version);
        Assert.Null(bundle.Icon);
    }

    [Fact]
    public void ListsAttributesItSkipped()
    {
        var path = App("Typo.app", "<App><Info identifier='dev.example.typo' name='Typo' nedds='files'/></App>");

        var ignored = Assert.Single(Bundle.Read(path, BundleKind.App).IgnoredAttributes);

        Assert.Equal(new IgnoredAttribute("Info", "nedds", 1), ignored);
    }

    [Fact]
    public void ReadsOtherKindsWithoutAMainScript()
    {
        var path = Write("Ocean.theme", "<Theme version='1'><Info identifier='dev.example.ocean' name='Ocean'/></Theme>");

        var bundle = Bundle.Read(path, BundleKind.Theme);

        Assert.Equal(BundleKind.Theme, bundle.Kind);
        Assert.Null(bundle.Main);
    }

    [Theory]
    [InlineData("<Theme><Info identifier='a.b' name='X'/></Theme>", "is a theme, not an app")]
    [InlineData("<Library><Info identifier='a.b' name='X'/></Library>", "is a library, not an app")]
    [InlineData("<Widget><Info identifier='a.b' name='X'/></Widget>", "<Widget> isn't a kind of bundle")]
    [InlineData("<App version='2'><Info identifier='a.b' name='X'/></App>", "format version 2")]
    [InlineData("<App version='one'><Info identifier='a.b' name='X'/></App>", "isn't a format version")]
    [InlineData("<App requires='teleport'><Info identifier='a.b' name='X'/></App>", "requires \"teleport\"")]
    [InlineData("<App><Info identifier='a.b' name='X'/><Dock/></App>", "<Dock> isn't something")]
    [InlineData("<App><Info identifier='a.b' name='X'/><Info identifier='a.b' name='X'/></App>", "more than one <Info>")]
    [InlineData("<App/>", "has no <Info>")]
    [InlineData("<App><Info name='X'/></App>", "needs identifier")]
    [InlineData("<App><Info identifier='a.b'/></App>", "needs name")]
    [InlineData("<App><Info identifier='notes' name='X'/></App>", "isn't a reverse-DNS identifier")]
    [InlineData("<App><Info identifier='dev..notes' name='X'/></App>", "isn't a reverse-DNS identifier")]
    [InlineData("<App><Info identifier='dev.no tes' name='X'/></App>", "isn't a reverse-DNS identifier")]
    [InlineData("<App><Info identifier='a.b' name='X' needs='teleporter'/></App>", "needs: \"teleporter\"")]
    [InlineData("<App><Info identifier='a.b' name='X' main='../escape.lua'/></App>", "must be a path inside the bundle")]
    [InlineData("<App><Info identifier='a.b' name='X' icon='/etc/icon.png'/></App>", "must be a path inside the bundle")]
    [InlineData("<App><Info identifier='a.b' name='X' main='Contents/Lua/missing.lua'/></App>", "has no Contents/Lua/missing.lua to run")]
    [InlineData("<App><Info", "line 1")]
    public void RefusesWhatItCantReadWithTheReason(string info, string message)
    {
        var path = App("Bad.app", info);

        var exception = Assert.Throws<BundleException>(() => Bundle.Read(path, BundleKind.App));

        Assert.Contains(message, exception.Message);
        Assert.StartsWith("Bad.app", exception.Message);
    }

    [Fact]
    public void RefusesAFolderThatIsntABundle()
    {
        Directory.CreateDirectory(Path.Combine(root, "Plain.app"));

        Assert.Contains("has no Contents/Info.xml",
            Assert.Throws<BundleException>(() => Bundle.Read(Path.Combine(root, "Plain.app"), BundleKind.App)).Message);
        Assert.Contains("There's no bundle",
            Assert.Throws<BundleException>(() => Bundle.Read(Path.Combine(root, "Missing.app"), BundleKind.App)).Message);
    }

    [Fact]
    public void NamesEachKindsExtension()
    {
        Assert.Equal(".app", Bundle.Extension(BundleKind.App));
        Assert.Equal(".theme", Bundle.Extension(BundleKind.Theme));
        Assert.Equal(".library", Bundle.Extension(BundleKind.Library));
        Assert.Equal("Library", Bundle.RootName(BundleKind.Library));
    }

    private string App(string name, string info, string main = Bundle.DefaultMain)
    {
        var path = Write(name, info);
        var script = Path.Combine(path, main);
        Directory.CreateDirectory(Path.GetDirectoryName(script)!);
        File.WriteAllText(script, "print('hi')");
        return path;
    }

    private string Write(string name, string info)
    {
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(Path.Combine(path, "Contents"));
        File.WriteAllText(Path.Combine(path, "Contents", "Info.xml"), info);
        return path;
    }
}
