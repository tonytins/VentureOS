using SkiaSharp;

namespace Venture.Roundhouse.Tests;

/// <summary><c>Roundhouse --render</c>: a document drawn into a PNG file, with no window.</summary>
[Collection(nameof(PaletteCollection))]
public sealed class RenderCommandTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"roundhouse-render-{Guid.NewGuid():N}");
    private readonly StringWriter error = new();

    public RenderCommandTests() => Directory.CreateDirectory(folder);

    public void Dispose() => Directory.Delete(folder, recursive: true);

    [Fact]
    public void DrawsTheAboutWindowOverTheSky()
    {
        var about = Path.Combine(SkiaPainterTests.RepositoryRoot(), "rootfs", "system", "interfaces", "about.uix");
        var picture = Path.Combine(folder, "about.png");

        Assert.Equal(0, RenderCommand.Run([about, picture], error));

        // The window as its document sizes it, with its 30-pixel bar and 24 pixels of sky all round. About's own size
        // is the owner's to change, so it's read from the document rather than pinned.
        var window = Venture.OS.Interface.UIScript.Load(about).Window;
        using var bitmap = SKBitmap.Decode(picture);
        Assert.Equal(((int)window.Size.Width + 48, (int)window.Size.Height + 30 + 48), (bitmap.Width, bitmap.Height));
        Assert.NotEqual(bitmap.GetPixel(2, 2), bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2));

        // About's icon is the desktop's to fill, so its picture isn't beside the document.
        Assert.Contains("the image \"icon\" couldn't be read", error.ToString());
    }

    [Fact]
    public void DarkAndAnAccentFollowThePalette()
    {
        var about = Path.Combine(SkiaPainterTests.RepositoryRoot(), "rootfs", "system", "interfaces", "about.uix");
        var light = Path.Combine(folder, "light.png");
        var dark = Path.Combine(folder, "dark.png");

        Assert.Equal(0, RenderCommand.Run([about, light], error));
        Assert.Equal(0, RenderCommand.Run([about, dark, "--dark", "--accent", "#ED5B00"], error));

        using var lightBitmap = SKBitmap.Decode(light);
        using var darkBitmap = SKBitmap.Decode(dark);
        // The window's panel, beside the content, 30 pixels in and below the bar.
        var (x, y) = (24 + 6, 24 + 30 + 100);
        Assert.True(Brightness(darkBitmap.GetPixel(x, y)) < Brightness(lightBitmap.GetPixel(x, y)));
    }

    [Theory]
    [InlineData(new string[0], 2, "Usage:")]
    [InlineData(new[] { "only-one.uix" }, 2, "Usage:")]
    [InlineData(new[] { "a.uix", "b.png", "--glow" }, 2, "--glow isn't an option")]
    [InlineData(new[] { "a.uix", "b.png", "--accent", "orange" }, 2, "#RRGGBB")]
    [InlineData(new[] { "a.uix", "b.png", "--scrollbars", "overlay" }, 2, "classic or automatic")]
    [InlineData(new[] { "missing.uix", "b.png" }, 1, "missing.uix")]
    public void SaysWhatsWrong(string[] arguments, int exit, string reason)
    {
        Assert.Equal(exit, RenderCommand.Run(arguments, error));
        Assert.Contains(reason, error.ToString());
    }

    [Fact]
    public void AutomaticScrollBarsShowInThePicture()
    {
        var document = Path.Combine(folder, "long.uix");
        File.WriteAllText(document, $"""<UIScript><Window width="200" height="100"/><TextArea name="t" x="0" y="0" width="200" height="100" text="{string.Join("&#10;", Enumerable.Range(1, 20))}"/></UIScript>""");
        var (classic, automatic, none) = (Path.Combine(folder, "classic.png"), Path.Combine(folder, "automatic.png"), Path.Combine(folder, "none.png"));
        File.WriteAllText(Path.Combine(folder, "short.uix"), """<UIScript><Window width="200" height="100"/><TextArea name="t" x="0" y="0" width="200" height="100" text="1"/></UIScript>""");

        Assert.Equal(0, RenderCommand.Run([document, classic], error));
        Assert.Equal(0, RenderCommand.Run([document, automatic, "--scrollbars", "automatic"], error));
        Assert.Equal(0, RenderCommand.Run([Path.Combine(folder, "short.uix"), none, "--scrollbars", "automatic"], error));

        // Near the text area's right edge, halfway down: a classic track, a thin thumb at the top, and plain text area
        // where nothing scrolls.
        using var withClassic = SKBitmap.Decode(classic);
        using var withAutomatic = SKBitmap.Decode(automatic);
        using var withNone = SKBitmap.Decode(none);
        var (x, y) = (24 + 200 - 5, 24 + 30 + 10);
        Assert.NotEqual(withNone.GetPixel(x, y), withAutomatic.GetPixel(x, y));
        Assert.NotEqual(withNone.GetPixel(x, y), withClassic.GetPixel(x, y));
    }

    [Fact]
    public void ADocumentVentureOSRefusesIsntDrawn()
    {
        var document = Path.Combine(folder, "bad.uix");
        File.WriteAllText(document, "<UIScript><Window width='10' height='10'/><Gadget/></UIScript>");

        Assert.Equal(1, RenderCommand.Run([document, Path.Combine(folder, "bad.png")], error));
        Assert.Contains("Gadget", error.ToString());
        Assert.False(File.Exists(Path.Combine(folder, "bad.png")));
    }

    private static int Brightness(SKColor color) => color.Red + color.Green + color.Blue;
}
