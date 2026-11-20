using Venture.OS.Interface.Xml;

namespace Venture.OS.Tests.Xml;

/// <summary>Editing nested documents in place with <see cref="FlatXmlEditor"/>.</summary>
public sealed class FlatXmlEditorNestingTests
{
    private const string Text = """
        <UIScript>
          <Window width="1" height="1"/>
          <Menu name="file">
            <MenuItem name="open"/>
            <!-- Recent files. -->
            <Menu name="recent">
              <MenuItem name="one"/>
            </Menu>
            <MenuSeparator/>
          </Menu>
          <Button name="ok"/>
        </UIScript>
        """;

    private static FlatXmlDocument Read(string text) => FlatXml.Parse(text, allowNesting: true);

    private static readonly FlatXmlDocument Document = Read(Text);

    [Fact]
    public void AttributesOfANestedElementChangeInPlace()
    {
        var edited = FlatXmlEditor.SetAttribute(Text, Document, 4, "name", "two");

        Assert.Equal(Text.Replace("\"one\"", "\"two\"", StringComparison.Ordinal), edited);
    }

    [Fact]
    public void AnElementAddedAtTheEndOfAParentFollowsItsLastChildIndentedAsItIs()
    {
        var edited = FlatXmlEditor.InsertElement(Text, Document, Document.End(1), "MenuItem", [new("name", "quit")], parent: 1);

        Assert.Equal(Text.Replace("""
                <MenuSeparator/>
            """, """
                <MenuSeparator/>
                <MenuItem name="quit"/>
            """, StringComparison.Ordinal), edited);
        Assert.Equal(1, Read(edited).Children[6].Parent);
    }

    [Fact]
    public void AnElementAddedBeforeASiblingGoesBeforeItsComment()
    {
        var edited = FlatXmlEditor.InsertElement(Text, Document, 3, "MenuItem", [new("name", "save")], parent: 1);

        Assert.Equal(Text.Replace("""
                <MenuItem name="open"/>
            """, """
                <MenuItem name="open"/>
                <MenuItem name="save"/>
            """, StringComparison.Ordinal), edited);
    }

    [Fact]
    public void AnElementWrittenEmptyIsOpenedUpToHoldOne()
    {
        const string text = "<UIScript>\n  <Menu name=\"m\" />\n</UIScript>";

        var edited = FlatXmlEditor.InsertElement(text, Read(text), 1, "MenuItem", [new("name", "a")], parent: 0);

        Assert.Equal("<UIScript>\n  <Menu name=\"m\">\n    <MenuItem name=\"a\"/>\n  </Menu>\n</UIScript>", edited);
    }

    [Theory]
    [InlineData("<UIScript>\n  <Menu name=\"m\"></Menu>\n</UIScript>")]
    [InlineData("<UIScript>\n  <Menu name=\"m\">\n  </Menu>\n</UIScript>")]
    public void AnEmptyParentGetsItsChildOnALineOfItsOwn(string text)
    {
        var edited = FlatXmlEditor.InsertElement(text, Read(text), 1, "MenuItem", [new("name", "a")], parent: 0);

        Assert.Equal("<UIScript>\n  <Menu name=\"m\">\n    <MenuItem name=\"a\"/>\n  </Menu>\n</UIScript>", edited);
    }

    [Fact]
    public void AnElementAddedToTheRootStillGoesAfterNestedOnes()
    {
        var edited = FlatXmlEditor.InsertElement(Text, Document, 6, "Text", [new("name", "t")]);

        Assert.Equal(Text.Replace("""
              </Menu>
              <Button
            """, """
              </Menu>
              <Text name="t"/>
              <Button
            """, StringComparison.Ordinal), edited);
    }

    [Theory]
    [InlineData(4, 1)] // Inside the submenu, not the menu.
    [InlineData(2, -1)] // Inside the menu, not the root.
    [InlineData(8, -1)] // Past the end.
    public void APlaceThatIsntInsideTheParentIsRefused(int index, int parent) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FlatXmlEditor.InsertElement(Text, Document, index, "MenuItem", [], parent));

    [Fact]
    public void RemovingAnElementTakesEverythingInsideItAndItsComment()
    {
        var edited = FlatXmlEditor.RemoveElement(Text, Document, 3);

        Assert.Equal("""
            <UIScript>
              <Window width="1" height="1"/>
              <Menu name="file">
                <MenuItem name="open"/>
                <MenuSeparator/>
              </Menu>
              <Button name="ok"/>
            </UIScript>
            """, edited);
    }

    [Fact]
    public void RemovingTheOnlyChildLeavesTheParentOpen()
    {
        var edited = FlatXmlEditor.RemoveElement(Text, Document, 4);

        Assert.Contains("""
                <Menu name="recent">
                </Menu>
            """, edited, StringComparison.Ordinal);
    }

    [Fact]
    public void MovingAnElementCarriesEverythingInsideItAmongItsSiblings()
    {
        var back = FlatXmlEditor.MoveElement(Text, Document, 3, 2);

        Assert.Equal("""
            <UIScript>
              <Window width="1" height="1"/>
              <Menu name="file">
                <!-- Recent files. -->
                <Menu name="recent">
                  <MenuItem name="one"/>
                </Menu>
                <MenuItem name="open"/>
                <MenuSeparator/>
              </Menu>
              <Button name="ok"/>
            </UIScript>
            """, back);

        // And forward again, past its old place, puts it after the separator.
        var moved = Read(back);
        var forward = FlatXmlEditor.MoveElement(back, moved, 2, moved.ChildrenOf(1)[^1]);
        Assert.Equal(["MenuItem", "MenuSeparator", "Menu"], Read(forward).ChildrenOf(1).Select(index => Read(forward).Children[index].Name));
    }

    [Fact]
    public void AMenuMovedAmongTheRootsElementsKeepsItsInside()
    {
        var edited = FlatXmlEditor.MoveElement(Text, Document, 1, 6);
        var moved = Read(edited);

        Assert.Equal(["Window", "Button", "Menu"], moved.ChildrenOf(FlatXmlDocument.RootIndex).Select(index => moved.Children[index].Name));
        Assert.Equal(7, moved.Children.Count);
        Assert.Contains("<!-- Recent files. -->", edited, StringComparison.Ordinal);
    }

    [Fact]
    public void ElementsInDifferentParentsCantSwapPlaces() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => FlatXmlEditor.MoveElement(Text, Document, 4, 2));

    [Fact]
    public void EveryElementAddedAnywhereAndRemovedLeavesTheTextAsItWas()
    {
        for (var parent = -1; parent < Document.Children.Count; parent++)
        {
            foreach (var index in (int[])[.. Document.ChildrenOf(parent), Document.End(parent)])
            {
                var added = FlatXmlEditor.InsertElement(Text, Document, index, "Probe", [], parent);
                var read = Read(added);
                Assert.Equal("Probe", read.Children[index].Name);
                Assert.Equal(parent, read.Children[index].Parent);

                // Opening up an empty element can't be undone by removing, so only the others must round-trip.
                if (parent < 0 || Document.Children[parent].Source!.CloseStart is not null)
                    Assert.Equal(Text, FlatXmlEditor.RemoveElement(added, read, index));
            }
        }
    }
}
