using Venture.OS.Interface.Xml;

namespace Venture.OS.Tests.Xml;

/// <summary>How <see cref="FlatXml"/> reads elements inside elements, when its caller allows them.</summary>
public sealed class FlatXmlNestingTests
{
    private const string Nested = """
        <UIScript>
          <Window width="1" height="1"/>
          <Menu name="file">
            <MenuItem name="open"/>
            <Menu name="recent">
              <MenuItem name="one"/>
            </Menu>
            <MenuSeparator/>
          </Menu>
          <Button name="ok"/>
        </UIScript>
        """;

    private static readonly FlatXmlDocument Document = FlatXml.Parse(Nested, allowNesting: true);

    [Fact]
    public void ElementsAreInDocumentOrderEachBeforeThoseInsideIt()
    {
        Assert.Equal(
            ["Window", "Menu", "MenuItem", "Menu", "MenuItem", "MenuSeparator", "Button"],
            Document.Children.Select(child => child.Name));
        Assert.Equal([-1, -1, 1, 1, 3, 1, -1], Document.Children.Select(child => child.Parent));
        Assert.Equal([0, 0, 1, 1, 2, 1, 0], Document.Children.Select(child => child.Depth));
    }

    [Fact]
    public void EndAndChildrenOfRecoverTheStructure()
    {
        Assert.Equal(6, Document.End(1));
        Assert.Equal(5, Document.End(3));
        Assert.Equal(1, Document.End(0));
        Assert.Equal(7, Document.End(FlatXmlDocument.RootIndex));
        Assert.Equal([2, 3, 5], Document.ChildrenOf(1));
        Assert.Equal([0, 1, 6], Document.ChildrenOf(FlatXmlDocument.RootIndex));
        Assert.Empty(Document.ChildrenOf(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Document.End(7));
    }

    [Fact]
    public void AnElementsSpanHoldsEverythingInsideIt()
    {
        Assert.Equal("""
            <Menu name="recent">
                  <MenuItem name="one"/>
                </Menu>
            """, Document.Children[3].Source!.Span.Of(Nested));
        Assert.NotNull(Document.Children[3].Source!.CloseStart);
    }

    [Fact]
    public void AFlatDocumentReadsTheSameEitherWay()
    {
        const string flat = """<A><B x="1"/><C></C><!-- c --><D/></A>""";
        var plain = FlatXml.Parse(flat);
        var allowing = FlatXml.Parse(flat, allowNesting: true);

        Assert.Equal(plain.Children.Select(child => (child.Name, child.Source!.Span, child.Source.CloseStart)), allowing.Children.Select(child => (child.Name, child.Source!.Span, child.Source.CloseStart)));
        Assert.All(allowing.Children, child => Assert.Equal((-1, 0), (child.Parent, child.Depth)));
    }

    [Fact]
    public void NestingIsRefusedUnlessAllowedAsBefore()
    {
        // Info.xml and the settings files don't nest, so they keep refusing it.
        var exception = Assert.Throws<FlatXmlException>(() => FlatXml.Parse("<A>\n<B>\n<C/></B></A>"));

        Assert.Equal("<B> can't hold text or other elements.", exception.Reason);
        Assert.Equal(3, exception.Line);
    }

    [Fact]
    public void TextIsRefusedInsideANestedElement()
    {
        var exception = Assert.Throws<FlatXmlException>(() => FlatXml.Parse("<A><B><C/>words</B></A>", allowNesting: true));

        Assert.Equal("Text isn't allowed inside <B>; put it in an attribute.", exception.Reason);
    }

    [Theory]
    [InlineData("<A><B><C></B></C></A>", "</B> closes <C>")]
    [InlineData("<A><B><C/></A>", "</A> closes <B>")]
    [InlineData("<A><B><C/>", "<B> is never closed")]
    public void BrokenNestingIsRefused(string text, string message) =>
        Assert.Contains(message, Assert.Throws<FlatXmlException>(() => FlatXml.Parse(text, allowNesting: true)).Reason, StringComparison.Ordinal);

    [Fact]
    public void NestingStopsAtTheMaximumDepth()
    {
        string Deep(int levels) =>
            "<R>" + string.Concat(Enumerable.Repeat("<E>", levels)) + string.Concat(Enumerable.Repeat("</E>", levels)) + "</R>";

        Assert.Equal(FlatXml.MaximumDepth, FlatXml.Parse(Deep(FlatXml.MaximumDepth), allowNesting: true).Children.Count);
        var exception = Assert.Throws<FlatXmlException>(() => FlatXml.Parse(Deep(FlatXml.MaximumDepth + 1), allowNesting: true));
        Assert.Contains($"more than {FlatXml.MaximumDepth} deep", exception.Reason, StringComparison.Ordinal);

        // A hostile document is refused, not a stack overflow.
        Assert.Throws<FlatXmlException>(() => FlatXml.Parse(Deep(100_000), allowNesting: true));
    }
}
