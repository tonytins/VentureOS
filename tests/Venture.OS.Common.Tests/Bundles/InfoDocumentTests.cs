using Venture.OS.Bundles;
using Venture.OS.Interface.Xml;

namespace Venture.OS.Tests.Bundles;

/// <summary>
/// <see cref="InfoDocument"/>: a bundle's <c>Info.xml</c> edited in place, refusing what VentureOS would refuse.
/// </summary>
public sealed class InfoDocumentTests : IDisposable
{
    private const string Info = """
        <?xml version="1.0" encoding="UTF-8"?>
        <!-- Who the app is. -->
        <App version="1">
          <Info identifier="dev.example.dice"
                name="Dice"
                needs="shell"/>
        </App>
        """;

    private readonly string folder = Path.Combine(Path.GetTempPath(), $"venture-info-{Guid.NewGuid():N}");
    private readonly string app;

    public InfoDocumentTests()
    {
        app = Path.Combine(folder, "Dice.app");
        Directory.CreateDirectory(Path.Combine(app, "Contents", "Lua"));
        Directory.CreateDirectory(Path.Combine(app, "Contents", "Resources"));
        File.WriteAllText(Path.Combine(app, "Contents", "Lua", "main.lua"), "print('rolled')");
        File.WriteAllText(Path.Combine(app, "Contents", "Lua", "other.lua"), "print('other')");
        File.WriteAllBytes(Path.Combine(app, "Contents", "Resources", "Icon.png"), [0x89, 0x50, 0x4E, 0x47]);
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private InfoDocument Document => InfoDocument.Read(Info, app, BundleKind.App);

    [Fact]
    public void ItReadsWhatVentureOSReads()
    {
        var document = Document;

        Assert.True(document.IsEditable);
        Assert.Equal("Dice", document.Bundle!.Name);
        Assert.Equal("shell", document["needs"]);
        Assert.Null(document["version"]);
    }

    [Fact]
    public void SettingAnAttributeChangesOnlyItAndANewOneTakesItsOwnLine()
    {
        var edited = Document.SetAttribute("name", "Dice Roller").SetAttribute("version", "1.1");

        Assert.Equal(Info.Replace("name=\"Dice\"", "name=\"Dice Roller\"", StringComparison.Ordinal)
            .Replace("needs=\"shell\"/>", "needs=\"shell\"\n        version=\"1.1\"/>", StringComparison.Ordinal), edited.Text);
        Assert.Equal("Dice Roller", edited.Bundle!.Name);
        Assert.Equal("1.1", edited.Bundle.Version);
    }

    [Fact]
    public void ARemovedAttributeTakesItsLine()
    {
        Assert.Equal(Info.Replace("\n        needs=\"shell\"", "", StringComparison.Ordinal), Document.RemoveAttribute("needs").Text);
    }

    [Fact]
    public void MainAndIconMustBeFilesInTheBundle()
    {
        Assert.EndsWith("other.lua", Document.SetAttribute("main", "Contents/Lua/other.lua").Bundle!.Main);
        Assert.EndsWith("Icon.png", Document.SetAttribute("icon", "Contents/Resources/Icon.png").Bundle!.Icon);

        Assert.Throws<BundleException>(() => Document.SetAttribute("main", "Contents/Lua/missing.lua"));
        Assert.Throws<BundleException>(() => Document.SetAttribute("main", "../outside.lua"));
    }

    [Theory]
    [InlineData("identifier", "not an identifier")]
    [InlineData("needs", "telepathy")]
    public void AValueVentureOSRefusesIsRefusedWithTheReason(string attribute, string value)
    {
        var exception = Assert.Throws<BundleException>(() => Document.SetAttribute(attribute, value));

        Assert.Contains("Dice.app", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARequiredAttributeCantBeRemoved()
    {
        Assert.Throws<BundleException>(() => Document.RemoveAttribute("name"));
    }

    [Fact]
    public void TextVentureOSRefusesOpensForTextEditingOnlyAndSaysWhy()
    {
        var broken = InfoDocument.Read("<App version=\"1\"><Info name=\"Dice\"/></App>", app, BundleKind.App);
        var malformed = InfoDocument.Read("<App><Info", app, BundleKind.App);

        Assert.False(broken.IsEditable);
        Assert.Contains("identifier", broken.Problem!.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => broken.SetAttribute("name", "x"));
        Assert.Null(malformed.Xml);
        Assert.Contains("line 1", malformed.Problem!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryShippedAppsInfoReadsAndSurvivesASetOfItsOwnName()
    {
        foreach (var bundle in Directory.EnumerateDirectories(AppContext.BaseDirectory, "*.app", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(Path.Combine(bundle, "Contents", "Info.xml"));
            var document = InfoDocument.Read(text, bundle, BundleKind.App);

            Assert.True(document.IsEditable, $"{bundle}: {document.Problem?.Message}");
            Assert.Same(document, document.SetAttribute("name", document["name"]!));
        }
    }

    [Theory]
    [InlineData("/a/Notes.app", BundleKind.App)]
    [InlineData("/a/Earthset.wallpaper/", BundleKind.Wallpaper)]
    [InlineData("/a/folder", null)]
    public void ABundlesKindComesFromItsFolder(string folder, BundleKind? kind)
    {
        Assert.Equal(kind, InfoDocument.KindOf(folder));
    }

    [Fact]
    public void TheEditorWorksOnAnyFlatXmlAndNamesTheRootWhenItHoldsNothing()
    {
        const string text = "<Settings><Look mode=\"dark\"/></Settings>";

        Assert.Equal("<Settings><Look mode=\"light\"/></Settings>", FlatXmlEditor.SetAttribute(text, FlatXml.Parse(text), 0, "mode", "light"));
        var empty = Assert.Throws<InvalidOperationException>(() => FlatXmlEditor.InsertElement("<Settings/>", FlatXml.Parse("<Settings/>"), 0, "Look", []));
        Assert.Contains("<Settings/>", empty.Message, StringComparison.Ordinal);
    }
}
