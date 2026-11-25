using Venture.OS.Desktop;
using Venture.OS.Interface;
using Venture.OS.Vdb;

namespace Venture.OS.Tests.Vdb;

/// <summary>
/// A desktop for the <c>ui</c> feature's tests: real windows, from a sample document of the tests' own, recording the
/// events and keys they're handed, and a screen of whatever pixels a test sets.
/// </summary>
internal sealed class FakeVdbDesktop : IVdbDesktop
{
    /// <summary>A window with something of each kind the conversations use.</summary>
    public const string Sample = """
        <UIScript>
          <Window width="300" height="200" subtitle="2 notes"/>
          <Menu name="file" title="File"/>
          <MenuItem menu="file" name="newNote" title="New Note"/>
          <Text name="label" x="10" y="10" width="100" height="20" text="Say &quot;hi&quot;"/>
          <Button name="ok" x="10" y="40" width="60" height="20" text="OK"/>
          <TextField name="name" x="10" y="70" width="200" height="24"/>
          <List name="notes" x="10" y="100" width="200" height="80"/>
          <Slider name="volume" x="220" y="100" width="70" height="20" maxValue="10" step="1"/>
        </UIScript>
        """;

    /// <summary>The windows, whose notifications' clock stands still, so ages in answers are always 0 seconds.</summary>
    public WindowManager Windows { get; } = new(1024, 768) { Notifications = new(() => TimeSpan.Zero), IsShowing = true };

    /// <summary>What each window was handed, as <c>&lt;id&gt; &lt;kind&gt; &lt;control&gt; [text or row]</c>.</summary>
    public List<string> Events { get; } = [];

    /// <summary>The keys pressed, as <c>&lt;id&gt; &lt;key&gt;</c>.</summary>
    public List<string> Keys { get; } = [];

    /// <summary>The screen a screenshot gives, or <see langword="null"/> for one that can't be read.</summary>
    public VdbScreenshot? Screen { get; set; }

    /// <summary>Opens the sample as an app's window, with two notes in its list.</summary>
    public DesktopWindow OpenApp(string app, string title)
    {
        var window = Record(new DesktopWindow(title, new UIView(UIScript.Parse(Sample))) { App = app });
        window.View.List("notes")!.Items.AddRange(["Milk", "Eggs"]);
        Windows.Open(window);
        return window;
    }

    /// <summary>Opens an app's panel, such as the dock's.</summary>
    public DesktopWindow OpenPanel(string app)
    {
        var window = Record(new DesktopWindow(app, new UIView(UIScript.Parse("""
            <UIScript><Panel width="300" height="24" edge="bottom"/><Button name="ok" x="0" y="0" width="40" height="20"/></UIScript>
            """))) { App = app });
        Windows.Open(window);
        return window;
    }

    /// <summary>Opens one of the system's own windows, such as the Trust alert, which no app owns.</summary>
    public DesktopWindow OpenSystemAlert(string title)
    {
        var window = Record(DesktopAlert.Create(title, "A computer wants to send apps.", "OK"));
        Windows.Open(window);
        return window;
    }

    public string? PressKeys(DesktopWindow window, IReadOnlyList<KeyPress> keys)
    {
        foreach (var key in keys)
            Keys.Add($"{window.Id} {Name(key)}");
        return null;
    }

    // A key as the conversations write it: its modifiers, then its character or its name.
    public static string Name(KeyPress key) =>
        (key.Control ? "Ctrl+" : "") + (key.Shift ? "Shift+" : "") + (key.Alt ? "Alt+" : "")
        + (key.Key == DesktopKey.Character ? key.Character.ToString() : key.Key.ToString());

    public VdbScreenshot? Screenshot() => Screen;

    private DesktopWindow Record(DesktopWindow window)
    {
        var inner = window.EventHandler;
        window.EventHandler = (sender, uiEvent) =>
        {
            Events.Add(uiEvent.Kind switch
            {
                UIEventKind.Selected => $"{sender.Id} selected {uiEvent.Control} {uiEvent.Index + 1}",
                _ when uiEvent.Value is { } value => $"{sender.Id} {uiEvent.Kind.ToString().ToLowerInvariant()} {uiEvent.Control} {value}",
                _ when uiEvent.Text is not null => $"{sender.Id} {uiEvent.Kind.ToString().ToLowerInvariant()} {uiEvent.Control} {uiEvent.Text}",
                _ => $"{sender.Id} {uiEvent.Kind.ToString().ToLowerInvariant()} {uiEvent.Control}",
            });
            inner?.Invoke(sender, uiEvent);
        };
        return window;
    }
}
