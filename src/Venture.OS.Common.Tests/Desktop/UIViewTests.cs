using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

public sealed class UIViewTests
{
    private const string Document = """
        <UIScript>
          <Window width="300" height="200"/>
          <String name="greeting" table="UIText:1" index="2"/>
          <Text name="label" x="10" y="10" width="100" height="20" text="Hello"/>
          <Button name="ok" x="200" y="160" width="80" height="24" text="OK"/>
          <TextField name="field" x="10" y="40" width="150" height="22" placeholder="Type here" maximumLength="5"/>
          <TextField name="secret" x="10" y="70" width="150" height="22" secure="true"/>
          <TextField name="fixed" x="10" y="100" width="150" height="22" readOnly="true" text="locked"/>
          <List name="picks" x="170" y="10" width="120" height="60" selectable="true"/>
          <List name="log" x="170" y="80" width="100" height="40" text="first"/>
          <Slider name="volume" x="10" y="130" width="100" height="12" minValue="0" maxValue="10"/>
          <Slider name="logScroll" x="275" y="80" width="10" height="40" scrolls="log"/>
          <Button name="logUp" x="275" y="60" width="10" height="10" text="^" scrolls="log" direction="up"/>
          <ProgressBar name="progress" x="10" y="150" width="100" height="10"/>
          <Text name="anchored" x="0" y="0" width="40" height="10" edgeOffsetRight="5" edgeOffsetBottom="5" string="greeting"/>
          <Region name="area" x="120" y="130" width="40" height="40"/>
        </UIScript>
        """;

    private readonly UIView view = new(UIScript.Parse(Document));

    [Fact]
    public void PlacesControlsWithEdgeOffsetsAsHudiniDoes()
    {
        Assert.Equal(new UIRect(200, 160, 80, 24), view.Bounds("ok"));
        Assert.Equal(new UIRect(255, 185, 40, 10), view.Bounds("anchored"));
        Assert.Null(view.Bounds("nothing"));
    }

    [Fact]
    public void ShowsAMissingStringsTableAndIndex()
    {
        Assert.Equal("UIText:1:2", view.Text("anchored"));
        Assert.Contains("greeting", view.MissingStrings);
        Assert.Equal("Bonjour", new UIView(UIScript.Parse(Document), _ => "Bonjour").Text("anchored"));
    }

    [Fact]
    public void ReportsAButtonPressedAndReleasedOnIt()
    {
        Assert.Empty(Click(240, 170, down: true));
        Assert.Equal([new UIEvent(UIEventKind.Pressed, "ok")], Click(241, 171, down: false));
    }

    [Fact]
    public void DoesntReportAButtonReleasedOffIt()
    {
        Click(240, 170, down: true);

        Assert.Empty(Click(10, 10, down: false));
    }

    [Fact]
    public void IgnoresDisabledAndHiddenControls()
    {
        view.SetDisabled(true, "ok");
        Click(240, 170, down: true);
        Assert.Empty(Click(240, 170, down: false));

        view.SetDisabled(false, "ok");
        view.SetHidden(true, "ok");
        Click(240, 170, down: true);
        Assert.Empty(Click(240, 170, down: false));
        Assert.True(view.IsHidden("ok"));
    }

    [Fact]
    public void TypesIntoAFocusedFieldUpToItsMaximumAndSubmitsOnEnter()
    {
        Click(20, 50, down: true);
        Assert.Equal("field", view.FocusedControl);

        Type("hello world");
        Assert.Equal("hello", view.Text("field"));

        var events = Type(new KeyPress(DesktopKey.Backspace), new KeyPress(DesktopKey.Home), KeyPress.Typed('X'), new KeyPress(DesktopKey.End), new KeyPress(DesktopKey.Enter));

        Assert.Equal("Xhell", view.Text("field"));
        Assert.Equal([new UIEvent(UIEventKind.Submitted, "field", Text: "Xhell")], events);
    }

    [Fact]
    public void EditsAroundTheCaret()
    {
        view.Focus("secret");
        Type("abcd");
        Type(new KeyPress(DesktopKey.Left), new KeyPress(DesktopKey.Left), new KeyPress(DesktopKey.Delete), new KeyPress(DesktopKey.Right), KeyPress.Typed('Z'));

        Assert.Equal("abdZ", view.Text("secret"));
    }

