using Venture.OS.Interface.Xml;

namespace Venture.OS.Tests.Xml;

/// <summary>Moving an element into and out of another with <see cref="FlatXmlEditor"/>, its lines indented afresh.</summary>
public sealed class FlatXmlEditorMoveTests
{
    private const string Text = """
        <UIScript>
          <Window width="1" height="1"/>
          <Pane name="general" title="General">
            <Text name="label" x="0" y="0" width="1" height="1"/>
          </Pane>
          <!-- Go, which belongs in General. -->
          <Button name="go" x="0" y="0"
                  width="1" height="1"/>
          <Menu name="file" title="File">
            <MenuItem name="open" title="Open"/>
          </Menu>
          <Menu name="more" title="More">
            <MenuItem name="deep" title="Deep"/>
          </Menu>
          <Menu name="empty" title="Empty"/>
        </UIScript>
        """;

    private static FlatXmlDocument Read(string text) => FlatXml.Parse(text, allowNesting: true);

    private static readonly FlatXmlDocument Document = Read(Text);

    [Fact]
    public void MovingIntoAnElementPutsItLastIndentedAsItsChildrenWithItsComment()
    {
        var (text, element) = FlatXmlEditor.MoveInto(Text, Document, 3, 1);

        Assert.Equal(3, element);
        Assert.Equal("""
            <UIScript>
              <Window width="1" height="1"/>
              <Pane name="general" title="General">
                <Text name="label" x="0" y="0" width="1" height="1"/>
                <!-- Go, which belongs in General. -->
                <Button name="go" x="0" y="0"
                        width="1" height="1"/>
              </Pane>
              <Menu name="file" title="File">
                <MenuItem name="open" title="Open"/>
              </Menu>
              <Menu name="more" title="More">
                <MenuItem name="deep" title="Deep"/>
              </Menu>
              <Menu name="empty" title="Empty"/>
            </UIScript>
            """, text);
        Assert.Equal(1, Read(text).Children[element].Parent);
    }

    [Fact]
    public void MovingOutPutsItJustAfterItsParentSoInAndOutIsNoChange()
    {
        var (inside, element) = FlatXmlEditor.MoveInto(Text, Document, 3, 1);
        var (outside, back) = FlatXmlEditor.MoveOut(inside, Read(inside), element);

        Assert.Equal(Text, outside);
        Assert.Equal(3, back);
        Assert.Equal(-1, Read(outside).Children[back].Parent);
    }

    [Fact]
    public void AMenuMovedIntoAnotherCarriesItsItemsAndIsIndentedAFurtherStep()
    {
        var (text, element) = FlatXmlEditor.MoveInto(Text, Document, 6, 4);

        Assert.Contains("""
              <Menu name="file" title="File">
                <MenuItem name="open" title="Open"/>
                <Menu name="more" title="More">
                  <MenuItem name="deep" title="Deep"/>
                </Menu>
              </Menu>
            """, text, StringComparison.Ordinal);
        var moved = Read(text);
        Assert.Equal(6, element);
        Assert.Equal((4, 1), (moved.Children[element].Parent, moved.Children[element].Depth));
        Assert.Equal(2, moved.Children[element + 1].Depth);
    }

    [Fact]
    public void MovingIntoAnElementWrittenEmptyOpensItUp()
    {
        var (text, element) = FlatXmlEditor.MoveInto(Text, Document, 3, 8);

        Assert.EndsWith("""
              <Menu name="empty" title="Empty">
                <!-- Go, which belongs in General. -->
                <Button name="go" x="0" y="0"
                        width="1" height="1"/>
              </Menu>
            </UIScript>
            """, text, StringComparison.Ordinal);
        Assert.Equal(7, Read(text).Children[element].Parent);
    }

    [Fact]
    public void AnElementMovedIntoOneBeforeItTakesItsPlaceInTheCount()
    {
        // The parent comes after it, so removing it first moves the parent back.
        var (text, element) = FlatXmlEditor.MoveInto(Text, Document, 3, 6);

        var moved = Read(text);
        Assert.Equal("Button", moved.Children[element].Name);
        Assert.Equal("more", moved.Children[moved.Children[element].Parent]["name"]);
    }

    [Theory]
    [InlineData(4, 4)] // Into itself.
    [InlineData(4, 5)] // Into what's inside it.
    [InlineData(3, 9)] // No such parent.
    [InlineData(9, 1)] // No such element.
    public void MovesThatCantBeAreRefused(int element, int parent) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => FlatXmlEditor.MoveInto(Text, Document, element, parent));

    [Fact]
    public void AnElementDirectlyInTheRootCantMoveOut() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => FlatXmlEditor.MoveOut(Text, Document, 3));

    [Fact]
    public void WindowsLineBreaksAreKept()
    {
        var text = Text.Replace("\n", "\r\n", StringComparison.Ordinal);

        var (inside, element) = FlatXmlEditor.MoveInto(text, Read(text), 3, 1);

        Assert.DoesNotContain("\n", inside.Replace("\r\n", "", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Equal(text, FlatXmlEditor.MoveOut(inside, Read(inside), element).Text);
    }
}
