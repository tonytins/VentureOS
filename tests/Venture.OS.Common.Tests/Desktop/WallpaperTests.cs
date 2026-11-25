using Venture.OS.Desktop;
using Venture.OS.Tests.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// The desktop shows the look's wallpaper, a PNG named after it, the same in light and dark, and follows a change while
/// it runs.
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
        Directory.CreateDirectory(root);
        foreach (var name in (string[])["Red.png", "Night.png", "Night-dark.png", "Clear.png", "notes.txt", ".hidden.png"])
            File.WriteAllText(Path.Combine(root, name), "a picture");

        // A bundle from before wallpapers were plain pictures isn't one any more.
        Directory.CreateDirectory(Path.Combine(root, "Old.wallpaper", "Contents"));
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
    public void ShowsTheSamePictureInDark()
    {
        DesktopAppearance.Use(DesktopLook.Standard with { Wallpaper = "Night", Mode = LookMode.Dark });

        Assert.Equal(Green, Draw(Open()).At(10, 300));
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
            for _, w in ipairs(venture.wallpapers()) do print(w.name, w.title, w.path:match("[^/]+$")) end
            print(venture.setAppearance{ wallpaper = "Nope" })
            print(venture.setAppearance{ wallpaper = "Night" })
            """), new FakeSystemInfo());

        Assert.Equal(
            [
                "Clear\tClear\tClear.png", "Night\tNight\tNight.png", "Night-dark\tNight-dark\tNight-dark.png",
                "Red\tRed\tRed.png", "nil\tthere's no wallpaper named Nope", "true",
            ],
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

    [Fact]
    public void TheShippedWallpapersArePlainPictures()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "system", "wallpaper");

        Assert.Equal(["Charger", "Earthset"], WallpaperCatalog.Read(folder).Select(wallpaper => wallpaper.Name));
        Assert.Equal($"{folder}/Earthset.png", WallpaperCatalog.Find(folder, "Earthset")!.Path);
    }
}
