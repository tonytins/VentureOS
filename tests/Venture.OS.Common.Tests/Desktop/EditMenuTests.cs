using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// The standard Edit menu: Cut, Copy, Paste, and Select All for whatever text has focus, in any window, through one
/// clipboard the desktop keeps. It shows while text has focus.
/// </summary>
public sealed class EditMenuTests
{
    private const string Document = """
        <UIScript requires="menus">
          <Window width="400" height="200"/>
          <TitleField name="address"/>
          <TextField name="name" x="10" y="10" width="300" height="24"/>
          <TextField name="short" x="10" y="40" width="300" height="24" maximumLength="5"/>
          <TextField name="secret" x="10" y="70" width="300" height="24" secure="true"/>
          <TextArea name="notes" x="10" y="100" width="300" height="80"/>
        </UIScript>
        """;

    private readonly DesktopSession session = new(1024, 768);
    private readonly DesktopWindow window;
    private readonly List<UIEvent> events = [];

    public EditMenuTests()
    {
        window = new DesktopWindow("Form", new UIView(UIScript.Parse(Document)))
        {
            EventHandler = (_, uiEvent) => events.Add(uiEvent),
        };
        session.Windows.Open(window);
        Frame();
    }

    private UIView View => window.View;

    [Fact]
    public void IsAlwaysThereWithWhatCantActDimmed()
    {
        Assert.Equal(["VentureOS", "File", "Edit", "Help"], Titles());
        Assert.Equal(["Cut", "Copy", "Paste", null, "Select All"], Items("Edit"));
        Assert.Equal([false, false, false, false], Enabled());

        // Text with focus can be selected; with some selected, cut and copied; and once copied, pasted over.
        Focus("name", "Hello");
        Assert.Equal([false, false, false, true], Enabled());
        Press('a');
        Assert.Equal([true, true, false, true], Enabled());
        Press('c');
        Assert.Equal([true, true, true, true], Enabled());
    }

    [Fact]
    public void CopyAndPasteMoveTextBetweenFields()
    {
        Focus("name", "Hello world");
        Press('a');
        Press('c');
        Assert.Equal("Hello world", session.Clipboard);

        Focus("notes", "");
        Press('v');

        Assert.Equal("Hello world", View.Text("notes"));
        Assert.Equal("Hello world", View.Text("name"));
        Assert.Contains(new UIEvent(UIEventKind.Changed, "notes", "Hello world"), events);
    }

    [Fact]
    public void CutTakesTheSelection()
    {
        Focus("name", "Hello world");
        Keys(new KeyPress(DesktopKey.End), Shift(DesktopKey.Left), Shift(DesktopKey.Left), Shift(DesktopKey.Left),
            Shift(DesktopKey.Left), Shift(DesktopKey.Left), Shift(DesktopKey.Left));

        Press('x');

        Assert.Equal(" world", session.Clipboard);
        Assert.Equal("Hello", View.Text("name"));
    }

    [Fact]
    public void NothingSelectedMeansNothingToCopy()
    {
        Focus("name", "Hello");
        Press('c');

        Assert.Null(session.Clipboard);
    }

    [Fact]
    public void PastingIntoAFieldKeepsItOnOneLineAndWithinItsLength()
    {
        Focus("notes", "two\nlines");
        Press('a');
        Press('c');

        Focus("name", "");
        Press('v');
        Assert.Equal("two lines", View.Text("name"));

        Focus("short", "");
        Press('v');
        Assert.Equal("two l", View.Text("short"));
    }

    [Fact]
    public void ASecureFieldCantBeCopied()
    {
        Focus("secret", "hunter2");
        Press('a');
        Press('c');
        Press('x');

        Assert.Null(session.Clipboard);
        Assert.Equal("hunter2", View.Text("secret"));
    }

    [Fact]
    public void TheTitleFieldCopiesAndPastesToo()
    {
        View.SetText("address", "/mnt/user");
        window.BeginTitleEdit();
        Frame();
        Assert.Contains("Edit", Titles());

        Press('c');
        Assert.Equal("/mnt/user", session.Clipboard);

        Frame(new KeyPress(DesktopKey.End));
        Press('v');
        Assert.Equal("/mnt/user/mnt/user", View.Text("address"));
    }

    private static KeyPress Shift(DesktopKey key) => new(key, Shift: true);

    private void Focus(string name, string text)
    {
        View.SetText(name, text);
        View.Focus(name);
        Frame();
    }

    // Ctrl and a letter, the Edit menu's shortcuts.
    private void Press(char letter) => Frame(new KeyPress(DesktopKey.Character, letter, Control: true));

    private void Keys(params KeyPress[] keys) => Frame(keys);

    private void Frame(params KeyPress[] keys) => session.Update(new DesktopInput(1000, 700, false, Keys: keys));

    private List<string> Titles() => [.. session.MenuBar.Menus.Select(menu => menu.Menu.Title)];

    // Whether Cut, Copy, Paste, and Select All can act, in that order.
    private List<bool> Enabled()
    {
        var edit = session.MenuBar.Menus.Single(bar => bar.Menu.Title == "Edit");
        return [.. edit.Menu.Entries.Where(entry => !entry.IsSeparator).Select(edit.IsEnabled)];
    }

    private List<string?> Items(string menu) =>
        [.. session.MenuBar.Menus.Single(bar => bar.Menu.Title == menu).Menu.Entries.Select(entry => entry.Title)];
}
