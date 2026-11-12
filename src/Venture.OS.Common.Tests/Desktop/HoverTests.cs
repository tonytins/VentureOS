using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// Hover: what the pointer rests on shows it, in whichever window is under the pointer, with no change to the app.
/// The close and minimize buttons show their marks, the title field's text takes the accent, toolbar items and
/// buttons lighten, and sidebar rows are shaded. Sidebar rows start 7 pixels below the content's top and are 26 tall.
/// </summary>
public sealed class HoverTests
{
    private const string Document = """
        <UIScript requires="sidebar">
          <Window width="300" height="100"/>
          <Sidebar name="places" width="100"/>
          <ToolbarItem name="add" title="Add"/>
          <TitleField name="address" placeholder="Path"/>
          <Button name="ok" x="10" y="10" width="60" height="24" text="OK"/>
        </UIScript>
        """;

    private readonly WindowManager manager = new(800, 600);
    private readonly RecordingPainter painter = new(800, 600);
    private readonly DesktopWindow window;

    public HoverTests()
    {
        window = new DesktopWindow("Files", new UIView(UIScript.Parse(Document)));
        window.View.List("places")!.Items.AddRange(["Home", "User"]);
        window.View.List("places")!.SelectedIndex = 0;
        window.View.SetText("address", "/mnt");
        manager.Open(window);
        Hover(0, 0);
    }

    [Fact]
    public void TheButtonsShowTheirMarks()
    {
        var (close, minimize) = (window.CloseButton, window.MinimizeButton);
        var closeMark = Pack(DesktopTheme.CloseMark);
        var minimizeMark = Pack(DesktopTheme.MinimizeMark);
        Assert.NotEqual(closeMark, painter.At((int)close.X + 3, (int)close.Y + 3));
        Assert.NotEqual(minimizeMark, painter.At((int)minimize.X + 4, (int)minimize.Y + 5));

        // Over either button, both show their marks, as macOS does.
        Hover((int)minimize.X + 6, (int)minimize.Y + 6);

        Assert.Equal(closeMark, painter.At((int)close.X + 3, (int)close.Y + 3));
        Assert.Equal(minimizeMark, painter.At((int)minimize.X + 4, (int)minimize.Y + 5));
    }

    [Fact]
    public void TheTitleFieldsTextTakesTheAccent()
    {
        Assert.Contains(painter.Texts, text => text.Text == "/mnt" && text.Color == DesktopTheme.Text);

        var field = window.TitleFieldBounds;
        Hover((int)field.X + 3, (int)field.Y + 3);

        Assert.Contains(painter.Texts, text => text.Text == "/mnt" && text.Color == DesktopTheme.Accent);
    }

    [Fact]
    public void AToolbarItemLightens()
    {
        var add = window.ToolbarItemBounds(0);
        var (x, y) = ((int)add.X + 3, (int)(add.Y + add.Height) - 3);
        var plain = painter.At(x, y);

        Hover((int)add.X + 3, (int)add.Y + 3);

        Assert.True(Brightness(painter.At(x, y)) > Brightness(plain));
    }

    [Fact]
    public void AButtonLightens()
    {
        var (left, top) = window.ContentOrigin;
        var (x, y) = (left + 13, top + 10 + 21);
        var plain = painter.At(x, y);

        Hover(left + 13, top + 13);

        Assert.True(Brightness(painter.At(x, y)) > Brightness(plain));
    }

    [Fact]
    public void ASidebarRowIsShaded()
    {
        var (_, top) = window.ContentOrigin;
        var (x, row) = (window.X + 20, top + 7 + 26 + 3);
        Assert.Equal(Pack(DesktopTheme.SidebarBackground), painter.At(x, row));

        Hover(window.X + 40, top + 7 + 26 + 5);

        Assert.Equal(Pack(DesktopTheme.SidebarHover), painter.At(x, row));
        Assert.Equal(Pack(DesktopTheme.SidebarSelection), painter.At(x, top + 7 + 3));
    }

    [Fact]
    public void NothingHoversWhileTheButtonIsDown()
    {
        var (left, top) = window.ContentOrigin;
        var minimize = window.MinimizeButton;
        manager.Update(new DesktopInput(left + 200, top + 50, true));
        manager.Update(new DesktopInput((int)minimize.X + 6, (int)minimize.Y + 6, true));
        manager.Draw(painter);

        Assert.NotEqual(Pack(DesktopTheme.MinimizeMark), painter.At((int)minimize.X + 4, (int)minimize.Y + 5));
    }

    [Fact]
    public void NothingHoversWhenTheDesktopSaysSo()
    {
        var minimize = window.MinimizeButton;
        manager.ShowsHover = false;

        Hover((int)minimize.X + 6, (int)minimize.Y + 6);

        Assert.NotEqual(Pack(DesktopTheme.MinimizeMark), painter.At((int)minimize.X + 4, (int)minimize.Y + 5));
    }

    [Fact]
    public void OnlyTheWindowUnderThePointerHovers()
    {
        DesktopWindow front = new("Front", new UIView(UIScript.Parse("""<UIScript><Window width="400" height="100"/></UIScript>""")));
        manager.Open(front);
        (front.X, front.Y) = (window.X, window.Y);
        var field = window.TitleFieldBounds;

        // The pointer is where the title field behind would be, but on the front window's bar.
        Hover((int)field.X + 3, (int)field.Y + 3);

        Assert.Contains(painter.Texts, text => text.Text == "/mnt" && text.Color == DesktopTheme.Text);
        Assert.DoesNotContain(painter.Texts, text => text.Text == "/mnt" && text.Color == DesktopTheme.Accent);
    }

    private static int Pack(UIColor color) => DesktopBackground.Pack(color.Red, color.Green, color.Blue);

    private static int Brightness(int pixel) => ((pixel >> 16) & 0xFF) + ((pixel >> 8) & 0xFF) + (pixel & 0xFF);

    private void Hover(int x, int y)
    {
        manager.Update(new DesktopInput(x, y, false));
        painter.Texts.Clear();
        manager.Draw(painter);
    }
}
