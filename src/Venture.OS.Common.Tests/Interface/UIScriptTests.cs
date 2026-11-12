using Venture.OS.Interface;

namespace Venture.OS.Tests.Interface;

public sealed class UIScriptTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Interface", "Fixtures", "Hudini");

    [Theory]
    [InlineData("chatdialog.xml")]
    [InlineData("livepanel1024.xml")]
    [InlineData("movelot.xml")]
    [InlineData("optionspanel.xml")]
    [InlineData("statspanel.xml")]
    public void ReadsEveryHudiniExample(string file)
    {
        var script = UIScript.Load(Path.Combine(Fixtures, file));

        Assert.Equal(1, script.Version);
        Assert.Empty(script.IgnoredAttributes);
        Assert.NotEmpty(script.Controls);
    }

    [Fact]
    public void ReadsTheChatDialogAsHudiniDoes()
    {
        var script = UIScript.Load(Path.Combine(Fixtures, "chatdialog.xml"));

        Assert.Equal(new UISize(400, 255), script.Window.Size);
        Assert.Equal(new UISize(350, 196), script.Window.MinimumSize);
        Assert.Equal(UIStyleColor.Literal(new UIColor(0xFF, 0xF9, 0x9D)), script.Styles["history"].Colors.Text);
        Assert.Equal(UIStyleSize.Points(14), script.Styles["entry"].FontSize);

        Assert.Equal(["chatHistory", "chatHistorySlider", "scrollUp", "scrollDown", "chatEntry"],
            script.Controls.Select(control => control.Name));

        var slider = script.Control("chatHistorySlider")!;
        Assert.Equal(UIControlType.Slider, slider.Type);
        Assert.Equal(new UIScrollBinding("chatHistory", null), slider.Scrolls);

        var up = script.Control("scrollUp")!;
        Assert.Equal(UIText.Literal("^"), up.Text);
        Assert.Equal(UIScrollDirection.Up, up.Scrolls!.Direction);
        Assert.Equal(3, up.LegacyId);

        var entry = script.Control("chatEntry")!;
        Assert.Equal(UIText.Literal("Type a message..."), entry.Placeholder);
        Assert.Equal(120, entry.MaximumLength);
        Assert.Equal(new UIFrame(52, 216, 299, 22, null), entry.Frame);
    }

    [Fact]
    public void ReadsEveryElementAndAttribute()
    {
        var script = UIScript.Parse("""
            <UIScript>
              <Window width="300.5" height="200"/>
              <Image name="send" path="ui/send.bmp" assetID="0x1234" stateCount="4"/>
              <String name="hello" table="UIText:174" index="3"/>
              <Style name="s" textColor="#11223344" selectedTextColor="#000000" highlightedTextColor="#FFFFFF"
                     disabledTextColor="#808080" backgroundColor="#010203" borderColor="#040506" fillColor="#070809"
                     fontSize="12"/>
              <Visual name="backdrop" image="send" x="0" y="0"/>
              <Text name="t" x="1" y="2" width="3" height="4" string="hello" alignment="center" tooltip="hello"/>
              <FormattedText name="f" x="0" y="0" width="1" height="1" text="hi" alignment="right" style="s"/>
              <Button name="b" x="0" y="0" image="send"/>
              <TextField name="tf" x="0" y="0" width="1" height="1" placeholderString="hello" readOnly="true" secure="true"/>
              <List name="l" x="0" y="0" width="1" height="1" selectable="true"/>
              <Slider name="sl" x="0" y="0" width="1" height="1" minValue="-1" maxValue="1.5" orientation="vertical"/>
              <ProgressBar name="p" x="0" y="0" width="1" height="1" maxValue="100"/>
              <Region name="r" x="-4" y="0" width="1" height="1" edgeOffsetRight="8" edgeOffsetBottom="2"/>
            </UIScript>
            """);

        Assert.Equal(new UISize(300.5f, 200), script.Window.Size);
        Assert.Equal(new UIImageResource("send", "ui/send.bmp", "0x1234", 4), script.Images["send"]);
        Assert.Equal(new UIStringResource("hello", "UIText:174", 3), script.Strings["hello"]);
        Assert.Equal(UIStyleColor.Literal(new UIColor(0x11, 0x22, 0x33, 0x44)), script.Styles["s"].Colors.Text);
        Assert.Equal(UIStyleColor.Literal(new UIColor(7, 8, 9)), script.Styles["s"].Colors.Fill);
        Assert.Equal("send", Assert.Single(script.Visuals).Image);

        Assert.Equal(UIText.Reference("hello"), script.Control("t")!.Text);
        Assert.Equal(UITextAlignment.Center, script.Control("t")!.Alignment);
        Assert.Equal("hello", script.Control("t")!.Tooltip);
        Assert.Null(script.Control("b")!.Frame.Width);
        Assert.Equal(UIText.Reference("hello"), script.Control("tf")!.Placeholder);
        Assert.True(script.Control("tf")!.IsReadOnly && script.Control("tf")!.IsSecure);
        Assert.True(script.Control("l")!.IsSelectable);
        Assert.Equal((-1f, 1.5f), script.Control("sl")!.ValueRange);
        Assert.Equal(UIOrientation.Vertical, script.Control("sl")!.Orientation);
        Assert.Equal((0f, 100f), script.Control("p")!.ValueRange);
        Assert.Equal(new UIEdgeOffsets(null, 8, null, 2), script.Control("r")!.Frame.EdgeOffsets);
        Assert.Equal(UIControlType.Region, script.Control("r")!.Type);
    }

    [Fact]
    public void ListsUnknownAttributesSortedPerElement()
    {
        var script = UIScript.Parse("""
            <UIScript zeta="1" alpha="2">
              <Window width="1" height="1" colour="red"/>
              <Text name="t" x="0" y="0" width="1" height="1" txet="typo"/>
            </UIScript>
            """);

        Assert.Equal(
            [
                new UIIgnoredAttribute("UIScript", "alpha", 1),
                new UIIgnoredAttribute("UIScript", "zeta", 1),
                new UIIgnoredAttribute("Window", "colour", 2),
                new UIIgnoredAttribute("Text", "txet", 3),
            ],
            script.IgnoredAttributes);
    }

    [Theory]
    [InlineData("<UIScript version='2'><Bogus/></UIScript>", "version 2")]
    [InlineData("<UIScript requires='teleport'><Bogus/></UIScript>", "requires \"teleport\"")]
    [InlineData("<UIScript version='0'><Window width='1' height='1'/></UIScript>", "version=\"0\"")]
    public void ReportsANewerDocumentBeforeAnythingElse(string text, string message) =>
        Assert.Contains(message, Assert.Throws<UIScriptException>(() => UIScript.Parse(text)).Message);

    [Theory]
    [InlineData("<Interface><Window width='1' height='1'/></Interface>", "must be <UIScript>")]
    [InlineData("<UIScript/>", "needs a <Window>")]
    [InlineData("<UIScript><Window width='1' height='1'/><Window width='1' height='1'/></UIScript>", "more than one <Window>")]
    [InlineData("<UIScript><Window width='1' height='1'/><Gizmo/></UIScript>", "<Gizmo> isn't a UIScript element")]
    [InlineData("<UIScript><Window width='0' height='1'/></UIScript>", "must be positive")]
    [InlineData("<UIScript><Window width='1'/></UIScript>", "needs height")]
    [InlineData("<UIScript><Window width='wide' height='1'/></UIScript>", "width=\"wide\"")]
    [InlineData("<UIScript><Window width='inf' height='1'/></UIScript>", "width=\"inf\"")]
    [InlineData("<UIScript><Window width='10' height='10' minWidth='5'/></UIScript>", "needs minHeight")]
    [InlineData("<UIScript><Window width='10' height='10' minWidth='20' minHeight='5'/></UIScript>", "minWidth and minHeight")]
    [InlineData("<UIScript><Window width='1' height='1'/><Style name='s' fontSize='0'/></UIScript>", "positive fontSize")]
    [InlineData("<UIScript><Window width='1' height='1'/><Style name='s' fontSize='1.5'/></UIScript>", "fontSize=\"1.5\"")]
    [InlineData("<UIScript><Window width='1' height='1'/><Style name='s' textColor='red'/></UIScript>", "textColor=\"red\"")]
    [InlineData("<UIScript><Window width='1' height='1'/><Style name='s' textColor='#12345'/></UIScript>", "textColor=\"#12345\"")]
    [InlineData("<UIScript><Window width='1' height='1'/><Image name='i'/></UIScript>", "needs a path, an assetID")]
    [InlineData("<UIScript><Window width='1' height='1'/><Image name='i' path='p' stateCount='3'/></UIScript>", "stateCount 3")]
    [InlineData("<UIScript><Window width='1' height='1'/><Image name='i' path='p'/><Image name='i' path='q'/></UIScript>", "\"i\" is used more than once")]
    [InlineData("<UIScript><Window width='1' height='1'/><Region name='a' x='0' y='0' width='1' height='1'/><Text name='a' x='0' y='0' width='1' height='1'/></UIScript>", "\"a\" is used more than once")]
    [InlineData("<UIScript><Window width='1' height='1'/><Visual name='v' image='nope' x='0' y='0'/></UIScript>", "image=\"nope\", which isn't declared")]
    [InlineData("<UIScript><Window width='1' height='1'/><Text name='t' x='0' y='0'/></UIScript>", "needs a width and a height")]
    [InlineData("<UIScript><Window width='1' height='1'/><Text name='t' x='0' y='0' width='-1' height='1'/></UIScript>", "negative width")]
    [InlineData("<UIScript><Window width='1' height='1'/><Text name='t' y='0' width='1' height='1'/></UIScript>", "needs x")]
    [InlineData("<UIScript><Window width='1' height='1'/><Text name='t' x='0' y='0' width='1' height='1' text='a' string='b'/></UIScript>", "both text and string")]
    [InlineData("<UIScript><Window width='1' height='1'/><Text name='t' x='0' y='0' width='1' height='1' string='missing'/></UIScript>", "text=\"missing\"")]
    [InlineData("<UIScript><Window width='1' height='1'/><Text name='t' x='0' y='0' width='1' height='1' tooltip='missing'/></UIScript>", "tooltip=\"missing\"")]
    [InlineData("<UIScript><Window width='1' height='1'/><Text name='t' x='0' y='0' width='1' height='1' style='missing'/></UIScript>", "style=\"missing\"")]
    [InlineData("<UIScript><Window width='1' height='1'/><Text name='t' x='0' y='0' width='1' height='1' placeholder='p'/></UIScript>", "can't have placeholder")]
    [InlineData("<UIScript><Window width='1' height='1'/><TextField name='t' x='0' y='0' width='1' height='1' maximumLength='0'/></UIScript>", "maximumLength 0")]
    [InlineData("<UIScript><Window width='1' height='1'/><Text name='t' x='0' y='0' width='1' height='1' readOnly='true'/></UIScript>", "can't have readOnly")]
    [InlineData("<UIScript><Window width='1' height='1'/><TextField name='t' x='0' y='0' width='1' height='1' readOnly='yes'/></UIScript>", "readOnly=\"yes\"")]
    [InlineData("<UIScript><Window width='1' height='1'/><Text name='t' x='0' y='0' width='1' height='1' secure='true'/></UIScript>", "can't have secure")]
    [InlineData("<UIScript><Window width='1' height='1'/><Text name='t' x='0' y='0' width='1' height='1' selectable='true'/></UIScript>", "can't have selectable")]
    [InlineData("<UIScript><Window width='1' height='1'/><Text name='t' x='0' y='0' width='1' height='1' alignment='middle'/></UIScript>", "alignment=\"middle\"")]
    [InlineData("<UIScript><Window width='1' height='1'/><Text name='t' x='0' y='0' width='1' height='1' orientation='vertical'/></UIScript>", "can't have orientation")]
    [InlineData("<UIScript><Window width='1' height='1'/><Slider name='s' x='0' y='0' width='1' height='1' minValue='2' maxValue='1'/></UIScript>", "minValue must be below maxValue")]
    [InlineData("<UIScript><Window width='1' height='1'/><Text name='t' x='0' y='0' width='1' height='1' maxValue='2'/></UIScript>", "only a Slider or ProgressBar")]
    [InlineData("<UIScript><Window width='1' height='1'/><Text name='t' x='0' y='0' image='i'/><Image name='i' path='p'/></UIScript>", "can't have image")]
    [InlineData("<UIScript><Window width='1' height='1'/><Button name='b' x='0' y='0' image='nope'/></UIScript>", "image=\"nope\"")]
    [InlineData("<UIScript><Window width='1' height='1'/><Slider name='s' x='0' y='0' width='1' height='1' scrolls='nope'/></UIScript>", "scrolls=\"nope\"")]
    [InlineData("<UIScript><Window width='1' height='1'/><List name='l' x='0' y='0' width='1' height='1'/><Slider name='s' x='0' y='0' width='1' height='1' scrolls='l' direction='up'/></UIScript>", "scrolls or direction")]
    [InlineData("<UIScript><Window width='1' height='1'/><List name='l' x='0' y='0' width='1' height='1'/><Button name='b' x='0' y='0' width='1' height='1' scrolls='l'/></UIScript>", "scrolls or direction")]
    [InlineData("<UIScript><Window width='1' height='1'/><Text name='x' x='0' y='0' width='1' height='1'/><Button name='b' x='0' y='0' width='1' height='1' scrolls='x' direction='up'/></UIScript>", "scrolls or direction")]
    [InlineData("<UIScript><Window width='1' height='1'/><Button name='b' x='0' y='0' width='1' height='1' direction='up'/></UIScript>", "scrolls or direction")]
    [InlineData("<UIScript><Window width='1' height='1'/><Region name='r' x='0' y='0' width='1' height='1'>text</Region></UIScript>", "can't hold text")]
    [InlineData("<!DOCTYPE x><UIScript/>", "DOCTYPE")]
    public void RefusesWhatHudiniRefuses(string text, string message) =>
        Assert.Contains(message, Assert.Throws<UIScriptException>(() => UIScript.Parse(text)).Message);

    [Fact]
    public void NamesTheLineOfAnElementsProblem()
    {
        var exception = Assert.Throws<UIScriptException>(() => UIScript.Parse("<UIScript>\n<Window width='1' height='1'/>\n<Text name='t' x='0' y='0'/>\n</UIScript>"));

        Assert.Equal(3, exception.Line);
        Assert.StartsWith("Line 3: ", exception.Message);
    }

    [Fact]
    public void RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => UIScript.Parse(null!));
        Assert.Throws<ArgumentNullException>(() => UIScript.Load(null!));
    }
}
