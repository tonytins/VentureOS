using Venture.OS.Interface;

namespace Venture.OS.Tests.Interface;

/// <summary>
/// Style roles and dark colors: a color can name a role the host resolves against its palette, a size can name a
/// role, and each color has a <c>dark</c> twin for the host's dark palette.
/// </summary>
public sealed class UIScriptStyleTests
{
    [Fact]
    public void ReadsColorRoles()
    {
        var style = Style("""textColor="secondaryText" backgroundColor="inputBackground" borderColor="accent" """);

        Assert.Equal(UIStyleColor.FromRole(UIColorRole.SecondaryText), style.Colors.Text);
        Assert.Equal(UIStyleColor.FromRole(UIColorRole.InputBackground), style.Colors.Background);
        Assert.Equal(UIStyleColor.FromRole(UIColorRole.Accent), style.Colors.Border);
        Assert.Null(style.Colors.Fill);
    }

    [Theory]
    [InlineData("text", UIColorRole.Text)]
    [InlineData("secondaryText", UIColorRole.SecondaryText)]
    [InlineData("disabledText", UIColorRole.DisabledText)]
    [InlineData("highlightedText", UIColorRole.HighlightedText)]
    [InlineData("accent", UIColorRole.Accent)]
    [InlineData("panel", UIColorRole.Panel)]
    [InlineData("inputBackground", UIColorRole.InputBackground)]
    [InlineData("border", UIColorRole.Border)]
    [InlineData("selection", UIColorRole.Selection)]
    public void KnowsEveryColorRole(string name, UIColorRole role)
    {
        Assert.Equal(UIStyleColor.FromRole(role), Style($"""fillColor="{name}" """).Colors.Fill);
    }

    [Fact]
    public void StillReadsLiteralColors()
    {
        var style = Style("""textColor="#6E747E" fillColor="#11223344" """);

        Assert.Equal(UIStyleColor.Literal(new UIColor(0x6E, 0x74, 0x7E)), style.Colors.Text);
        Assert.Equal(UIStyleColor.Literal(new UIColor(0x11, 0x22, 0x33, 0x44)), style.Colors.Fill);
    }

    [Fact]
    public void ReadsDarkTwins()
    {
        var style = Style("""
            textColor="#202020" darkTextColor="#E0E0E0" darkSelectedTextColor="text" darkHighlightedTextColor="accent"
            darkDisabledTextColor="disabledText" darkBackgroundColor="panel" darkBorderColor="border" darkFillColor="#102030"
            """);

        Assert.Equal(UIStyleColor.Literal(new UIColor(0x20, 0x20, 0x20)), style.Colors.Text);
        Assert.Equal(
            new UIStyleColors(
                UIStyleColor.Literal(new UIColor(0xE0, 0xE0, 0xE0)),
                UIStyleColor.FromRole(UIColorRole.Text),
                UIStyleColor.FromRole(UIColorRole.Accent),
                UIStyleColor.FromRole(UIColorRole.DisabledText),
                UIStyleColor.FromRole(UIColorRole.Panel),
                UIStyleColor.FromRole(UIColorRole.Border),
                UIStyleColor.Literal(new UIColor(0x10, 0x20, 0x30))),
            style.Dark);
    }

    [Theory]
    [InlineData("caption", UIFontSizeRole.Caption)]
    [InlineData("body", UIFontSizeRole.Body)]
    [InlineData("title", UIFontSizeRole.Title)]
    [InlineData("largeTitle", UIFontSizeRole.LargeTitle)]
    public void ReadsSizeRoles(string name, UIFontSizeRole role)
    {
        Assert.Equal(UIStyleSize.FromRole(role), Style($"""fontSize="{name}" """).FontSize);
    }

    [Fact]
    public void StillReadsPointSizes()
    {
        Assert.Equal(UIStyleSize.Points(24), Style("""fontSize="24" """).FontSize);
    }

    [Theory]
    [InlineData("""textColor="secondary" """, "textColor")]
    [InlineData("""darkTextColor="blue" """, "darkTextColor")]
    [InlineData("""textColor="#12345" """, "textColor")]
    [InlineData("""fontSize="huge" """, "fontSize")]
    [InlineData("""fontSize="0" """, "positive fontSize")]
    [InlineData("""fontSize="-3" """, "positive fontSize")]
    public void RefusesMistakes(string attributes, string message)
    {
        var text = $"""<UIScript><Window width="10" height="10"/><Style name="s" {attributes}/></UIScript>""";

        Assert.Contains(message, Assert.Throws<UIScriptException>(() => UIScript.Parse(text)).Message);
    }

    private static UIStyle Style(string attributes) =>
        UIScript.Parse($"""<UIScript><Window width="10" height="10"/><Style name="s" {attributes}/></UIScript>""").Styles["s"];
}
