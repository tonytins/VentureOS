using Venture.OS.Interface.Xml;

namespace Venture.OS.Tests.Xml;

public sealed class FlatXmlTests
{
    [Fact]
    public void ReadsARootAndItsChildrenInOrder()
    {
        var document = FlatXml.Parse("""
            <?xml version="1.0" encoding="UTF-8"?>
            <!-- A comment before the root. -->
            <UIScript version="1">
              <Window width="400" height="255"/>
              <!-- Comments are fine anywhere. -->
              <Button name="send" x='345' y="212"></Button>
            </UIScript>
            <!-- And after it. -->
            """);

        Assert.Equal("UIScript", document.Root.Name);
        Assert.Equal("1", document.Root["version"]);
        Assert.Equal(["Window", "Button"], document.Children.Select(child => child.Name));
        Assert.Equal(["name", "x", "y"], document.Children[1].Attributes.Select(attribute => attribute.Key));
        Assert.Equal("345", document.Children[1]["x"]);
        Assert.Null(document.Children[1]["missing"]);
        Assert.Equal([3, 4, 6], [document.Root.Line, document.Children[0].Line, document.Children[1].Line]);
    }

    [Fact]
    public void ReadsAnEmptyRoot()
    {
        var document = FlatXml.Parse("<App/>");

        Assert.Equal("App", document.Root.Name);
        Assert.Empty(document.Children);
    }

    [Theory]
    [InlineData("&lt;&gt;&amp;&quot;&apos;", "<>&\"'")]
    [InlineData("caf&#233; &#x2603;", "café ☃")]
    [InlineData("a&#x1F600;b", "a😀b")]
    [InlineData("line\none\ttab", "line one tab")]
    public void DecodesEntitiesAndWhitespaceInValues(string raw, string expected)
    {
        var document = FlatXml.Parse($"<A text=\"{raw}\"/>");

        Assert.Equal(expected, document.Root["text"]);
    }

    [Fact]
    public void AcceptsAByteOrderMark() => Assert.Equal("A", FlatXml.Parse("\uFEFF<A/>").Root.Name);

    [Theory]
    [InlineData("", 1, "empty")]
    [InlineData("<!DOCTYPE x><x/>", 1, "DOCTYPE")]
    [InlineData("<A>\n<B>hello</B></A>", 2, "can't hold text")]
    [InlineData("<A>\n<B><C/></B></A>", 2, "can't hold text or other elements")]
    [InlineData("<A>words</A>", 1, "Text isn't allowed")]
    [InlineData("<A><![CDATA[x]]></A>", 1, "CDATA")]
    [InlineData("<A><?pi x?></A>", 1, "Processing instructions")]
    [InlineData("<A>\n<B/>", 2, "never closed")]
    [InlineData("<A></B>", 1, "</B> closes <A>")]
    [InlineData("<A x=1/>", 1, "must be in quotes")]
    [InlineData("<A x/>", 1, "needs a value")]
    [InlineData("<A x='1'y='2'/>", 1, "Expected a space")]
    [InlineData("<A x='1' x='2'/>", 1, "twice")]
    [InlineData("<A x='a<b'/>", 1, "can't contain <")]
    [InlineData("<A x='&nbsp;'/>", 1, "isn't a known entity")]
    [InlineData("<A x='&#0;'/>", 1, "isn't a valid character reference")]
    [InlineData("<A x='&#xD800;'/>", 1, "isn't a valid character reference")]
    [InlineData("<A x='a & b'/>", 1, "An & must start an entity")]
    [InlineData("<A x='open/>", 1, "never closed")]
    [InlineData("<A/>\n<B/>", 2, "Nothing may follow")]
    [InlineData("<!-- open", 1, "comment is never closed")]
    [InlineData("<1A/>", 1, "Expected an element name")]
    public void RefusesWhatIsntFlatXmlWithTheLine(string text, int line, string message)
    {
        var exception = Assert.Throws<FlatXmlException>(() => FlatXml.Parse(text));

        Assert.Equal(line, exception.Line);
        Assert.Contains(message, exception.Reason);
        Assert.StartsWith($"Line {line}: ", exception.Message);
    }

    [Fact]
    public void LoadsAUtf8File()
    {
        var path = Path.Combine(Path.GetTempPath(), $"venture-flat-{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, "<A name='Café'/>");
        try
        {
            Assert.Equal("Café", FlatXml.Load(path).Root["name"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => FlatXml.Parse(null!));
        Assert.Throws<ArgumentNullException>(() => FlatXml.Load(null!));
    }
}
