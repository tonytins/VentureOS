using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>VentureOS's own icon: in the menu bar in place of the VentureOS menu's name, as Tiger's Apple, and in About.</summary>
public sealed class SystemIconTests
{
    private const int Blue = unchecked((int)0xFF0078A4);
    private const int Key = unchecked((int)0xFF000000) | DesktopIcon.ClearKey;
    private const string IconPath = "/mnt/system/icons/VentureOS.png";

    private const string About = """
        <UIScript version="1">
          <Window width="340" height="250"/>
          <Image name="icon" path="AppIcon.png"/>
          <Visual name="icon" image="icon" x="138" y="16" width="64" height="64"/>
          <Text name="name" x="20" y="88" width="300" height="30" text="VentureOS"/>
          <Text name="version" x="20" y="120" width="300" height="20" text="Version"/>
          <FormattedText name="blurb" x="20" y="146" width="300" height="48" text="An OS."/>
          <Button name="ok" x="250" y="212" width="70" height="24" text="OK"/>
        </UIScript>
        """;

    [Fact]
    public void TheBarShowsTheIconInPlaceOfTheName()
    {
        var session = Open(IconPath);

        var system = session.MenuBar.Menus[0];
        Assert.NotNull(system.Icon);
        Assert.Equal((DesktopSession.SystemIconSize, DesktopSession.SystemIconSize), (system.Icon.Width, system.Icon.Height));

        RecordingPainter painter = new(800, 600);
        session.Draw(painter);

        Assert.DoesNotContain(painter.Texts, text => text.Text == "VentureOS");
        var (drawn, x, y) = Assert.Single(painter.Images, image => image.Image.Width == DesktopSession.SystemIconSize);
        Assert.Same(system.Icon, drawn);
        Assert.InRange(y, 0, MenuBar.Height - DesktopSession.SystemIconSize);

        // The key around the shape is clear, so the bar shows through, and there's no shadow at this size.
        Assert.Equal(0, drawn.Pixels[0]);
        Assert.Equal(Blue, painter.At(x + 8, y + 8));
    }

    [Fact]
    public void OpenTheIconTurnsTheHighlightedTextColor()
    {
        var session = Open(IconPath);
        session.MenuBar.OpenAt(0);
        RecordingPainter painter = new(800, 600);

        session.Draw(painter);

        var (drawn, x, y) = Assert.Single(painter.Images, image => image.Image.Width == DesktopSession.SystemIconSize);
        var white = DesktopTheme.HighlightedText;
        Assert.Equal(DesktopImage.Pack(255, white.Red, white.Green, white.Blue), painter.At(x + 8, y + 8));
        Assert.NotSame(session.MenuBar.Menus[0].Icon, drawn);
    }

    [Fact]
    public void TheIconTakesLessRoomThanTheName()
    {
        RecordingPainter named = new(800, 600);
        RecordingPainter withIcon = new(800, 600);

        Open(null).Draw(named);
        Open(IconPath).Draw(withIcon);

        // "VentureOS" measures 72 here, the icon 16, so File starts 56 sooner.
        Assert.Contains(named.Texts, text => text.Text == "VentureOS");
        Assert.Equal(FileX(named) - 56, FileX(withIcon));
    }

    [Fact]
    public void AnIconThatCantBeReadKeepsTheName()
    {
        var session = Open("/mnt/system/icons/missing.png");

        Assert.Null(session.MenuBar.Menus[0].Icon);
    }

    [Fact]
    public void AboutVentureOSShowsTheIcon()
    {
        var session = Open(IconPath);

        session.ShowAbout();

        var about = session.Windows.Focused!;
        Assert.False(about.View.IsHidden(DesktopSession.AboutIcon));
        Assert.Empty(about.View.MissingImages);
    }

    private static int FileX(RecordingPainter painter) => painter.Texts.First(text => text.Text == "File").X;

    private static DesktopSession Open(string? icon)
    {
        DesktopSession session = new(800, 600, loadImage: Load, text: TestDesktopText.Create())
        {
            SystemIcon = icon,
            AboutDocument = () => UIScript.Parse(About),
        };
        session.Update(new DesktopInput(400, 300, false));
        return session;
    }

    /// <summary>The OS icon: a blue block on the key color, as AniCraft exports it.</summary>
    private static DesktopImage? Load(string path)
    {
        if (path != IconPath)
            return null;

        var pixels = Enumerable.Repeat(Key, 64 * 64).ToArray();
        for (var y = 8; y < 56; y++)
        {
            for (var x = 8; x < 56; x++)
                pixels[(y * 64) + x] = Blue;
        }

        return new DesktopImage(64, 64, pixels);
    }
}
