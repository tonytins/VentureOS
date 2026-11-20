using Venture.OS.Interface;

namespace Venture.OS.Tests.Interface;

/// <summary>
/// Adding, removing, and moving elements in a <see cref="UIDocument"/>: laid out as their neighbours are, with
/// comments left where they were and nothing VentureOS would refuse let through.
/// </summary>
public sealed class UIDocumentElementTests
{
    private const string Text = """
        <?xml version="1.0" encoding="UTF-8"?>
        <UIScript version="1">
          <!-- The window. -->
          <Window width="200" height="120"/>
          <Text name="greeting" x="8" y="8" width="100" height="20" text="Hi"/>
          <!-- The button. -->
          <Button name="ok" x="8" y="40" width="60"
                  height="24" text="OK"></Button>
        </UIScript>
        """;

    private static readonly UIDocument Document = UIDocument.Read(Text);

    private static readonly KeyValuePair<string, string>[] Cancel =
    [
        new("name", "cancel"), new("x", "76"), new("y", "40"), new("width", "60"), new("height", "24"), new("text", "Can't & won't"),
    ];

    private const string CancelText = """<Button name="cancel" x="76" y="40" width="60" height="24" text="Can't &amp; won't"/>""";

    [Fact]
    public void AnAddedElementAtTheEndGetsItsOwnLineIndentedAsTheOthers()
    {
        var edited = Document.InsertElement(3, "Button", Cancel);

        Assert.Equal(Text.Replace("""text="OK"></Button>""", $"""text="OK"></Button>{"\n"}  {CancelText}""", StringComparison.Ordinal), edited.Text);
        Assert.Equal(["greeting", "ok", "cancel"], edited.Script!.Controls.Select(control => control.Name));
        Assert.Equal("Can't & won't", edited.Script.Control("cancel")!.Text!.Value);
    }

    [Fact]
    public void AnAddedElementFollowsTheOneBeforeItSoACommentStaysWithTheOneAfter()
    {
        var edited = Document.InsertElement(2, "Button", Cancel);

        Assert.Contains($"""
              <Text name="greeting" x="8" y="8" width="100" height="20" text="Hi"/>
              {CancelText}
              <!-- The button. -->
              <Button name="ok"
            """, edited.Text, StringComparison.Ordinal);
        Assert.Equal(["greeting", "cancel", "ok"], edited.Script!.Controls.Select(control => control.Name));
    }

