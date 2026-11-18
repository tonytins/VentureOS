using Venture.OS.Interface;
using Venture.OS.Interface.Xml;

namespace Venture.OS.Tests.Interface;

/// <summary>
/// Setting and removing attributes on a <see cref="UIDocument"/>: only the attribute changes, laid out as the
/// element's others are, and nothing VentureOS would refuse gets through.
/// </summary>
public sealed class UIDocumentAttributeTests
{
    private const string Text = """
        <?xml version="1.0" encoding="UTF-8"?>
        <UIScript version="1">
          <!-- The window. -->
          <Window width="200"
                  height="120"/>
          <Text name="greeting" x="8" y='8' width="100" height="20" text="Hi &amp; bye"/>
          <Button name="ok" x="8" y="40" width="60" height="24" text="OK"></Button>
        </UIScript>
        """;

    private static readonly UIDocument Document = UIDocument.Read(Text);

    [Fact]
    public void SettingAnAttributeChangesOnlyItsValue()
    {
        var edited = Document.SetAttribute(2, "x", "16");

        Assert.Equal(Text.Replace("""name="ok" x="8" """, """name="ok" x="16" """, StringComparison.Ordinal), edited.Text);
        Assert.Equal(16, edited.Script!.Control("ok")!.Frame.X);
        Assert.Equal(Text, Document.Text);
    }

    [Fact]
    public void AValueKeepsItsQuotesAndIsEncodedForThem()
    {
        var edited = Document.SetAttribute(1, "y", "12").SetAttribute(1, "text", "Say \"it's <here> & now\"");

        Assert.Contains("y='12'", edited.Text, StringComparison.Ordinal);
        Assert.Contains("""text="Say &quot;it's &lt;here> &amp; now&quot;" """.TrimEnd(), edited.Text, StringComparison.Ordinal);
        Assert.Equal("Say \"it's <here> & now\"", edited.Xml!.Children[1]["text"]);
    }

    [Fact]
    public void LineBreaksAndTabsAreWrittenAsReferencesSoTheyReadBack()
    {
        var edited = Document.SetAttribute(1, "text", "one\ntwo\tthree");

        Assert.Contains("text=\"one&#10;two&#9;three\"", edited.Text, StringComparison.Ordinal);
        Assert.Equal("one\ntwo\tthree", edited.Xml!.Children[1]["text"]);
    }

    [Fact]
    public void AValueThatAlreadyReadsTheSameIsLeftAsWritten()
    {
        Assert.Same(Document, Document.SetAttribute(1, "text", "Hi & bye"));
    }

