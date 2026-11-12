using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// A style's <c>font="monospace"</c>, and a text area's <c>follows</c>. The area holds 4 lines of 18 pixels.
/// </summary>
public sealed class MonospaceAndFollowTests
{
    private const string Document = """
        <UIScript version="1">
          <Window width="300" height="200"/>
          <Style name="mono" font="monospace"/>
          <Text name="plain" x="0" y="0" width="100" height="20" text="plain"/>
          <Text name="code" x="0" y="20" width="100" height="20" style="mono" text="code"/>
          <TextArea name="log" x="10" y="50" width="200" height="80" readOnly="true" follows="true" style="mono"/>
        </UIScript>
        """;

    private readonly UIView view = new(UIScript.Parse(Document));
    private readonly RecordingPainter painter = new(400, 300);

    [Fact]
    public void AMonospaceStyleDrawsInTheMonospaceFont()
    {
        view.Draw(painter, 0, 0);

        Assert.Contains(painter.Texts, text => text.Text == "plain" && text.Font == DesktopFont.Interface);
        Assert.Contains(painter.Texts, text => text.Text == "code" && text.Font == DesktopFont.Monospace);
    }

    [Fact]
    public void AFollowingAreaStaysAtItsNewestLine()
    {
        view.SetText("log", "1\n2\n3");
        Draw();
        view.SetText("log", "1\n2\n3\n4\n5\n6");
        Draw();

        Assert.Equal("3", FirstLine());
    }

    [Fact]
    public void ItStaysPutWhenScrolledBack()
    {
        view.SetText("log", "1\n2\n3\n4\n5\n6");
        Draw();
        view.Update(new UIViewInput(20, 60, false, false, false, -2, []), 0, 0);
        Draw();
        Assert.Equal("1", FirstLine());

        view.SetText("log", "1\n2\n3\n4\n5\n6\n7");
        Draw();

        Assert.Equal("1", FirstLine());
    }

    [Theory]
    [InlineData("""<Style name="s" font="serif"/>""", "font")]
    [InlineData("""<List name="l" x="0" y="0" width="10" height="10" follows="true"/>""", "follows")]
    public void RefusesMistakes(string elements, string message)
    {
        var text = $"""<UIScript><Window width="10" height="10"/>{elements}</UIScript>""";

        Assert.Contains(message, Assert.Throws<UIScriptException>(() => UIScript.Parse(text)).Message);
    }

    private void Draw()
    {
        painter.Texts.Clear();
        view.Draw(painter, 0, 0);
    }

    private string FirstLine() => painter.Texts.Find(text => text.X == 14 && text.Y == 54).Text;
}
