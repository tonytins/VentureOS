using Venture.OS.Interface;

namespace Venture.OS.Tests.Interface;

/// <summary><c>&lt;Grid&gt;</c> and its <c>&lt;Item&gt;</c>s, as the reader takes them in and refuses them.</summary>
public sealed class UIScriptGridTests
{
    private const string Declarations = """
        <Window width="300" height="200"/>
        <Image name="sky" path="sky.png"/>
        <String name="blueName" table="1" index="1"/>
        <String name="tip" table="1" index="2"/>
        """;

    [Fact]
    public void ReadsAGridAndTheItemsInIt()
    {
        var script = UIScript.Parse($"""
            <UIScript>
              {Declarations}
              <Grid name="accents" x="10" y="10" width="200" height="60" itemWidth="28" itemHeight="24" spacing="12" titles="selected">
                <Item name="blue" color="#3584E4" string="blueName" tooltip="tip"/>
                <Item name="sky" image="sky" title="Sky"/>
                <Item title="Plain"/>
              </Grid>
              <Button name="after" x="10" y="100" width="60" height="20" text="After"/>
            </UIScript>
            """);

        var grid = script.Control("accents")!;
        Assert.Equal(UIControlType.Grid, grid.Type);
        Assert.Equal(new UIGridLayout(28, 24, 12, UIGridTitles.Selected, grid.Grid!.Items), grid.Grid);
        Assert.Equal(
            [
                new UIGridItem("blue", UIText.Reference("blueName"), null, new UIColor(0x35, 0x84, 0xE4), "tip", 7),
                new UIGridItem("sky", UIText.Literal("Sky"), "sky", null, null, 8),
                new UIGridItem(null, UIText.Literal("Plain"), null, null, null, 9),
            ],
            grid.Grid.Items);

        // Items join the grid, not the controls, and what follows the grid is read as before.
        Assert.Equal(["accents", "after"], script.Controls.Select(control => control.Name));
        Assert.Empty(script.IgnoredAttributes);
    }

    [Fact]
    public void AGridWithoutSpacingOrTitlesSpacesItsCellsEightApartWithTitlesBelow()
    {
        var script = UIScript.Parse("""<UIScript><Window width="300" height="200"/><Grid name="g" x="0" y="0" width="100" height="100" itemWidth="20" itemHeight="20"/></UIScript>""");

        Assert.Equal(new UIGridLayout(20, 20, 8, UIGridTitles.Below, []), script.Control("g")!.Grid);
    }

    [Fact]
    public void AGridMayHoldNothingAndSitInAPane()
    {
        var script = UIScript.Parse("""
            <UIScript>
              <Window width="300" height="200"/>
              <Sidebar name="side" width="80"/>
              <Pane name="looks" title="Looks">
                <Grid name="g" x="0" y="0" width="100" height="100" itemWidth="20" itemHeight="20">
                  <Item title="One"/>
                </Grid>
              </Pane>
            </UIScript>
            """);

        Assert.Equal("looks", script.Control("g")!.Pane);
        Assert.Single(script.Control("g")!.Grid!.Items);
    }

    [Theory]
    [InlineData("""<Item title="Loose"/>""", "An <Item> belongs inside a <Grid>")]
    [InlineData("""<Grid name="g" x="0" y="0" width="100" height="100" itemWidth="20" itemHeight="20"><Button name="b" x="0" y="0" width="1" height="1"/></Grid>""", "<Button> can't be inside <Grid>; a grid holds items")]
    [InlineData("""<List name="l" x="0" y="0" width="100" height="100"><Item title="x"/></List>""", "only a <Menu>, a <Pane>, a <Sidebar>, or a <Grid>")]
    [InlineData("""<Grid name="g" x="0" y="0" width="100" height="100" itemWidth="20" itemHeight="20"><Item name="i" image="sky" color="#000000"/></Grid>""", "\"i\" has both an image and a color")]
    [InlineData("""<Grid name="g" x="0" y="0" width="100" height="100" itemWidth="20" itemHeight="20"><Item tooltip="tip"/></Grid>""", "needs a title, an image, or a color")]
    [InlineData("""<Grid name="g" x="0" y="0" width="100" height="100" itemWidth="20" itemHeight="20"><Item image="nope"/></Grid>""", "image=\"nope\", which isn't declared")]
    [InlineData("""<Grid name="g" x="0" y="0" width="100" height="100" itemWidth="20" itemHeight="20"><Item string="nope"/></Grid>""", "string=\"nope\", which isn't declared")]
    [InlineData("""<Grid name="g" x="0" y="0" width="100" height="100" itemWidth="20" itemHeight="20"><Item title="t" tooltip="nope"/></Grid>""", "tooltip=\"nope\", which isn't declared")]
    [InlineData("""<Grid name="g" x="0" y="0" width="100" height="100" itemWidth="20" itemHeight="20"><Item title="t" color="blue"/></Grid>""", "color")]
    [InlineData("""<Grid name="g" x="0" y="0" width="100" height="100" itemWidth="20" itemHeight="20"><Item name="g" title="t"/></Grid>""", "The name \"g\" is used more than once")]
    [InlineData("""<Grid name="g" x="0" y="0" width="100" height="100" itemWidth="20" itemHeight="20" titles="above"/>""", "below, selected, or none")]
    [InlineData("""<Grid name="g" x="0" y="0" width="100" height="100" itemWidth="0" itemHeight="20"/>""", "itemWidth and itemHeight above 0")]
    [InlineData("""<Grid name="g" x="0" y="0" width="100" height="100" itemWidth="20" itemHeight="20" spacing="-1"/>""", "spacing of 0 or more")]
    [InlineData("""<Grid name="g" x="0" y="0" width="100" height="100" itemHeight="20"/>""", "itemWidth")]
    public void RefusesWhatDoesntFit(string elements, string message)
    {
        var problem = Assert.Throws<UIScriptException>(() => UIScript.Parse($"<UIScript>{Declarations}{elements}</UIScript>"));

        Assert.Contains(message, problem.Message);
    }
}