    [Fact]
    public void IgnoresControlCharactersAndShortcuts()
    {
        view.Focus("secret");
        Type(KeyPress.Typed('\t'), new KeyPress(DesktopKey.Character, 'c', Control: true), KeyPress.Typed('a'));

        Assert.Equal("a", view.Text("secret"));
    }

    [Fact]
    public void ReadOnlyFieldsDontTakeFocus()
    {
        Click(20, 110, down: true);
        view.Focus("fixed");

        Assert.Null(view.FocusedControl);
    }

    [Fact]
    public void AClickElsewhereTakesFocusAway()
    {
        view.Focus("field");
        Click(5, 195, down: true);

        Assert.Null(view.FocusedControl);
    }

    [Fact]
    public void SelectsAListItemAndReportsOnlyAChange()
    {
        var picks = view.List("picks")!;
        picks.Items.AddRange(["Apples", "Pears", "Plums"]);

        var first = Click(180, 12 + 19, down: true);
        Click(180, 31, down: false);
        var again = Click(180, 31, down: true, seconds: 2);

        Assert.Equal([new UIEvent(UIEventKind.Selected, "picks", Index: 1)], first);
        Assert.Empty(again);
        Assert.Equal(1, picks.SelectedIndex);
    }

    [Fact]
    public void ADoubleClickOnAnItemSubmitsIt()
    {
        view.List("picks")!.Items.AddRange(["Apples", "Pears", "Plums"]);

        var first = Click(180, 31, down: true);
        Click(180, 31, down: false, seconds: 0.1);
        var second = Click(180, 31, down: true, seconds: 0.3);
        Click(180, 31, down: false, seconds: 0.35);
        var third = Click(180, 31, down: true, seconds: 0.45);

        Assert.Equal([new UIEvent(UIEventKind.Selected, "picks", Index: 1)], first);
        Assert.Equal([new UIEvent(UIEventKind.Submitted, "picks", "Pears", Index: 1)], second);
        Assert.Empty(third);
    }

    [Fact]
    public void SlowClicksOrClicksOnDifferentItemsDontSubmit()
    {
        view.List("picks")!.Items.AddRange(["Apples", "Pears", "Plums"]);

        Click(180, 31, down: true);
        Click(180, 31, down: false, seconds: 0.1);
        var slow = Click(180, 31, down: true, seconds: 0.7);
        Click(180, 31, down: false, seconds: 0.8);
        var other = Click(180, 51, down: true, seconds: 0.9);

        Assert.Empty(slow);
        Assert.Equal([new UIEvent(UIEventKind.Selected, "picks", Index: 2)], other);
    }

    [Fact]
    public void ALogIgnoresDoubleClicks()
    {
        Click(180, 85, down: true);
        Click(180, 85, down: false, seconds: 0.1);

        Assert.Empty(Click(180, 85, down: true, seconds: 0.2));
    }

    [Fact]
    public void ALogFollowsItsNewestItemsAndItsTextIsTheFirstItem()
    {
        var log = view.List("log")!;
        Assert.Equal(["first"], log.Items);

        log.Items.AddRange(["two", "three", "four", "five"]);
        Click(0, 0, down: false);

        // Rows are 20 pixels at the default size, so the 40-pixel log shows one: the newest.
        Assert.Equal(log.Items.Count - 1, log.FirstVisible);
    }

    [Fact]
    public void ScrollButtonsAndTheWheelMoveTheList()
    {
        var log = view.List("log")!;
        log.Items.AddRange(["two", "three", "four", "five"]);
        Click(0, 0, down: false);
        var bottom = log.FirstVisible;

        Click(278, 63, down: true);
        var events = Click(278, 63, down: false);
        Assert.Empty(events);
        Assert.Equal(bottom - 1, log.FirstVisible);

        view.Update(new UIViewInput(200, 90, false, false, false, -10, []), 0, 0);
        Assert.Equal(0, log.FirstVisible);
    }

