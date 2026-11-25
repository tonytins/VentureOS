using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// Using a window's controls by name, as airo's tests and vdb do, over a sample window of the tests' own. Each action
/// is checked for what it changes and what it hands the app, and each refusal for its reason.
/// </summary>
public sealed class WindowControlsTests
{
    private const string Document = """
        <UIScript requires="menus">
          <Window width="400" height="300" subtitle="3 items"/>
          <TitleField name="address"/>
          <ToolbarItem name="share" title="Share"/>
          <Menu name="file" title="File" hotkey="F"/>
          <MenuItem menu="file" name="newNote" title="New Note"/>
          <Text name="label" x="10" y="10" width="100" height="20" text="Hello"/>
          <Button name="ok" x="10" y="40" width="60" height="20" text="OK"/>
          <Button name="gone" x="80" y="40" width="60" height="20" text="Gone"/>
          <Button name="dim" x="150" y="40" width="60" height="20" text="Dim"/>
          <TextField name="name" x="10" y="70" width="200" height="24"/>
          <List name="notes" x="10" y="100" width="200" height="80"/>
          <ProgressBar name="level" x="10" y="190" width="200" height="8"/>
          <Slider name="taxes" x="220" y="190" width="150" height="20" maxValue="20" step="1"/>
          <Grid name="colors" x="10" y="200" width="200" height="60" itemWidth="20" itemHeight="20" titles="none">
            <Item name="red" title="Red" color="#FF0000"/>
            <Item name="blue" title="Blue" color="#0000FF"/>
          </Grid>
        </UIScript>
        """;

    private readonly DesktopWindow window;
    private readonly List<UIEvent> events = [];

    public WindowControlsTests()
    {
        window = new DesktopWindow("Sample", new UIView(UIScript.Parse(Document)));
        window.EventHandler = (_, uiEvent) => events.Add(uiEvent);
        window.View.List("notes")!.Items.AddRange(["Milk", "Eggs", "Bread"]);
        window.View.SetHidden(true, "gone");
        window.View.SetDisabled(true, "dim");
        window.View.SetValue("level", 0.5f);
    }

    [Theory]
    [InlineData("ok")]
    [InlineData("share")]
    [InlineData("newNote")]
    public void PressingAButtonToolbarItemOrMenuItemHandsTheAppItsPress(string name)
    {
        Assert.True(WindowControls.TryPress(window, name, out var problem), problem);
        Assert.Equal([new UIEvent(UIEventKind.Pressed, name)], events);
    }

    [Theory]
    [InlineData("missing", "there's no \"missing\" in Sample")]
    [InlineData("gone", "\"gone\" isn't showing, so a person couldn't use it")]
    [InlineData("dim", "\"dim\" is dimmed, so a person couldn't use it")]
    public void WhatAPersonCouldNotUseIsNotUsedAndSaysWhy(string name, string reason)
    {
        Assert.False(WindowControls.TryPress(window, name, out var problem));
        Assert.Equal(reason, problem);
        Assert.Empty(events);
    }

    [Fact]
    public void ADimmedMenuItemIsNotPressed()
    {
        window.View.SetDisabled(true, "newNote");

        Assert.False(WindowControls.TryPress(window, "newNote", out var problem));
        Assert.Equal("\"newNote\" is dimmed, so a person couldn't use it", problem);
    }

    [Fact]
    public void SubmittingPutsTheTextInAndPressesEnter()
    {
        Assert.True(WindowControls.TrySubmit(window, "name", "Ada", out var problem), problem);

        Assert.Equal("Ada", window.View.Text("name"));
        Assert.Equal([new UIEvent(UIEventKind.Submitted, "name", "Ada")], events);
    }

    [Fact]
    public void TypingPutsTheTextInWithoutEnter()
    {
        Assert.True(WindowControls.TryType(window, "name", "Ada", out var problem), problem);

        Assert.Equal("Ada", window.View.Text("name"));
        Assert.Equal([new UIEvent(UIEventKind.Changed, "name", "Ada")], events);
    }

    [Fact]
    public void SlidingMovesItAsADragThatEndsThereWould()
    {
        Assert.True(WindowControls.TrySlide(window, "taxes", 7.4f, out var problem), problem);

        Assert.Equal(7, window.View.Value("taxes"));
        Assert.Equal(
            [new UIEvent(UIEventKind.Changed, "taxes", Value: 7), new UIEvent(UIEventKind.Committed, "taxes", Value: 7)],
            events);
    }

    [Fact]
    public void SlidingToWhereItIsReportsNothing()
    {
        Assert.True(WindowControls.TrySlide(window, "taxes", 0.2f, out var problem), problem);
        Assert.Empty(events);
    }

    [Theory]
    [InlineData("ok", 1f, "\"ok\" isn't a slider")]
    [InlineData("taxes", float.NaN, "a slider's value is a number, not NaN")]
    [InlineData("missing", 1f, "there's no \"missing\" in Sample")]
    public void SlidingWhatIsntASliderOrToWhatIsntANumberIsRefused(string name, float value, string reason)
    {
        Assert.False(WindowControls.TrySlide(window, name, value, out var problem));
        Assert.Equal(reason, problem);
        Assert.Empty(events);
    }

    [Fact]
    public void ChoosingByNumberCountsFromOne()
    {
        Assert.True(WindowControls.TryChoose(window, "notes", 2, out var problem), problem);

        Assert.Equal(1, window.View.List("notes")!.SelectedIndex);
        Assert.Equal([new UIEvent(UIEventKind.Selected, "notes", Index: 1)], events);
    }

