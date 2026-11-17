using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// Buttons that show a picture, as a wallpaper picker's thumbnails do: scaled to the button, the first state of a
/// strip, and ringed in the accent when checked. They draw in the palette in use, so they run with the appearance
/// tests.
/// </summary>
[Collection(nameof(AppearanceCollection))]
public sealed class ImageButtonTests
{
    private const int Red = unchecked((int)0xFFFF0000);
    private const int Blue = unchecked((int)0xFF0000FF);

    private const string Document = """
        <UIScript version="1">
          <Window width="200" height="100"/>
          <Image name="picture" path="picture.png"/>
          <Image name="states" path="states.png" stateCount="4"/>
          <Image name="missing" path="missing.png"/>
          <Button name="thumbnail" x="20" y="20" width="40" height="30" image="picture"/>
          <Button name="strip" x="100" y="20" width="20" height="20" image="states"/>
          <Button name="broken" x="150" y="20" width="20" height="20" image="missing"/>
        </UIScript>
        """;

    private readonly UIView view = new(UIScript.Parse(Document));
    private readonly RecordingPainter painter = new(400, 300);

    public ImageButtonTests()
    {
        view.SetImage("picture", new DesktopImage(4, 3, Enumerable.Repeat(Red, 12).ToArray()));

        // Four states side by side: red, then three blue.
        view.SetImage("states", new DesktopImage(4, 1, [Red, Blue, Blue, Blue]));
    }

    [Fact]
    public void ShowsItsPictureScaledToTheButton()
    {
        view.Draw(painter, 0, 0);

        var (image, x, y) = Assert.Single(painter.Images, drawn => drawn.Image.Width == 40);
        Assert.Equal((40, 30, 20, 20), (image.Width, image.Height, x, y));
        Assert.Equal(Red, painter.At(40, 35));
        Assert.Equal(["missing"], view.MissingImages);
    }

    [Fact]
    public void AStripShowsItsFirstState()
    {
        view.Draw(painter, 0, 0);

        Assert.Equal(Red, painter.At(118, 38));
    }

    [Fact]
    public void AButtonWithoutItsPictureIsOutlinedInRed()
    {
        view.Draw(painter, 0, 0);

        Assert.Equal(Pack(DesktopTheme.MissingImage), painter.At(150, 30));
    }

    [Fact]
    public void ACheckedOneIsRingedInTheAccent()
    {
        view.Draw(painter, 0, 0);
        Assert.NotEqual(Pack(DesktopTheme.Accent), painter.At(18, 30));

        view.SetSelected(true, "thumbnail");
        view.Draw(painter, 0, 0);

        Assert.Equal(Pack(DesktopTheme.Accent), painter.At(18, 30));
        Assert.Equal(Pack(DesktopTheme.Accent), painter.At(17, 30));
        Assert.Equal(Red, painter.At(20, 30));
    }

    private static int Pack(UIColor color) => DesktopImage.Pack(255, color.Red, color.Green, color.Blue);
}