    [Fact]
    public void DraggingASliderReportsItsValue()
    {
        Click(10, 135, down: true);
        var events = view.Update(new UIViewInput(60, 135, true, false, false, 0, []), 0, 0);

        Assert.Equal(UIEventKind.Changed, Assert.Single(events).Kind);
        Assert.Equal(5, view.Value("volume")!.Value, 0.01);
    }

    [Fact]
    public void ASliderBoundToAListScrollsItWithoutReporting()
    {
        var log = view.List("log")!;
        log.Items.AddRange(["two", "three", "four", "five"]);
        Click(0, 0, down: false);

        Click(280, 80, down: true);
        var events = view.Update(new UIViewInput(280, 80, true, false, false, 0, []), 0, 0);

        Assert.Empty(events);
        Assert.Equal(0, log.FirstVisible);
    }

    [Fact]
    public void SetsTextAndValuesByName()
    {
        view.SetText("label", "Changed");
        view.SetValue("progress", 3);
        view.SetValue("volume", 99);

        Assert.Equal("Changed", view.Text("label"));
        Assert.Equal(1, view.Value("progress"));
        Assert.Equal(10, view.Value("volume"));
        Assert.Null(view.Value("label"));
    }

    [Fact]
    public void DrawsControlsAndHandsRegionsToTheOwner()
    {
        List<(string, UIRect)> regions = [];
        UIView drawn = new(UIScript.Parse(Document), drawRegion: (name, bounds) => regions.Add((name, bounds)));
        RecordingPainter painter = new(400, 300);

        drawn.Draw(painter, 50, 40);

        Assert.Contains(painter.Texts, text => text.Text == "Hello" && text.X == 60);
        Assert.Contains(painter.Texts, text => text.Text == "Type here" && text.Color == DesktopTheme.SecondaryText);
        Assert.Contains(painter.Texts, text => text.Text == "OK");
        Assert.Equal([("area", new UIRect(170, 170, 40, 40))], regions);
    }

    [Fact]
    public void LeavesAGapBetweenTheTextAndTheCaret()
    {
        view.Focus("secret");
        Type("abc");
        RecordingPainter painter = new(400, 300);

        view.Draw(painter, 0, 0);

        // The stars start at 10 + 4 and measure 24 wide, so the last one ends at 38.
        var caret = DesktopBackground.Pack(DesktopTheme.Text.Red, DesktopTheme.Text.Green, DesktopTheme.Text.Blue);
        Assert.NotEqual(caret, painter.At(38, 81));
        Assert.Equal(caret, painter.At(39, 81));
    }

    [Fact]
    public void DrawsASecureFieldAsStarsAndCutsLongTextShort()
    {
        view.Focus("secret");
        Type("pass");
        view.SetText("label", new string('W', 40));
        RecordingPainter painter = new(400, 300);

        view.Draw(painter, 0, 0);

        Assert.Contains(painter.Texts, text => text.Text == "****");
        Assert.Contains(painter.Texts, text => text.Text.EndsWith('…') && text.Text.Length * 8 <= 100 + 8);
    }

    [Fact]
    public void OutlinesImagesItCantDrawYet()
    {
        UIView withImage = new(UIScript.Parse("""
            <UIScript><Window width="100" height="100"/><Image name="send" path="send.bmp"/>
            <Button name="send" x="10" y="10" image="send"/></UIScript>
            """));
        RecordingPainter painter = new(100, 100);

        withImage.Draw(painter, 0, 0);

        Assert.Equal(["send"], withImage.MissingImages);
        Assert.Equal(new UIRect(10, 10, 24, 24), withImage.Bounds("send"));
        Assert.Equal(DesktopBackground.Pack(220, 30, 30), painter.At(10, 10));
    }

    private IReadOnlyList<UIEvent> Click(int x, int y, bool down, double seconds = 0) =>
        view.Update(new UIViewInput(x, y, down, down, !down, 0, [], TimeSpan.FromSeconds(seconds)), 0, 0);

    private IReadOnlyList<UIEvent> Type(string text) =>
        Type([.. text.Select(KeyPress.Typed)]);

    private IReadOnlyList<UIEvent> Type(params KeyPress[] keys) =>
        view.Update(new UIViewInput(0, 0, false, false, false, 0, keys), 0, 0);
}
