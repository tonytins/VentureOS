using Venture.OS.Interface.Xml;

namespace Venture.OS.Tests.Xml;

/// <summary>Where <see cref="FlatXml"/> finds each element and attribute in the text, for editing it in place.</summary>
public sealed class FlatXmlSourceTests
{
    private const string Text = """
        <?xml version="1.0"?>
        <UIScript version="1">
          <!-- A comment. -->
          <Window width="400"
                  height='255'/>
          <Button name="send" text="Tom &amp; Jerry"></Button>
        </UIScript>
        """;

    private static readonly FlatXmlDocument Document = FlatXml.Parse(Text);

    [Fact]
    public void AnElementsSpanRunsFromItsOpeningToItsClosingBracket()
    {
        Assert.Equal("""
            <Window width="400"
                      height='255'/>
            """, Document.Children[0].Source!.Span.Of(Text));
        Assert.Equal("""<Button name="send" text="Tom &amp; Jerry"></Button>""", Document.Children[1].Source!.Span.Of(Text));
    }

    [Fact]
    public void TheRootsSpanHoldsItsChildrenAndItsClosingTag()
    {
        var root = Document.Root.Source!;

        Assert.StartsWith("<UIScript version", root.Span.Of(Text), StringComparison.Ordinal);
        Assert.EndsWith("</UIScript>", root.Span.Of(Text), StringComparison.Ordinal);
        Assert.Equal(Text.IndexOf("</UIScript>", StringComparison.Ordinal), root.CloseStart);
    }

    [Fact]
    public void AnAttributeKeepsItsValueAsWrittenAndItsQuote()
    {
        var height = Document.Children[0].Source!.Attributes[1];
        var text = Document.Children[1].Source!.Attributes[1];

        Assert.Equal(("height", "height='255'", "255", '\''), (height.Name, height.Span.Of(Text), height.Value.Of(Text), height.Quote));
        Assert.Equal("Tom &amp; Jerry", text.Value.Of(Text));
        Assert.Equal("Tom & Jerry", Document.Children[1]["text"]);
    }

    [Fact]
    public void TheNameEndsWhereAttributesWouldBeginAndAClosingTagIsOnlyForOneWrittenOpen()
    {
        var window = Document.Children[0].Source!;
        var button = Document.Children[1].Source!;

        Assert.Equal("<Window", Text[window.Span.Start..window.NameEnd]);
        Assert.Null(window.CloseStart);
        Assert.Equal("</Button>", Text[button.CloseStart!.Value..button.Span.End]);
    }

    [Fact]
    public void AnEmptyValueHasAnEmptySpanBetweenItsQuotes()
    {
        const string text = """<Root><Item a="" b=''/></Root>""";

        var attributes = FlatXml.Parse(text).Children[0].Source!.Attributes;

        Assert.All(attributes, attribute => Assert.Equal(0, attribute.Value.Length));
        Assert.Equal(text.IndexOf("\"\"", StringComparison.Ordinal) + 1, attributes[0].Value.Start);
    }
}
