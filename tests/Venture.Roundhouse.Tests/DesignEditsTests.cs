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
        Assert.Equal(["pane", "pane2"], second.Script!.Panes.Select(pane => pane.Name));
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

        // An empty pane is opened up to hold it, the form new documents take.
        Assert.Equal(2, edited.Xml!.Children[element].Parent);
        Assert.Null(edited.Xml.Children[element]["pane"]);
        Assert.Contains("""<Pane name="general" title="General">""", edited.Text, StringComparison.Ordinal);
        Assert.Equal("general", edited.Script!.Controls.Single().Pane);
    }

    [Fact]
    public void APictureIsAnImageAmongTheResourcesAndAVisualShowingIt()
    {
        var plain = UIDocument.Read("""
            <UIScript>
              <Window width="300" height="200"/>
              <Style name="plain" fontSize="12"/>
              <Button name="ok" x="0" y="0" width="10" height="10"/>
            </UIScript>
            """);

        var (edited, element) = DesignEdits.Add(plain, "Visual", null);

        var xml = edited.Xml!;
        Assert.Equal(["Window", "Style", "Image", "Button", "Visual"], xml.ChildrenOf(UIDocument.Root).Select(child => xml.Children[child].Name));
        Assert.Equal("Visual", xml.Children[element].Name);
        var visual = Assert.Single(edited.Script!.Visuals);
        Assert.Equal(("picture", "picture", null), (visual.Name, visual.Image, visual.Pane));
        Assert.Equal("picture.png", edited.Script.Images["picture"].Path);
        Assert.Equal((118, 68, 64, 64), ((int)visual.Frame.X, (int)visual.Frame.Y, (int)visual.Frame.Width!, (int)visual.Frame.Height!));
    }

    [Fact]
    public void APictureGoesInThePaneOnShowAndItsImageStaysAtTheRoot()
    {
        var (edited, element) = DesignEdits.Add(UIDocument.Read(NestedPanes), "Visual", null, "advanced");

        var xml = edited.Xml!;
        Assert.Equal("Pane", xml.Children[xml.Children[element].Parent].Name);
        Assert.Equal("advanced", Assert.Single(edited.Script!.Visuals).Pane);
        Assert.Equal(UIDocument.Root, xml.Children.Single(child => child.Name == "Image").Parent);

        // A second one takes a name of its own, for both its image and its visual.
        var (again, _) = DesignEdits.Add(edited, "Visual", null, "advanced");
        Assert.Equal(["picture", "picture2"], again.Script!.Visuals.Select(visual => visual.Image));
    }

    [Fact]
    public void APictureCantGoInAPaneWhoseControlsNameItFromOutside()
    {
        var flat = UIDocument.Read("""
            <UIScript>
              <Window width="300" height="200"/>
              <Sidebar name="side" width="100"/>
              <Pane name="general" title="General"/>
              <Button name="ok" x="0" y="0" width="10" height="10" pane="general"/>
            </UIScript>
            """);

        var problem = Assert.Throws<UIScriptException>(() => DesignEdits.Add(flat, "Visual", null, "general"));

        Assert.Contains("Move its controls into it first", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AVisualMovesIntoThePaneBeforeIt()
    {
        var document = UIDocument.Read("""
            <UIScript>
              <Window width="300" height="200"/>
              <Image name="logo" path="logo.png"/>
              <Sidebar name="side" width="100"/>
              <Pane name="about" title="About"/>
              <Visual name="icon" image="logo" x="0" y="0" width="64" height="64"/>
            </UIScript>
            """);

        var (moved, element) = DesignEdits.MoveInto(document, 4);

        Assert.Equal(3, moved.Xml!.Children[element].Parent);
        Assert.Equal("about", Assert.Single(moved.Script!.Visuals).Pane);
    }

    private const string Player = """
        <UIScript>
          <Window width="300" height="200"/>
          <Sidebar name="player" width="160" edge="right">
            <Button name="stop" x="10" y="10" width="80" height="24" text="Stop"/>
          </Sidebar>
          <List name="stations" x="10" y="10" width="280" height="180"/>
        </UIScript>
        """;

    [Fact]
    public void AControlGoesInTheSidebarWhenItsSelectedCentredThere()
    {
        var (edited, element) = DesignEdits.Add(UIDocument.Read(Player), "Button", 1);

        var xml = edited.Xml!;
        Assert.Equal(1, xml.Children[element].Parent);
        var button = edited.Script!.Control("button")!;
        Assert.True(button.InSidebar);
        Assert.Equal((40, 88), ((int)button.Frame.X, (int)button.Frame.Y));
    }

    [Fact]
    public void AControlGoesInTheSidebarWhenSomethingInItIsSelected()
    {
        var (edited, element) = DesignEdits.Add(UIDocument.Read(Player), "Text", 2);

        Assert.Equal(1, edited.Xml!.Children[element].Parent);
        Assert.Equal(["stop", "text"], edited.Script!.Controls.Where(control => control.InSidebar).Select(control => control.Name));
    }

    [Fact]
    public void AControlGoesInTheContentWhenTheContentIsSelected()
    {
        var (edited, _) = DesignEdits.Add(UIDocument.Read(Player), "Button", 3);

        Assert.False(edited.Script!.Control("button")!.InSidebar);
    }

    [Fact]
    public void APictureGoesInTheSidebarWithItsImageAtTheRoot()
    {
        var (edited, element) = DesignEdits.Add(UIDocument.Read(Player), "Visual", 1);

        var xml = edited.Xml!;
        Assert.Equal("Sidebar", xml.Children[xml.Children[element].Parent].Name);
        Assert.True(Assert.Single(edited.Script!.Visuals).InSidebar);
        Assert.Equal(UIDocument.Root, xml.Children.Single(child => child.Name == "Image").Parent);
        Assert.Equal((48, 68), ((int)edited.Script.Visuals[0].Frame.X, (int)edited.Script.Visuals[0].Frame.Y));
    }

    [Fact]
    public void ASidebarWithPanesLeavesNewControlsToThePaneOnShow()
    {
        var withPanes = UIDocument.Read("""
            <UIScript>
              <Window width="300" height="200"/>
              <Sidebar name="side" width="100"/>
              <Pane name="general" title="General"/>
            </UIScript>
            """);

        var (edited, _) = DesignEdits.Add(withPanes, "Button", 1, "general");

        Assert.Equal("general", edited.Script!.Controls.Single().Pane);
        Assert.False(edited.Script.Sidebar!.HoldsContent);
    }

    [Fact]
    public void AControlMovesIntoTheSidebarBeforeItAndOutAgain()
    {
        var plain = UIDocument.Read("""
            <UIScript>
              <Window width="300" height="200"/>
              <Sidebar name="player" width="160"/>
              <Button name="stop" x="10" y="10" width="80" height="24" text="Stop"/>
            </UIScript>
            """);

        Assert.Equal(1, DesignEdits.MoveIntoTarget(plain, 2));
        var (moved, element) = DesignEdits.MoveInto(plain, 2);
        Assert.True(moved.Script!.Control("stop")!.InSidebar);
        Assert.True(moved.Script.Sidebar!.HoldsContent);

        var (back, _) = DesignEdits.MoveOut(moved, element);
        Assert.False(back.Script!.Control("stop")!.InSidebar);
        Assert.False(back.Script.Sidebar!.HoldsContent);
    }

    [Fact]
    public void ANewGridHasCellsToBeReadWith()
    {
        var (edited, element) = DesignEdits.Add(Document, "Grid");

        var grid = edited.Script!.Control(edited.Xml!.Children[element]["name"]!)!;
        Assert.Equal(UIControlType.Grid, grid.Type);
        Assert.Equal((48f, 48f), (grid.Grid!.ItemWidth, grid.Grid.ItemHeight));
    }

    [Fact]
    public void AnItemJoinsTheSelectedGridOrTheLastOne()
    {
        var two = UIDocument.Read("""
            <UIScript>
              <Window width="300" height="200"/>
              <Grid name="first" x="0" y="0" width="100" height="100" itemWidth="20" itemHeight="20">
                <Item name="a" title="A"/>
              </Grid>
              <Grid name="second" x="0" y="100" width="100" height="100" itemWidth="20" itemHeight="20"/>
            </UIScript>
            """);

        var (intoLast, _) = DesignEdits.Add(two, "Item", null);
        Assert.Equal(["item"], intoLast.Script!.Control("second")!.Grid!.Items.Select(item => item.Name));

        // With an item selected, the new one goes after the items of the grid it's in.
        var (intoFirst, element) = DesignEdits.Add(two, "Item", 2);
        Assert.Equal(["a", "item"], intoFirst.Script!.Control("first")!.Grid!.Items.Select(item => item.Name));
        Assert.Equal(1, intoFirst.Xml!.Children[element].Parent);
    }

    [Fact]
    public void AnItemNeedsAGrid()
    {
        var plain = UIDocument.Read("""<UIScript><Window width="300" height="200"/></UIScript>""");

        Assert.Contains("add a grid first", Assert.Throws<UIScriptException>(() => DesignEdits.Add(plain, "Item", null)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyAnItemMovesIntoAGridAndNoItemMovesOutOfOne()
    {
        var plain = UIDocument.Read("""
            <UIScript>
              <Window width="300" height="200"/>
              <Grid name="g" x="0" y="0" width="100" height="100" itemWidth="20" itemHeight="20">
                <Item name="a" title="A"/>
              </Grid>
              <Button name="b" x="0" y="100" width="80" height="24" text="B"/>
            </UIScript>
            """);

        Assert.Null(DesignEdits.MoveIntoTarget(plain, 3));
        Assert.False(DesignEdits.CanMoveOut(plain, 2));
        Assert.False(DesignEdits.CanMoveOut(plain, 3));
    }

    [Fact]
    public void ADuplicateOfAControlInTheSidebarStaysInIt()
    {
        var (edited, element) = DesignEdits.Duplicate(UIDocument.Read(Player), 2);

        Assert.Equal(1, edited.Xml!.Children[element].Parent);
        Assert.Equal(2, edited.Script!.Controls.Count(control => control.InSidebar));
    }

    [Theory]
    [InlineData(false, Handle.Right)]
    [InlineData(true, Handle.Left)]
    public void ASidebarResizesFromTheEdgeThatMeetsTheContent(bool onRight, Handle handle) =>
        Assert.Equal([handle], FrameEdit.HandlesFor("Sidebar", onRight));

    private const string NestedPanes = """
        <UIScript>
          <Window width="300" height="200"/>
          <Sidebar name="side" width="100"/>
          <Pane name="general" title="General">
            <Button name="a" x="0" y="0" width="10" height="10"/>
            <Button name="b" x="20" y="0" width="10" height="10"/>
          </Pane>
          <Pane name="advanced" title="Advanced">
            <Button name="c" x="0" y="0" width="10" height="10"/>
          </Pane>
          <Text name="always" x="0" y="50" width="10" height="10"/>
        </UIScript>
        """;

    [Fact]
    public void AControlGoesInsideAPaneThatHoldsItsControls()
    {
        var (edited, element) = DesignEdits.Add(UIDocument.Read(NestedPanes), "Button", null, "general");

        Assert.Equal(5, element);
        Assert.Equal(2, edited.Xml!.Children[element].Parent);
        Assert.Equal(["a", "b", "button"], edited.Script!.Controls.Where(control => control.Pane == "general").Select(control => control.Name));
    }

    [Fact]
    public void AControlJoinsAPaneWhoseControlsNameItTheSameWay()
    {
        var flat = UIDocument.Read("""
            <UIScript>
              <Window width="300" height="200"/>
              <Sidebar name="side" width="100"/>
              <Pane name="general" title="General"/>
              <Button name="a" x="0" y="0" width="10" height="10" pane="general"/>
            </UIScript>
            """);

        var (edited, element) = DesignEdits.Add(flat, "Button", null, "general");

        Assert.Equal("general", edited.Xml!.Children[element]["pane"]);
        Assert.Equal(-1, edited.Xml.Children[element].Parent);
    }

    [Fact]
    public void ForwardAndBackwardStayAmongAPanesControls()
    {
        var document = UIDocument.Read(NestedPanes);

        var (forward, a) = DesignEdits.BringForward(document, 3);
        Assert.Equal(4, a);
        Assert.Equal(["b", "a"], forward.Script!.Controls.Where(control => control.Pane == "general").Select(control => control.Name));

        // At the end of its pane, it stays, rather than passing a control in another pane.
        var (same, b) = DesignEdits.BringForward(forward, 4);
        Assert.Same(forward, same);
        Assert.Equal(4, b);
    }

    [Fact]
    public void ANewPaneGoesAfterTheLastPaneAndWhatItHolds()
    {
        var (edited, pane) = DesignEdits.Add(UIDocument.Read(NestedPanes), "Pane", null);

        Assert.Equal(7, pane);
        Assert.Equal(["general", "advanced", "pane"], edited.Script!.Panes.Select(each => each.Name));
        Assert.Equal(-1, edited.Xml!.Children[pane].Parent);
    }
}
