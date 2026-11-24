using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// The subtitle under a window's title, as a GNOME header bar has one: smaller, quieter, and centred with the title
/// in a taller bar; and in the title's place when the window shows no title. It draws in the palette in use, so it runs with the appearance tests.
/// </summary>
[Collection(nameof(AppearanceCollection))]
public sealed class SubtitleTests
{
    private readonly WindowManager manager = new(800, 600);
    private readonly RecordingPainter painter = new(800, 600);

    [Fact]
    public void ShowsUnderTheTitleSmallerAndQuieter()
    {
        var window = Open("<Window width='300' height='100' subtitle='2 items'/>");

        var title = Assert.Single(painter.Texts, text => text.Text == "Files");
        var subtitle = Assert.Single(painter.Texts, text => text.Text == "2 items");

        Assert.Equal(DesktopTheme.CaptionSize, subtitle.Size);
        Assert.Equal(DesktopTheme.SecondaryText, subtitle.Color);
        Assert.True(subtitle.Y >= title.Y + (int)MathF.Ceiling(DesktopTheme.TextSize * 1.35f));
        Assert.True(subtitle.Y + (int)MathF.Ceiling(DesktopTheme.CaptionSize * 1.35f) <= window.Y + window.BarHeight);

        // Both centred in the window: "Files" is 40 wide and "2 items" 56 at 8 pixels a character.
        Assert.Equal(window.X + ((300 - 40) / 2), title.X);
        Assert.Equal(window.X + ((300 - 56) / 2), subtitle.X);
    }

    [Fact]
    public void ALongSubtitleIsCutToTheTitlesRoom()
    {
        Open("<Window width='200' height='100' subtitle='/mnt/user/apps/Calculator.app/Contents/Resources'/>");

        var subtitle = Assert.Single(painter.Texts, text => text.Text.EndsWith('…'));
        Assert.StartsWith("/mnt/user", subtitle.Text);
        Assert.True(subtitle.Text.Length * 8 <= 200 - 53 - 8);
    }

    [Fact]
    public void AnIdleTitleFieldStaysOnTheTitlesLine()
    {
        var window = Open("<Window width='300' height='100' subtitle='2 items'/><TitleField name='address'/>");
        window.View.SetText("address", "/mnt");
        manager.Draw(painter);

        var title = painter.Texts.Last(text => text.Text == "/mnt");
        var bounds = window.TitleFieldBounds;

        Assert.InRange(title.Y, (int)bounds.Y, (int)(bounds.Y + bounds.Height) - (int)MathF.Ceiling(DesktopTheme.TextSize * 1.35f));
        Assert.Equal((FramePart.TitleField, -1), window.Hit((int)bounds.X + 3, title.Y + 4));
    }

    [Fact]
    public void WithoutOneTheTitleIsCentredInAPlainBar()
    {
        var window = Open("<Window width='300' height='100'/>");

        var title = Assert.Single(painter.Texts, text => text.Text == "Files");
        Assert.Equal(DesktopTheme.TitleBarHeight, window.BarHeight);
        Assert.Equal(window.Y + ((DesktopTheme.TitleBarHeight - (int)MathF.Ceiling(DesktopTheme.TextSize * 1.35f)) / 2), title.Y);
    }

    [Fact]
    public void AWindowThatShowsNoTitleHasABlankBar()
    {
        var window = Open("<Window width='300' height='100'/>", showsTitle: false);

        Assert.DoesNotContain(painter.Texts, text => text.Text == "Files");
        Assert.Equal(DesktopTheme.TitleBarHeight, window.BarHeight);
    }

    [Fact]
    public void WithoutATitleTheSubtitleMovesUpIntoItsPlace()
    {
        var window = Open("<Window width='300' height='100' subtitle='2 items'/>", showsTitle: false);

        Assert.DoesNotContain(painter.Texts, text => text.Text == "Files");
        var subtitle = Assert.Single(painter.Texts, text => text.Text == "2 items");

        // A plain bar, since there's one line, with the subtitle still small and quiet, centred both ways.
        Assert.Equal(DesktopTheme.TitleBarHeight, window.BarHeight);
        Assert.Equal(DesktopTheme.CaptionSize, subtitle.Size);
        Assert.Equal(DesktopTheme.SecondaryText, subtitle.Color);
        Assert.Equal(window.Y + ((DesktopTheme.TitleBarHeight - (int)MathF.Ceiling(DesktopTheme.CaptionSize * 1.35f)) / 2), subtitle.Y);
        Assert.Equal(window.X + ((300 - 56) / 2), subtitle.X);
    }

    [Fact]
    public void ATitleFieldKeepsItsLineWhenTheWindowShowsNoTitle()
    {
        var window = Open("<Window width='300' height='100' subtitle='2 items'/><TitleField name='address'/>", showsTitle: false);
        window.View.SetText("address", "/mnt");
        manager.Draw(painter);

        var title = painter.Texts.Last(text => text.Text == "/mnt");
        var subtitle = painter.Texts.Last(text => text.Text == "2 items");
        Assert.Equal(DesktopTheme.TallTitleBarHeight, window.BarHeight);
        Assert.True(subtitle.Y >= title.Y + (int)MathF.Ceiling(DesktopTheme.TextSize * 1.35f));
    }

    private DesktopWindow Open(string elements, bool showsTitle = true)
    {
        DesktopWindow window = new("Files", new UIView(UIScript.Parse($"<UIScript>{elements}</UIScript>")))
        {
            ShowsTitle = showsTitle,
        };
        manager.Open(window);
        manager.Draw(painter);
        return window;
    }
}
