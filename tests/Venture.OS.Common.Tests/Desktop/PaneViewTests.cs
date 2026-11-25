using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// A sidebar of panes in a view: its rows are the panes' titles, and only the selected pane's controls show, take
/// clicks, and keep the keyboard. Rows start 7 pixels below the content's top and are 26 tall.
/// </summary>
public sealed class PaneViewTests
{
    private const int Red = unchecked((int)0xFFFF0000);

    private const string Document = """
        <UIScript version="1" requires="sidebar panes">
          <Window width="300" height="200"/>
          <Sidebar name="panes" width="100"/>
          <Pane name="general" title="General"/>
          <Pane name="advanced" title="Advanced"/>
          <Text name="heading" x="10" y="10" width="200" height="20" text="Always here"/>
          <TextField name="nickname" x="10" y="40" width="200" height="26" pane="general"/>
          <Button name="reset" x="10" y="40" width="200" height="26" text="Reset" pane="advanced"/>
        </UIScript>
        """;

    private readonly UIView view = new(UIScript.Parse(Document));

    [Fact]
    public void TheRowsAreThePanesAndTheFirstStartsSelected()
    {
        Assert.Equal(["General", "Advanced"], view.List("panes")!.Items);
        Assert.Equal(0, view.List("panes")!.SelectedIndex);
        Assert.Equal("general", view.SelectedPane!.Name);
        Assert.True(view.HasPanes);
    }

    [Fact]
    public void OnlyTheSelectedPanesControlsShowWithThoseInNoPane()
    {
        Assert.True(view.IsShowing("heading"));
        Assert.True(view.IsShowing("nickname"));
        Assert.False(view.IsShowing("reset"));

        view.List("panes")!.SelectedIndex = 1;

        Assert.True(view.IsShowing("heading"));
        Assert.False(view.IsShowing("nickname"));
        Assert.True(view.IsShowing("reset"));
    }

    [Fact]
    public void ClickingARowSwitchesPanesAndSaysSo()
    {
        var events = Click(-50, 7 + 26 + 13);

        Assert.Contains(events, e => e is { Kind: UIEventKind.Selected, Control: "panes", Index: 1 });
        Assert.Equal("advanced", view.SelectedPane!.Name);

        // The button now sits where the field was, and takes the click.
        Assert.Contains(Click(50, 50), e => e is { Kind: UIEventKind.Pressed, Control: "reset" });
    }

    [Fact]
    public void AControlInAnotherPaneTakesNoClick()
    {
        Assert.DoesNotContain(Click(50, 50), e => e.Control == "reset");
    }

    [Fact]
    public void HidingIsTheOwnersAndPanesDoNotUndoIt()
    {
        view.SetHidden(true, "reset");
        view.List("panes")!.SelectedIndex = 1;

        Assert.True(view.IsHidden("reset"));
        Assert.False(view.IsShowing("reset"));
        Assert.False(view.IsHidden("nickname"));
    }

    [Fact]
    public void AFieldLosesTheKeyboardWhenItsPaneIsSwitchedAway()
    {
        view.Focus("nickname");
        Assert.Equal("nickname", view.FocusedControl);

        view.List("panes")!.SelectedIndex = 1;
        view.Update(new UIViewInput(0, 0, false, false, false, 0, []), 0, 0);

        Assert.Null(view.FocusedControl);
    }

    [Fact]
    public void AVisualInAPaneShowsAndDrawsOnlyWithItsPane()
    {
        var pictured = new UIView(UIScript.Parse("""
            <UIScript version="1">
              <Window width="300" height="200"/>
              <Sidebar name="panes" width="100"/>
              <Image name="logo" path="logo.png"/>
              <Pane name="about" title="About">
                <Visual name="icon" image="logo" x="10" y="10" width="2" height="2"/>
              </Pane>
              <Pane name="credits" title="Credits"/>
            </UIScript>
            """));
        pictured.SetImage("logo", new DesktopImage(2, 2, [Red, Red, Red, Red]));

        var onAbout = new RecordingPainter(300, 200);
        pictured.Draw(onAbout, 0, 0);
        pictured.List("panes")!.SelectedIndex = 1;
        var onCredits = new RecordingPainter(300, 200);
        pictured.Draw(onCredits, 0, 0);

        Assert.Single(onAbout.Images, drawn => (drawn.X, drawn.Y) == (10, 10));
        Assert.DoesNotContain(onCredits.Images, drawn => (drawn.X, drawn.Y) == (10, 10));
        Assert.False(pictured.IsShowing("icon"));
    }

    [Fact]
    public void AHiddenVisualStaysHiddenWhenItsPaneShows()
    {
        var pictured = new UIView(UIScript.Parse("""
            <UIScript version="1">
              <Window width="300" height="200"/>
              <Sidebar name="panes" width="100"/>
              <Image name="logo" path="logo.png"/>
              <Pane name="about" title="About">
                <Visual name="icon" image="logo" x="10" y="10" width="2" height="2"/>
              </Pane>
            </UIScript>
            """));

        pictured.SetHidden(true, "icon");

        Assert.False(pictured.IsShowing("icon"));
    }

    [Fact]
    public void WithNoRowSelectedOnlyControlsInNoPaneShow()
    {
        view.List("panes")!.SelectedIndex = -1;

        Assert.Null(view.SelectedPane);
        Assert.True(view.IsShowing("heading"));
        Assert.False(view.IsShowing("nickname"));
        Assert.False(view.IsShowing("reset"));
    }

    private List<UIEvent> Click(int x, int y)
    {
        List<UIEvent> events = [.. view.Update(new UIViewInput(x, y, true, true, false, 0, []), 0, 0)];
        events.AddRange(view.Update(new UIViewInput(x, y, false, false, true, 0, []), 0, 0));
        return events;
    }
}
