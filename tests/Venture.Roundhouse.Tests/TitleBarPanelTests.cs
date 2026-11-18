using Avalonia;

namespace Venture.Roundhouse.Tests;

/// <summary><see cref="TitleBarPanel"/> places the title as VentureOS's own title bar does.</summary>
public sealed class TitleBarPanelTests
{
    [Fact]
    public void ATitleThatFitsIsCentredInTheWholeBar()
    {
        Assert.Equal(new Rect(450, 0, 100, 44), TitleBarPanel.TitleBounds(1000, 44, 300, 100, 100));
    }

    [Fact]
    public void ATitleThatWouldMeetAnEndIsCentredInTheRoomBetweenThem()
    {
        // Centred in the bar it would start at 450, inside the 500-pixel start items, so it moves right.
        var bounds = TitleBarPanel.TitleBounds(1000, 44, 500, 100, 100);

        Assert.Equal(new Rect(512 + ((376 - 100) / 2.0), 0, 100, 44), bounds);
    }

    [Fact]
    public void ATitleWiderThanTheRoomIsCutToIt()
    {
        var bounds = TitleBarPanel.TitleBounds(400, 44, 150, 150, 300);

        Assert.Equal(new Rect(162, 0, 76, 44), bounds);
    }

    [Fact]
    public void NoRoomLeavesNoTitle()
    {
        Assert.Equal(0, TitleBarPanel.TitleBounds(200, 44, 150, 150, 80).Width);
    }

    [Theory]
    [InlineData("/home/me/rootfs/user/apps/Calculator.app/Contents/Resources/main.uix", "Calculator.app")]
    [InlineData("/home/me/rootfs/system/interfaces/about.uix", "interfaces")]
    [InlineData("/about.uix", "")]
    public void TheSubtitleIsTheBundleOrFolderADocumentIsIn(string path, string expected)
    {
        Assert.Equal(expected, MainWindow.Subtitle(path));
    }

    [Theory]
    [InlineData("/home/me/rootfs/user/apps/Calculator.app/Contents/Resources/main.uix", "main.uix — Calculator.app")]
    [InlineData("/about.uix", "about.uix")]
    public void TheBarsTitleIsTheDocumentAndItsAppOnOneLine(string path, string expected)
    {
        Assert.Equal(expected, MainWindow.BarTitle(path));
    }
}