    [Fact]
    public void ChoosingByTextFindsTheRowAPersonReads()
    {
        Assert.True(WindowControls.TryChoose(window, "notes", "Bread", out var problem), problem);
        Assert.Equal(2, window.View.List("notes")!.SelectedIndex);
    }

    [Fact]
    public void ChoosingAGridItemByNameReportsTheItem()
    {
        Assert.True(WindowControls.TryChoose(window, "colors", "blue", out var problem), problem);
        Assert.Equal([new UIEvent(UIEventKind.Selected, "colors", Index: 1, Item: "blue")], events);
    }

    [Theory]
    [InlineData(0, "\"notes\" has 3 rows, so there's no row 0")]
    [InlineData(4, "\"notes\" has 3 rows, so there's no row 4")]
    public void ARowPastTheListIsRefused(int row, string reason)
    {
        Assert.False(WindowControls.TryChoose(window, "notes", row, out var problem));
        Assert.Equal(reason, problem);
        Assert.Empty(events);
    }

    [Fact]
    public void ARowThatIsNotThereNamesTheRowsThatAre()
    {
        Assert.False(WindowControls.TryChoose(window, "notes", "Jam", out var problem));
        Assert.Equal("\"notes\" has no row \"Jam\"; its rows are Milk, Eggs, Bread", problem);
    }

    [Fact]
    public void ChoosingFromWhatIsNotAListIsRefused()
    {
        Assert.False(WindowControls.TryChoose(window, "ok", 1, out var problem));
        Assert.Equal("\"ok\" isn't a list or a grid", problem);
    }

    [Fact]
    public void ChoicesGiveTheRowsAndTheChosenOneFromOne()
    {
        window.View.List("notes")!.SelectedIndex = 0;

        var choices = WindowControls.Choices(window, "notes")!;

        Assert.Equal(["Milk", "Eggs", "Bread"], choices.Rows);
        Assert.Equal(1, choices.Chosen);
        Assert.Null(WindowControls.Choices(window, "colors")!.Chosen);
        Assert.Null(WindowControls.Choices(window, "ok"));
    }

    [Fact]
    public void ASnapshotListsEverythingWithWhatTheReadsWouldSay()
    {
        window.View.Focus("name");
        window.View.List("notes")!.SelectedIndex = 2;

        var snapshot = WindowControls.Snapshot(window);

        Assert.Equal("Sample", snapshot.Title);
        Assert.Equal("3 items", snapshot.Subtitle);
        Assert.Equal("name", snapshot.Focused);
        Assert.Equal(
            ["label", "ok", "gone", "dim", "name", "notes", "level", "taxes", "colors", "address", "share", "newNote"],
            snapshot.Controls.Select(control => control.Name));

        var byName = snapshot.Controls.ToDictionary(control => control.Name);
        Assert.Equal(new ControlSnapshot("label", "text", "Hello", null, true, false, false, false, null, null), byName["label"]);
        Assert.False(byName["gone"].Showing);
        Assert.True(byName["dim"].Dimmed);
        Assert.True(byName["name"].Focused);
        Assert.Equal("textField", byName["name"].Kind);
        Assert.Equal(["Milk", "Eggs", "Bread"], byName["notes"].Rows);
        Assert.Equal(3, byName["notes"].Chosen);
        Assert.Equal(0.5f, byName["level"].Value);
        Assert.Equal(("slider", 0f), (byName["taxes"].Kind, byName["taxes"].Value));
        Assert.Equal(["Red", "Blue"], byName["colors"].Rows);
        Assert.Equal(("titleField", "toolbarItem", "menuItem"), (byName["address"].Kind, byName["share"].Kind, byName["newNote"].Kind));
        Assert.Equal("New Note", byName["newNote"].Text);
    }

    [Fact]
    public void ASnapshotOfAWindowWithABlankTitleBarHasNoTitle()
    {
        window.ShowsTitle = false;
        Assert.Null(WindowControls.Snapshot(window).Title);
    }

    [Theory]
    [InlineData("""<UIScript><StatusItem name="mixer" title="Mix" width="200" height="60"/><Slider name="volume" x="10" y="20" width="180" height="20"/></UIScript>""", "panel")]
    [InlineData("""<UIScript><StatusItem name="mixer" title="Mix"/><Menu name="m" title="Mix"><MenuItem name="mute" title="Mute"/></Menu></UIScript>""", "menu")]
    public void AStatusItemThatOpensSomethingIsNotPressed(string document, string opens)
    {
        DesktopWindow item = new("Mix", new UIView(UIScript.Parse(document))) { EventHandler = (_, uiEvent) => events.Add(uiEvent) };

        Assert.False(WindowControls.TryPress(item, "mixer", out var problem));
        Assert.Contains($"opens a {opens}", problem);
        Assert.Empty(events);
    }

    [Fact]
    public void AStatusItemThatsAButtonIsPressed()
    {
        DesktopWindow item = new("Ping", new UIView(UIScript.Parse("""<UIScript><StatusItem name="ping" title="Ping"/></UIScript>"""))) { EventHandler = (_, uiEvent) => events.Add(uiEvent) };

        Assert.True(WindowControls.TryPress(item, "ping", out _));
        Assert.Equal([new UIEvent(UIEventKind.Pressed, "ping")], events);
    }
}