    [Fact]
    public void ANewAttributeFollowsTheLastOnTheSameLine()
    {
        var edited = Document.SetAttribute(1, "alignment", "center");

        Assert.Contains("""text="Hi &amp; bye" alignment="center"/>""", edited.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ANewAttributeOfAnElementWrittenOnePerLineGetsItsOwnLine()
    {
        var edited = Document.SetAttribute(0, "resizable", "true");

        Assert.Contains("""
            <Window width="200"
                      height="120"
                      resizable="true"/>
            """, edited.Text, StringComparison.Ordinal);
        Assert.True(edited.Script!.Window.IsResizable);
    }

    [Fact]
    public void ANewAttributeTakesTheQuotesOfTheOneBeforeIt()
    {
        var document = UIDocument.Read("<UIScript><Window width='10' height='10'/></UIScript>");

        Assert.Equal("<UIScript><Window width='10' height='10' subtitle='Hello'/></UIScript>",
            document.SetAttribute(0, "subtitle", "Hello").Text);
    }

    [Fact]
    public void TheRootTakesAttributesToo()
    {
        var edited = Document.SetAttribute(UIDocument.Root, "requires", "menus");

        Assert.Contains("""<UIScript version="1" requires="menus">""", edited.Text, StringComparison.Ordinal);
        Assert.Equal(["menus"], edited.Script!.RequiredFeatures);
    }

    [Fact]
    public void AnAttributeOnAnElementWithNoneFollowsItsName()
    {
        var document = UIDocument.Read("<UIScript>\n  <Window width=\"10\" height=\"10\"/>\n</UIScript>");

        Assert.Equal("<UIScript version=\"1\">\n  <Window width=\"10\" height=\"10\"/>\n</UIScript>",
            document.SetAttribute(UIDocument.Root, "version", "1").Text);
    }

    [Theory]
    [InlineData("note", """<Text name="t" x="0" y="0" width="9" height="9" hint='mid' text="end"/>""")]
    [InlineData("hint", """<Text note="first" name="t" x="0" y="0" width="9" height="9" text="end"/>""")]
    [InlineData("text", """<Text note="first" name="t" x="0" y="0" width="9" height="9" hint='mid'/>""")]
    public void RemovingAnAttributeTakesTheSpaceBeforeIt(string attribute, string expected)
    {
        // Attributes VentureOS doesn't know are kept and skipped, so they can be removed from anywhere.
        const string text = """<UIScript><Window width="10" height="10"/><Text note="first" name="t" x="0" y="0" width="9" height="9" hint='mid' text="end"/></UIScript>""";

        var edited = UIDocument.Read(text).RemoveAttribute(1, attribute);

        Assert.Equal($"""<UIScript><Window width="10" height="10"/>{expected}</UIScript>""", edited.Text);
    }

    [Fact]
    public void RemovingAnAttributeOnItsOwnLineTakesTheLine()
    {
        var edited = Document.SetAttribute(0, "resizable", "true").RemoveAttribute(0, "resizable");

        Assert.Equal(Text, edited.Text);
    }

    [Fact]
    public void RemovingAnAttributeThatIsntThereChangesNothing()
    {
        Assert.Same(Document, Document.RemoveAttribute(1, "missing"));
    }

    [Fact]
    public void AnEditVentureOSWouldRefuseIsTurnedDownWithTheReason()
    {
        var exception = Assert.Throws<UIScriptException>(() => Document.SetAttribute(0, "width", "wide"));

        Assert.Contains("width", exception.Message, StringComparison.Ordinal);
        Assert.Equal(Text, Document.Text);
        Assert.Contains("needs name", Assert.Throws<UIScriptException>(() => Document.RemoveAttribute(1, "name")).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADocumentForTextEditingOnlyTakesNoEdits()
    {
        var refused = UIDocument.Read("<UIScript><Window width=\"1\" height=\"1\"/><Hologram/></UIScript>");

        var exception = Assert.Throws<InvalidOperationException>(() => refused.SetAttribute(0, "width", "2"));

        Assert.Contains("Hologram", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("2x")]
    [InlineData("a b")]
    [InlineData("a=\"1\"")]
    public void ANameThatIsntOneIsRefused(string name)
    {
        Assert.Throws<ArgumentException>(() => Document.SetAttribute(1, name, "1"));
        Assert.Throws<ArgumentException>(() => Document.RemoveAttribute(1, name));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(-2)]
    public void AnElementThatIsntThereIsRefused(int element)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Document.SetAttribute(element, "x", "1"));
    }

    [Fact]
    public void EncodingRefusesAQuoteThatIsntOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FlatXml.EncodeValue("x", '`'));
    }

    [Theory]
    [MemberData(nameof(UIDocumentTests.Documents), MemberType = typeof(UIDocumentTests))]
    public void EveryShippedAttributeSetToANewValueAndBackLeavesTheDocumentAsItWas(string file)
    {
        var document = UIDocument.Load(Path.Combine(AppContext.BaseDirectory, file));

        for (var index = 0; index < document.Xml!.Children.Count; index++)
        {
            var element = document.Xml.Children[index];
            foreach (var (name, value) in element.Attributes)
            {
                // The new value has every character that needs encoding, in a name nothing reads, so it's accepted.
                var round = document.SetAttribute(index, "roundTrip", "<&\"'\n>")
                    .RemoveAttribute(index, "roundTrip")
                    .SetAttribute(index, name, value);

                Assert.Equal(document.Text, round.Text);
            }
        }
    }

    [Theory]
    [MemberData(nameof(UIDocumentTests.Documents), MemberType = typeof(UIDocumentTests))]
    public void EveryShippedControlMovesWithOnlyItsXChanged(string file)
    {
        var document = UIDocument.Load(Path.Combine(AppContext.BaseDirectory, file));

        for (var index = 0; index < document.Xml!.Children.Count; index++)
        {
            var element = document.Xml.Children[index];
            if (element["x"] is not { } x || !int.TryParse(x, out var position))
                continue;

            var moved = document.SetAttribute(index, "x", (position + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));

            Assert.Equal(document.Text.Length + (position + 1).ToString(System.Globalization.CultureInfo.InvariantCulture).Length - x.Length, moved.Text.Length);
            Assert.Equal(document.Text, moved.SetAttribute(index, "x", x).Text);
        }
    }
}
