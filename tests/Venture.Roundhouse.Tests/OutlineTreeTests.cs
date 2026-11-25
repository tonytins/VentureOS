using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Venture.OS.Desktop;
using Venture.Roundhouse.Designer;

namespace Venture.Roundhouse.Tests;

/// <summary>
/// The outline as a tree, with the shipped Notes, whose Note menu holds its items and Sort By: indented by depth,
/// menus and panes folding, and Move Into and Move Out from the Edit menu and the outline's Tab and Shift+Tab.
/// </summary>
[Collection(nameof(PaletteCollection))]
public sealed class OutlineTreeTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"roundhouse-outline-{Guid.NewGuid():N}");
    private readonly string document;

    public OutlineTreeTests()
    {
        Directory.CreateDirectory(folder);
        document = Path.Combine(folder, "main.uix");
        var notes = Path.Combine(SkiaPainterTests.RepositoryRoot(), "rootfs", "user", "apps", "Notes.app", "Contents", "Resources", "main.uix");
        File.Copy(notes, document);
    }

    public void Dispose()
    {
        DesktopAppearance.Use(DesktopLook.Standard);
        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public void TheOutlineIndentsWhatsInsideAMenuAndMarksTheMenuOpen() => TestApp.Run(() =>
    {
        var window = Open();
        var labels = Labels(window);
        var note = Index(window, "noteMenu");
        var sort = Index(window, "sortMenu");

        Assert.Equal(window.Document!.Xml!.Children.Count, labels.Length);
        Assert.Equal("▾ Menu  noteMenu", labels[note]);
        Assert.Equal("    MenuItem  newNote", labels[note + 1]);
        Assert.Equal("    ▾ Menu  sortMenu", labels[sort]);
        Assert.Equal("        MenuItem  sortAdded", labels[sort + 1]);
        Assert.Equal("    MenuSeparator", labels[note + 3]);
        Assert.Equal("ToolbarItem  add", labels[1]);
        window.Close();
    });

    [Fact]
    public void FoldingAMenuHidesWhatsInItAndTheRowsStillSelectTheirElements() => TestApp.Run(() =>
    {
        var window = Open();
        var outline = window.FindControl<ListBox>("OutlineList")!;
        var note = Index(window, "noteMenu");
        var text = Index(window, "text");

        window.SetFolded(note, true);

        Assert.True(window.IsFolded(note));
        Assert.Equal("▸ Menu  noteMenu", Labels(window)[note]);
        Assert.DoesNotContain(note + 1, window.OutlineRows);
        Assert.Equal("TextField  text", Labels(window)[note + 1]);

        // The row after the folded menu is the text field, however many elements the menu holds.
        outline.SelectedIndex = note + 1;
        Assert.Equal(text, window.Selection);
        window.Close();
    });

    [Fact]
    public void FoldingAroundTheSelectionSelectsTheMenuAndSelectingInsideUnfoldsIt() => TestApp.Run(() =>
    {
        var window = Open();
        var note = Index(window, "noteMenu");
        var byTitle = Index(window, "sortTitle");

        window.Select(byTitle);
        window.SetFolded(note, true);
        Assert.Equal(note, window.Selection);

        window.Select(byTitle);
        Assert.False(window.IsFolded(note));
        Assert.Contains(byTitle, window.OutlineRows);
        window.Close();
    });

    [Fact]
    public void FoldsSurviveEditsButNotAnotherDocument() => TestApp.Run(() =>
    {
        var window = Open();
        var sort = Index(window, "sortMenu");
        window.SetFolded(sort, true);

        // Adding a control renumbers nothing before it, but a fold kept by name would survive even if it did.
        Assert.True(window.AddControl("Button"));
        Assert.True(window.IsFolded(Index(window, "sortMenu")));

        window.Open(document);
        Dispatcher.UIThread.RunJobs();
        Assert.False(window.IsFolded(Index(window, "sortMenu")));
        window.Close();
    });

    [Fact]
    public void LeftAndRightFoldUnfoldAndGoUpInTheOutline() => TestApp.Run(() =>
    {
        var window = Open();
        var outline = window.FindControl<ListBox>("OutlineList")!;
        var sort = Index(window, "sortMenu");
        var byDate = Index(window, "sortAdded");
        window.Select(byDate);
        FocusOutline(window);

        window.KeyPress(Key.Left, RawInputModifiers.None, PhysicalKey.ArrowLeft, null);
        Assert.Equal(sort, window.Selection);
        window.KeyPress(Key.Left, RawInputModifiers.None, PhysicalKey.ArrowLeft, null);
        Assert.True(window.IsFolded(sort));
        window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
        Assert.False(window.IsFolded(sort));

        // The arrows worked the outline, not the canvas, so nothing moved.
        Assert.Equal(File.ReadAllText(document), window.Text);
        window.Close();
    });

    [Fact]
    public void TabMovesTheSelectionIntoTheMenuBeforeItAndShiftTabBackOut() => TestApp.Run(() =>
    {
        var window = Open();
        var outline = window.FindControl<ListBox>("OutlineList")!;
        var clear = Index(window, "clearAll");
        var separator = clear - 1;
        window.Select(separator);
        FocusOutline(window);

        // The separator before Clear All follows Sort By, so Tab takes it into Sort By, at its end.
        window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
        var sort = Index(window, "sortMenu");
        Assert.Equal(sort, window.Document!.Xml!.Children[window.Selection!.Value].Parent);
        Assert.Equal("MenuSeparator", window.Document.Xml.Children[window.Selection.Value].Name);
        Assert.Contains("""
                  <MenuItem name="sortTitle" title="A to Z" hotkey="A"/>
                  <MenuSeparator/>
                </Menu>
            """, window.Text, StringComparison.Ordinal);

        // The outline keeps the keyboard through the move, so Shift+Tab follows on.
        Assert.True(outline.IsKeyboardFocusWithin);
        window.KeyPress(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, null);
        Assert.Equal(File.ReadAllText(document), window.Text);
        Assert.Equal(separator, window.Selection);
        window.Close();
    });

    [Fact]
    public void MoveIntoAndOutAreInTheEditMenuWhenThereIsSomewhereToGo() => TestApp.Run(() =>
    {
        var window = Open();
        var into = Command(window, "Move Into");
        var outOf = Command(window, "Move Out");

        window.Select(Index(window, "text"));
        Assert.True(into.IsEnabled!());
        Assert.False(outOf.IsEnabled!());

        // A text field into the Note menu isn't something VentureOS reads, so it's refused and nothing changes.
        into.Run();
        Assert.Equal(File.ReadAllText(document), window.Text);
        Assert.Contains("can't be inside <Menu>", window.Status, StringComparison.Ordinal);

        window.Select(Index(window, "newNote"));
        Assert.False(into.IsEnabled!());
        Assert.True(outOf.IsEnabled!());
        window.Close();
    });

    [Fact]
    public void AControlNamingAPaneMovesIntoItAndStopsNamingIt() => TestApp.Run(() =>
    {
        File.WriteAllText(document, """
            <UIScript version="1">
              <Window width="300" height="200"/>
              <Sidebar name="side" width="100"/>
              <Pane name="general" title="General"/>
              <Button name="go" x="0" y="0" width="10" height="10" pane="general"/>
            </UIScript>
            """);
        var window = Open();

        window.Select(Index(window, "go"));
        Assert.True(window.MoveIntoSelection());

        Assert.Contains("""
              <Pane name="general" title="General">
                <Button name="go" x="0" y="0" width="10" height="10"/>
              </Pane>
            """, window.Text, StringComparison.Ordinal);
        Assert.Equal("general", window.Document!.Script!.Control("go")!.Pane);
        window.Close();
    });

    [Fact]
    public void ASidebarsControlsAreIndentedUnderItAndTabMovesOneIn() => TestApp.Run(() =>
    {
        File.WriteAllText(document, """
            <UIScript version="1">
              <Window width="300" height="200"/>
              <Sidebar name="player" width="160">
                <Button name="stop" x="10" y="10" width="80" height="24" text="Stop"/>
              </Sidebar>
              <Button name="play" x="10" y="40" width="80" height="24" text="Play"/>
            </UIScript>
            """);
        var window = Open();
        var labels = Labels(window);

        Assert.Equal("▾ Sidebar  player", labels[Index(window, "player")]);
        Assert.Equal("    Button  stop", labels[Index(window, "stop")]);

        window.Select(Index(window, "play"));
        FocusOutline(window);
        window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);

        Assert.True(window.Document!.Script!.Control("play")!.InSidebar);
        window.Close();
    });

    private MainWindow Open()
    {
        MainWindow window = new() { AskToLeave = _ => Task.FromResult(Bundles.LeaveChoice.DontSave) };
        window.Show();
        window.Open(document);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>Gives the outline's selected row the keyboard, as clicking it would.</summary>
    private static void FocusOutline(MainWindow window)
    {
        var outline = window.FindControl<ListBox>("OutlineList")!;
        Dispatcher.UIThread.RunJobs();
        var row = outline.ContainerFromIndex(outline.SelectedIndex) ?? throw new InvalidOperationException("The selected row isn't shown.");
        Assert.True(row.Focus(), "The outline's row didn't take the keyboard.");
        Assert.True(outline.IsKeyboardFocusWithin);
    }

    private static string[] Labels(MainWindow window) => (string[])window.FindControl<ListBox>("OutlineList")!.ItemsSource!;

    private static int Index(MainWindow window, string name) =>
        window.Document!.Xml!.Children.ToList().FindIndex(element => element["name"] == name);

    private static Menus.MenuCommand Command(MainWindow window, string title) =>
        Menus.AppMenu.Find(window.Menus, "Edit", title) ?? throw new InvalidOperationException($"No Edit ▸ {title}.");
}
