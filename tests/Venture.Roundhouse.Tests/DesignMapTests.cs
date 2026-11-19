using Venture.OS.Desktop;
using Venture.OS.Interface;
using Venture.Roundhouse.Designer;
using Venture.Roundhouse.Drawing;

namespace Venture.Roundhouse.Tests;

/// <summary><see cref="DesignMap"/>: from a point on the canvas to the element drawn there, and back.</summary>
[Collection(nameof(PaletteCollection))]
public sealed class DesignMapTests : IDisposable
{
    private const string Overlapping = """
        <UIScript>
          <Window width="200" height="120"/>
          <Image name="sky" path="sky.png"/>
          <Style name="plain" fontSize="12"/>
          <Region name="back" x="10" y="10" width="100" height="60"/>
          <Button name="front" x="40" y="30" width="40" height="20" text="Go"/>
          <Visual name="picture" image="sky" x="0" y="0" width="190" height="110"/>
        </UIScript>
        """;

    private readonly SkiaFonts fonts = SkiaFonts.LoadShipped();

    public void Dispose()
    {
        fonts.Dispose();
        DesktopAppearance.Use(DesktopLook.Standard);
    }

    [Fact]
    public void APointFindsTheFrontmostControlThenAVisualThenTheWindow()
    {
        var (map, window) = Map(Overlapping);
        var (left, top) = window.ContentOrigin;

        Assert.Equal(4, map.ElementAt(left + 50, top + 35));
        Assert.Equal(3, map.ElementAt(left + 15, top + 15));
        Assert.Equal(5, map.ElementAt(left + 150, top + 100));
        Assert.Equal(0, map.ElementAt(left + 195, top + 115));
        Assert.Equal(0, map.Window);
    }

    [Fact]
    public void ThePlainTitleBarFindsTheWindowAndOffTheWindowFindsNothing()
    {
        var (map, window) = Map(Overlapping);

        Assert.Equal(map.Window, map.ElementAt(window.X + 60, window.Y + 5));
        Assert.Null(map.ElementAt(2, 2));
    }

    [Fact]
    public void AnElementFindsWhereItWasDrawn()
    {
        var (map, window) = Map(Overlapping);
        var (left, top) = window.ContentOrigin;

        Assert.Equal(new UIRect(left + 40, top + 30, 40, 20), map.BoundsOf(4));
        Assert.Equal(new UIRect(left, top, 200, 120), map.BoundsOf(0));
        Assert.Equal(map.Content, map.BoundsOf(0));
        Assert.Null(map.BoundsOf(1));
        Assert.Null(map.BoundsOf(2));
    }

    [Fact]
    public void EveryShippedControlIsFoundAtItsOwnCentre()
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(SkiaPainterTests.RepositoryRoot(), "rootfs"), "*.uix", SearchOption.AllDirectories))
        {
            var document = UIDocument.Load(file);
            var (map, _) = Map(document);
            var script = document.Script!;
            var children = document.Xml!.Children;

            for (var index = 0; index < children.Count; index++)
            {
                if (!UIScriptSchema.Controls.Contains(children[index].Name) || map.BoundsOf(index) is not { } bounds)
                    continue;

                // A later control drawn over this one's centre wins it, as it should.
                var found = map.ElementAt(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
                Assert.True(found >= index, $"{Path.GetFileName(file)}: {children[index]["name"]} found {found}.");
            }

            // A panel the desktop draws into, as the menu bar's, needs none.
            Assert.True(script.Controls.Count > 0 || script.Window.Role == UIWindowRole.Panel, $"{Path.GetFileName(file)} has no controls.");
        }
    }

    [Fact]
    public void APanelIsTheWindowItsDocumentDescribes()
    {
        var (map, _) = Map("""
            <UIScript version="1" requires="shelf">
              <Panel width="400" height="72" edge="bottom"/>
              <Shelf name="apps" x="0" y="0" width="400" height="72"/>
            </UIScript>
            """);

        Assert.Equal(0, map.Window);
    }

    [Fact]
    public void ADocumentVentureOSRefusesCantBeMapped()
    {
        var (_, window) = Map(Overlapping);

        Assert.Throws<ArgumentException>(() => new DesignMap(UIDocument.Read("<UIScript/>"), window));
    }

    private (DesignMap Map, DesktopWindow Window) Map(string text) => Map(UIDocument.Read(text));

    private (DesignMap Map, DesktopWindow Window) Map(UIDocument document)
    {
        var view = DocumentView.Open(document.Script!, Path.GetTempPath(), _ => null, _ => { });
        using var painter = RenderCommand.Draw(view, "test", fonts, out var window);
        return (new DesignMap(document, window), window);
    }
}
