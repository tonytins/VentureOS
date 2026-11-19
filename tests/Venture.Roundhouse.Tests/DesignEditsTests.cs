using Venture.OS.Interface;
using Venture.Roundhouse.Designer;

namespace Venture.Roundhouse.Tests;

/// <summary><see cref="DesignEdits"/>: the library, duplicating, and moving elements in front and behind.</summary>
public sealed class DesignEditsTests
{
    private const string Text = """
        <UIScript>
          <Window width="200" height="120"/>
          <Image name="sky" path="sky.png"/>
          <Visual name="back" image="sky" x="0" y="0" width="10" height="10"/>
          <Region name="a" x="10" y="10" width="20" height="20"/>
          <Style name="plain" fontSize="12"/>
          <Button name="b" x="20" y="20" width="20" height="20" text="B"/>
          <Visual name="front" image="sky" x="5" y="5" width="10" height="10"/>
        </UIScript>
        """;

    private static readonly UIDocument Document = UIDocument.Read(Text);

    [Fact]
    public void EveryLibraryControlCanBeAddedToAnEmptyWindow()
    {
        var empty = UIDocument.Read("""<UIScript><Window width="300" height="200"/></UIScript>""");

        foreach (var control in DesignEdits.Library)
        {
            var (edited, element) = DesignEdits.Add(empty, control.Element);

            Assert.Equal(control.Element, edited.Xml!.Children[element].Name);
            Assert.True(edited.IsEditable);
        }
    }

    [Fact]
    public void AnAddedControlIsCentredNamedAfterItsKindAndInFront()
    {
        var (edited, element) = DesignEdits.Add(Document, "Button");

        Assert.Equal(7, element);
        Assert.Contains("""<Button name="button" x="60" y="48" width="80" height="24" text="Button"/>""", edited.Text, StringComparison.Ordinal);
        Assert.Equal("button", edited.Script!.Controls[^1].Name);
    }

    [Fact]
    public void AControlAddedWhereAnotherStartsIsSetOffAndNamedApart()
    {
        var (once, _) = DesignEdits.Add(Document, "Button");
        var (twice, element) = DesignEdits.Add(once, "Button");

        Assert.Equal("button2", twice.Xml!.Children[element]["name"]);
        Assert.Equal(("68", "56"), (twice.Xml.Children[element]["x"], twice.Xml.Children[element]["y"]));
    }

    [Fact]
    public void TheLibraryRefusesWhatItDoesntHold()
    {
        Assert.Throws<ArgumentException>(() => DesignEdits.Add(Document, "Window"));
    }

    [Fact]
    public void ADuplicateIsJustInFrontSetOffWithANewName()
    {
        var (edited, element) = DesignEdits.Duplicate(Document, 5);

        Assert.Equal(6, element);
        Assert.Contains("""
              <Button name="b" x="20" y="20" width="20" height="20" text="B"/>
              <Button name="b2" x="28" y="28" width="20" height="20" text="B"/>
            """, edited.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ADuplicatesNameCountsOnFromTheOriginals()
    {
        var (once, copy) = DesignEdits.Duplicate(Document, 5);
        var (twice, again) = DesignEdits.Duplicate(once, copy);

        Assert.Equal("b3", twice.Xml!.Children[again]["name"]);
    }

    [Fact]
    public void AnElementWithoutANameIsntDuplicatedAndAStyleIsCopiedWhereItIs()
    {
        Assert.Throws<ArgumentException>(() => DesignEdits.Duplicate(Document, 0));

        var (edited, element) = DesignEdits.Duplicate(Document, 4);
        Assert.Equal("""<Style name="plain2" fontSize="12"/>""", edited.Xml!.Children[element].Source!.Span.Of(edited.Text));
    }

    [Fact]
    public void ForwardAndBackwardPassOverWhatIsntDrawn()
    {
        var (forward, at) = DesignEdits.BringForward(Document, 3);
        Assert.Equal(5, at);
        Assert.Equal(["b", "a"], forward.Script!.Controls.Select(control => control.Name));

        // Back again, the controls are in their first order; the style between them may end up on the other side.
        var (backward, back) = DesignEdits.SendBackward(forward, 5);
        Assert.Equal(4, back);
        Assert.Equal(["a", "b"], backward.Script!.Controls.Select(control => control.Name));
    }

    [Fact]
    public void AControlAlreadyInFrontOrAtTheBackStays()
    {
        Assert.Same(Document, DesignEdits.BringForward(Document, 5).Document);
        Assert.Same(Document, DesignEdits.SendBackward(Document, 3).Document);
    }

    [Fact]
    public void VisualsMoveOnlyAmongVisualsSinceTheyreAllBehindTheControls()
    {
        var (edited, at) = DesignEdits.BringForward(Document, 2);

        Assert.Equal(6, at);
        Assert.Equal(["front", "back"], edited.Script!.Visuals.Select(visual => visual.Name));
    }

    [Theory]
    [InlineData("button", "button")]
    [InlineData("b", "b2")]
    [InlineData("sky", "sky2")]
    public void UniqueNamesAvoidEveryNameInTheDocument(string stem, string expected)
    {
        Assert.Equal(expected, DesignEdits.UniqueName(Document, stem));
    }

    [Fact]
    public void APaneGoesAfterTheLastPaneAndAddsTheFeature()
    {
        var withSidebar = UIDocument.Read("""
            <UIScript requires="sidebar">
              <Window width="300" height="200"/>
              <Sidebar name="side" width="100"/>
              <Text name="t" x="0" y="0" width="10" height="10"/>
            </UIScript>
            """);

        var (first, one) = DesignEdits.Add(withSidebar, "Pane", null);
        var (second, two) = DesignEdits.Add(first, "Pane", null);

        Assert.Equal(2, one);
        Assert.Equal(3, two);
        Assert.Contains(UIScript.PanesFeature, second.Script!.RequiredFeatures);
        Assert.Equal(["pane", "pane2"], second.Script.Panes.Select(pane => pane.Name));
    }

    [Fact]
    public void APaneNeedsASidebar()
    {
        var plain = UIDocument.Read("""<UIScript><Window width="300" height="200"/></UIScript>""");

        Assert.Contains("add a sidebar first", Assert.Throws<UIScriptException>(() => DesignEdits.Add(plain, "Pane", null)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AControlJoinsThePaneItIsGiven()
    {
        var withPanes = UIDocument.Read("""
            <UIScript requires="sidebar panes">
              <Window width="300" height="200"/>
              <Sidebar name="side" width="100"/>
              <Pane name="general" title="General"/>
            </UIScript>
            """);

        var (edited, element) = DesignEdits.Add(withPanes, "Button", null, "general");

        Assert.Equal("general", edited.Xml!.Children[element]["pane"]);
        Assert.Equal("general", edited.Script!.Controls.Single().Pane);
    }
}