    [Fact]
    public void AnElementAddedFirstFollowsTheRootsStartTag()
    {
        var edited = Document.InsertElement(0, "Style", [new("name", "title"), new("fontSize", "18")]);

        Assert.Contains("""
            <UIScript version="1">
              <Style name="title" fontSize="18"/>
              <!-- The window. -->
            """, edited.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ElementsSharingALineGetANewOneOnTheirLine()
    {
        var document = UIDocument.Read("""<UIScript><Window width="10" height="10"/></UIScript>""");

        Assert.Equal("""<UIScript><Window width="10" height="10"/><Region name="r" x="0" y="0" width="5" height="5"/></UIScript>""",
            document.InsertElement(1, "Region", [new("name", "r"), new("x", "0"), new("y", "0"), new("width", "5"), new("height", "5")]).Text);
    }

    [Fact]
    public void AnAddedElementUsesTheDocumentsLineBreaks()
    {
        var document = UIDocument.Read("<UIScript>\r\n  <Window width=\"10\" height=\"10\"/>\r\n</UIScript>\r\n");

        Assert.Equal("<UIScript>\r\n  <Window width=\"10\" height=\"10\"/>\r\n  <Style name=\"s\"/>\r\n</UIScript>\r\n",
            document.InsertElement(1, "Style", [new("name", "s")]).Text);
    }

    [Fact]
    public void RemovingAnElementOnItsOwnLineTakesTheLineAndLeavesTheComments()
    {
        var edited = Document.RemoveElement(1);

        Assert.Equal(Text.Replace("""  <Text name="greeting" x="8" y="8" width="100" height="20" text="Hi"/>""" + "\n", "", StringComparison.Ordinal), edited.Text);
    }

    [Fact]
    public void RemovingAnElementTakesItsLinesAndTheCommentAboveIt()
    {
        var edited = Document.RemoveElement(2);

        Assert.Equal("""
            <?xml version="1.0" encoding="UTF-8"?>
            <UIScript version="1">
              <!-- The window. -->
              <Window width="200" height="120"/>
              <Text name="greeting" x="8" y="8" width="100" height="20" text="Hi"/>
            </UIScript>
            """, edited.Text);
    }

    [Fact]
    public void AddingAnElementAndRemovingItLeavesTheDocumentAsItWas()
    {
        for (var index = 0; index <= 3; index++)
            Assert.Equal(Text, Document.InsertElement(index, "Style", [new("name", "s")]).RemoveElement(index).Text);
    }

    [Fact]
    public void MovingAnElementTakesTheCommentAboveItAndChangesWhatsInFront()
    {
        var edited = Document.MoveElement(2, 1);

        Assert.Equal("""
            <?xml version="1.0" encoding="UTF-8"?>
            <UIScript version="1">
              <!-- The window. -->
              <Window width="200" height="120"/>
              <!-- The button. -->
              <Button name="ok" x="8" y="40" width="60"
                      height="24" text="OK"></Button>
              <Text name="greeting" x="8" y="8" width="100" height="20" text="Hi"/>
            </UIScript>
            """, edited.Text);
        Assert.Equal(["ok", "greeting"], edited.Script!.Controls.Select(control => control.Name));
        Assert.Equal(Text, edited.MoveElement(1, 2).Text);
    }

    [Fact]
    public void StackedCommentsTravelButOnesPastABlankLineOrAfterAnotherElementStay()
    {
        var document = UIDocument.Read("""
            <UIScript>
              <Window width="10" height="10"/> <!-- About the window. -->
              <!-- A heading for what follows. -->

              <!-- First about the region. -->
              <!-- Then more. -->
              <Region name="r" x="0" y="0" width="5" height="5"/>
              <Style name="s"/>
            </UIScript>
            """);

        Assert.Equal("""
            <UIScript>
              <Window width="10" height="10"/> <!-- About the window. -->
              <!-- A heading for what follows. -->

              <Style name="s"/>
              <!-- First about the region. -->
              <!-- Then more. -->
              <Region name="r" x="0" y="0" width="5" height="5"/>
            </UIScript>
            """, document.MoveElement(1, 2).Text);
    }

    [Fact]
    public void MovingAnElementToWhereItIsChangesNothing()
    {
        Assert.Same(Document, Document.MoveElement(1, 1));
    }

    [Fact]
    public void AnEditVentureOSWouldRefuseIsTurnedDown()
    {
        Assert.Contains("more than one <Window>", Assert.Throws<UIScriptException>(() => Document.InsertElement(1, "Window", [new("width", "1"), new("height", "1")])).Message, StringComparison.Ordinal);
        Assert.Throws<UIScriptException>(() => Document.RemoveElement(0));
        Assert.Throws<UIScriptException>(() => Document.InsertElement(1, "Hologram", []));
        Assert.Throws<UIScriptException>(() => Document.InsertElement(1, "Button", [new("name", "ok"), new("x", "0"), new("y", "0"), new("width", "1"), new("height", "1")]));
        Assert.Equal(Text, Document.Text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("2Button")]
    [InlineData("Button/")]
    public void AnElementNameThatIsntOneIsRefused(string name)
    {
        Assert.Throws<ArgumentException>(() => Document.InsertElement(1, name, []));
    }

    [Fact]
    public void AttributesThatArentOnesAreRefused()
    {
        Assert.Throws<ArgumentException>(() => Document.InsertElement(1, "Style", [new("name", "a"), new("name", "b")]));
        Assert.Throws<ArgumentException>(() => Document.InsertElement(1, "Style", [new("a b", "1")]));
    }

    [Fact]
    public void PlacesThatArentThereAreRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Document.InsertElement(4, "Style", [new("name", "s")]));
        Assert.Throws<ArgumentOutOfRangeException>(() => Document.InsertElement(-1, "Style", [new("name", "s")]));
        Assert.Throws<ArgumentOutOfRangeException>(() => Document.RemoveElement(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => Document.MoveElement(0, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => Document.MoveElement(-1, 0));
    }

    [Fact]
    public void ADocumentForTextEditingOnlyTakesNoEdits()
    {
        var refused = UIDocument.Read("<UIScript><Window width=\"1\" height=\"1\"/><Hologram/></UIScript>");

        Assert.Throws<InvalidOperationException>(() => refused.RemoveElement(1));
        Assert.Throws<InvalidOperationException>(() => refused.MoveElement(0, 1));
        Assert.Throws<InvalidOperationException>(() => refused.InsertElement(0, "Style", [new("name", "s")]));
    }

    [Theory]
    [MemberData(nameof(UIDocumentTests.Documents), MemberType = typeof(UIDocumentTests))]
    public void EveryShippedElementMovedToTheFrontAndBackKeepsEveryElementAndComment(string file)
    {
        var document = UIDocument.Load(Path.Combine(AppContext.BaseDirectory, file));
        var moves = 0;

        // Each element moves among its siblings: those directly inside the root, or inside the same menu.
        foreach (var parent in (int[])[UIDocument.Root, .. Enumerable.Range(0, document.Xml!.Children.Count)])
        {
            var siblings = document.Xml.ChildrenOf(parent);
            for (var place = 0; place < siblings.Length - 1; place++)
            {
                UIDocument moved;
                try
                {
                    moved = document.MoveElement(siblings[place], siblings[^1]);
                }
                catch (UIScriptException)
                {
                    // Some elements must come before others, such as a style before the controls that use it.
                    continue;
                }

                moves++;
                var after = moved.Xml!.ChildrenOf(parent);
                Assert.Equal(Elements(document)[siblings[place]], Elements(moved)[after[^1]]);

                // Blank lines and comments stay where they were, so only the elements are sure to be as they were.
                var back = moved.MoveElement(after[^1], after[place]);
                Assert.Equal(Elements(document), Elements(back));
                Assert.Equal(Comments(document), Comments(back));
            }
        }

        // A document of one element, such as the menu bar's panel, has nothing to move.
        Assert.True(moves > 0 || document.Xml.Children.Count < 2);
    }

    [Theory]
    [MemberData(nameof(UIDocumentTests.Documents), MemberType = typeof(UIDocumentTests))]
    public void AnElementAddedAnywhereInAShippedDocumentAndRemovedLeavesItAsItWas(string file)
    {
        var document = UIDocument.Load(Path.Combine(AppContext.BaseDirectory, file));
        var xml = document.Xml!;

        // A style anywhere directly inside the root, and a separator anywhere inside each menu.
        foreach (var parent in (int[])[UIDocument.Root, .. Enumerable.Range(0, xml.Children.Count)])
        {
            string name;
            KeyValuePair<string, string>[] attributes = [];
            if (parent == UIDocument.Root)
            {
                name = "Style";
                attributes = [new("name", "roundTripStyle")];
            }
            else if (xml.Children[parent].Name == "Menu" && xml.Children[parent].Source!.CloseStart is not null)
            {
                name = "MenuSeparator";
            }
            else
            {
                continue;
            }

            foreach (var index in (int[])[.. xml.ChildrenOf(parent), xml.End(parent)])
            {
                var added = document.InsertElement(index, name, attributes, parent);

                Assert.Equal(name, added.Xml!.Children[index].Name);
                Assert.Equal(document.Text, added.RemoveElement(index).Text);
            }
        }
    }

    private static List<string> Elements(UIDocument document) =>
        [.. document.Xml!.Children.Select(element => element.Source!.Span.Of(document.Text))];

    private static List<string> Comments(UIDocument document) =>
        [.. System.Text.RegularExpressions.Regex.Matches(document.Text, "<!--.*?-->", System.Text.RegularExpressions.RegexOptions.Singleline).Select(match => match.Value)];
}
