using Venture.OS.Desktop;
using Venture.OS.Tests.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// The desktop shows the look's wallpaper, from its bundle, in light or dark, and follows a change while it runs.
/// The look is shared, so these run with the appearance tests.
/// </summary>
[Collection(nameof(AppearanceCollection))]
public sealed class WallpaperTests : IDisposable
{
    private const int Red = unchecked((int)0xFFFF0000);
    private const int Green = unchecked((int)0xFF00FF00);
    private const int Blue = unchecked((int)0xFF0000FF);

    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-wallpapers-{Guid.NewGuid():N}");
    private readonly HeldOutput held = new();

    public WallpaperTests()
    {
        Bundle("Red", "Red.png");
        Bundle("Night", "Night.png", "Night-dark.png");
        Bundle("Clear", "Clear.png");
        DesktopAppearance.WallpapersFolder = root;
    }

    public void Dispose()
    {
        DesktopAppearance.WallpapersFolder = null;
        DesktopAppearance.Use(DesktopLook.Standard);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void ShowsTheLooksWallpaperCoveringTheScreen()
    {
        DesktopAppearance.Use(DesktopLook.Standard with { Wallpaper = "Red" });

        var painter = Draw(Open());

        Assert.Equal(Red, painter.At(10, 300));
        Assert.Equal(Red, painter.At(1013, 500));
    }

    [Fact]
    public void FollowsAChangeWhileItRuns()
    {
        DesktopAppearance.Use(DesktopLook.Standard with { Wallpaper = "Red" });
        var session = Open();
        Draw(session);

        DesktopAppearance.Use(DesktopLook.Standard with { Wallpaper = "Night" });

        Assert.True(session.Update(new DesktopInput(512, 384, false)));
        Assert.Equal(Green, Draw(session).At(10, 300));
    }

    [Fact]
    public void ShowsTheDarkPictureInDark()
    {
        DesktopAppearance.Use(DesktopLook.Standard with { Wallpaper = "Night", Mode = LookMode.Dark });

        Assert.Equal(Blue, Draw(Open()).At(10, 300));
    }

    [Fact]
    public void TheSkyIsTheSky()
    {
        DesktopAppearance.Use(DesktopLook.Standard with { Wallpaper = DesktopLook.Sky });

        Assert.Equal(DesktopBackground.Render(1024, 768)[(300 * 1024) + 10], Draw(Open()).At(10, 300));
    }

    [Fact]
    public void AMissingWallpaperShowsTheSkyAndSaysSo()
    {
        DesktopAppearance.Use(DesktopLook.Standard with { Wallpaper = "Gone" });

        Assert.Equal(DesktopBackground.Render(1024, 768)[(300 * 1024) + 10], Draw(Open()).At(10, 300));
        Assert.Contains(held.Take(), line => line.Contains("no wallpaper named Gone") && line.Contains("showing the sky"));
    }

    [Fact]
    public void AWallpapersClearPartsShowTheSky()
    {
        DesktopAppearance.Use(DesktopLook.Standard with { Wallpaper = "Clear" });

        Assert.Equal(DesktopBackground.Render(1024, 768)[(300 * 1024) + 10], Draw(Open()).At(10, 300));
    }

    [Fact]
    public void ScriptsListTheWallpapersAndCantChooseOneThatIsntThere()
    {
        RecordingOutput output = new();
        Venture.OS.Scripting.LuaRunner.Run(output, Path.GetTempPath(), lua => lua.DoString("""
            for _, w in ipairs(venture.wallpapers()) do print(w.name, w.title, w.thumbnail ~= nil) end
            print(venture.setAppearance{ wallpaper = "Nope" })
            print(venture.setAppearance{ wallpaper = "Night" })
            """), new FakeSystemInfo());

        Assert.Equal(
            ["Clear\tClear\ttrue", "Night\tNight\ttrue", "Red\tRed\ttrue", "nil\tthere's no wallpaper named Nope", "true"],
            output.Lines);
        Assert.Equal("Night", DesktopAppearance.Current.Wallpaper);
    }

    private DesktopSession Open() =>
        new(1024, 768, appOutput: held, loadImage: path => Path.GetFileName(path) switch
        {
            "Red.png" => Square(Red),
            "Night.png" => Square(Green),
            "Night-dark.png" => Square(Blue),
            "Clear.png" => new DesktopImage(1, 1, [0]),
            _ => null,
        });

    private static RecordingPainter Draw(DesktopSession session)
    {
        RecordingPainter painter = new(1024, 768);
        session.Draw(painter);
        return painter;
    }

    private static DesktopImage Square(int pixel) => new(2, 2, [pixel, pixel, pixel, pixel]);

    private void Bundle(string name, params string[] pictures)
    {
        var contents = Path.Combine(root, $"{name}.wallpaper", "Contents");
        Directory.CreateDirectory(Path.Combine(contents, "Resources"));
        foreach (var picture in pictures)
            File.WriteAllText(Path.Combine(contents, "Resources", picture), "a picture");
        File.WriteAllText(Path.Combine(contents, "Info.xml"),
            $"""<Wallpaper version="1"><Info identifier="dev.example.{name.ToLowerInvariant()}" name="{name}" image="Contents/Resources/{pictures[0]}"/></Wallpaper>""");
    }
}
