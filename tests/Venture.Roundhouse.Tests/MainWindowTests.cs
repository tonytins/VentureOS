using Avalonia;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Venture.OS.Desktop;

namespace Venture.Roundhouse.Tests;

/// <summary>Roundhouse's main window, opened and drawn off-screen: a document's text beside its live preview.</summary>
[Collection(nameof(PaletteCollection))]
public sealed class MainWindowTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"roundhouse-window-{Guid.NewGuid():N}");
    private readonly string document;

    public MainWindowTests()
    {
        Directory.CreateDirectory(folder);
        document = Path.Combine(folder, "main.uix");
        var calculator = Path.Combine(SkiaPainterTests.RepositoryRoot(), "rootfs", "user", "apps", "Calculator.app", "Contents", "Resources", "main.uix");
        File.Copy(calculator, document);
    }

    public void Dispose()
    {
        DesktopAppearance.Use(DesktopLook.Standard);
        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public void IsTitledWithTheVersionItSharesWithVentureOS() => TestApp.Run(() =>
    {
        MainWindow window = new();

        window.Show();

        Assert.Matches($@"^Roundhouse {System.Text.RegularExpressions.Regex.Escape(SkiaPainterTests.Release())}\.\d+$", window.Title);
        Assert.Equal(MainWindow.ProductTitle, window.Title);
        window.Close();
    });

    [Fact]
    public void DrawsAFrame() => TestApp.Run(() =>
    {
        MainWindow window = new();
        window.Show();

        var frame = window.CaptureRenderedFrame();

        Assert.NotNull(frame);
        Assert.Equal((1100, 720), (frame.PixelSize.Width, frame.PixelSize.Height));
        window.Close();
    });

    [Fact]
    public void OpeningADocumentShowsItsTextAndDrawsIt() => TestApp.Run(() =>
    {
        MainWindow window = new();
        window.Show();

        window.Open(document);

        Assert.Equal(File.ReadAllText(document), window.Text);
        Assert.Equal($"main.uix — {MainWindow.ProductTitle}", window.Title);
        Assert.NotNull(window.Preview);
        Assert.Equal("VentureOS reads this document.", window.Status);
        window.Close();
    });

    [Fact]
    public void AFileThatCantBeOpenedSaysSoAndOpensNothing() => TestApp.Run(() =>
    {
        MainWindow window = new();
        window.Show();

        window.Open(Path.Combine(folder, "missing.uix"));

        Assert.Null(window.DocumentPath);
        Assert.Null(window.Preview);
        Assert.Contains("couldn't be opened", window.Status);
        window.Close();
    });

    [Fact]
    public void TextVentureOSRefusesKeepsTheLastDrawingAndNamesTheLine() => TestApp.Run(() =>
    {
        MainWindow window = new();
        window.Show();
        window.Open(document);
        var drawn = window.Preview;

        window.Text = "<?xml version=\"1.0\"?>\n<Begin>\n<Nope";
        window.RenderNow();

        Assert.Same(drawn, window.Preview);
        Assert.StartsWith("Line ", window.Status);
        Assert.Contains("last version VentureOS could read", window.Status);
        window.Close();
    });

    [Fact]
    public void TypingRedrawsAfterAPause() => TestApp.Run(() =>
    {
        MainWindow window = new();
        window.Show();
        window.Open(document);
        var drawn = window.Preview;

        window.Text += "\n";
        Assert.Same(drawn, window.Preview);

        // The redraw waits for typing to stop, then runs on the window's own thread: let it run for a while.
        DispatcherFrame frame = new();
        DispatcherTimer.RunOnce(() => frame.Continue = false, MainWindow.PreviewDelay * 3);
        Dispatcher.UIThread.PushFrame(frame);

        Assert.NotSame(drawn, window.Preview);
        window.Close();
    });

    [Fact]
    public void TheDarkSwitchRedrawsInTheDarkPalette() => TestApp.Run(() =>
    {
        MainWindow window = new();
        window.Show();
        window.Open(document);
        var light = Pixel(window.Preview!, 40, 60);

        window.SetDark(true);

        Assert.NotEqual(light, Pixel(window.Preview!, 40, 60));
        window.Close();
    });

    [Fact]
    public void SaveWritesTheTextAsItIs() => TestApp.Run(() =>
    {
        MainWindow window = new();
        window.Show();
        window.Open(document);
        var edited = window.Text.Replace("Calculator", "Adder", StringComparison.Ordinal);

        window.Text = edited;

        Assert.True(window.Save());
        Assert.Equal(edited, File.ReadAllText(document));
        Assert.Equal("Saved main.uix.", window.Status);
        window.Close();
    });

    [Fact]
    public void AnUneditedDocumentIsSavedByteForByteWithItsByteOrderMark() => TestApp.Run(() =>
    {
        byte[] original = [0xEF, 0xBB, 0xBF, .. File.ReadAllBytes(document)];
        File.WriteAllBytes(document, original);
        MainWindow window = new();
        window.Show();
        window.Open(document);

        Assert.False(window.Text.StartsWith('\uFEFF'));
        Assert.True(window.Save());
        Assert.Equal(original, File.ReadAllBytes(document));
        window.Close();
    });

    [Fact]
    public void TheDocumentFollowsTheTextAndSaysWhenOnlyTheTextCanBeEdited() => TestApp.Run(() =>
    {
        MainWindow window = new();
        window.Show();
        window.Open(document);
        Assert.True(window.Document!.IsEditable);

        window.Text = window.Text.Replace("</UIScript>", "  <Hologram/>\n</UIScript>", StringComparison.Ordinal);
        window.RenderNow();

        Assert.False(window.Document.IsEditable);
        Assert.Contains("Hologram", window.Status);
        Assert.Contains("only the text can be edited", window.Status);
        window.Close();
    });

    [Fact]
    public void AnAppliedEditChangesOnlyItsTextRedrawsAndCanBeUndone() => TestApp.Run(() =>
    {
        MainWindow window = new();
        window.Show();
        window.Open(document);
        var before = window.Text;
        var drawn = window.Preview;
        var equals = window.Document!.Xml!.Children.ToList().FindIndex(element => element["name"] == "equals");

        window.Apply(window.Document.SetAttribute(equals, "text", "Go"));

        Assert.Equal(before.Replace("text=\"=\"", "text=\"Go\"", StringComparison.Ordinal), window.Text);
        Assert.Equal("Go", window.Document.Script!.Control("equals")!.Text!.Value);
        Assert.NotSame(drawn, window.Preview);

        var editor = window.FindControl<AvaloniaEdit.TextEditor>("Editor")!;
        Assert.True(editor.CanUndo);
        editor.Undo();
        Assert.Equal(before, window.Text);
        window.Close();
    });

    [Fact]
    public void TheDesignerHasAnOutlineACanvasOverTheTextAndAnInspector() => TestApp.Run(() =>
    {
        MainWindow window = new();
        window.Show();

        Assert.NotNull(window.FindControl<DockPanel>("OutlinePane"));
        Assert.NotNull(window.FindControl<DockPanel>("InspectorPane"));
        Assert.True(window.IsTextShowing);
        window.Close();
    });

    [Fact]
    public void TheTextCollapsesAndComesBackWhereItWas() => TestApp.Run(() =>
    {
        MainWindow window = new();
        window.Show();
        var center = window.FindControl<Grid>("CenterGrid")!;
        var tabs = window.FindControl<TabControl>("BottomTabs")!;
        center.RowDefinitions[2].Height = new GridLength(180);

        window.FindControl<ToggleButton>("TextToggle")!.IsChecked = false;

        Assert.False(window.IsTextShowing);
        Assert.False(tabs.IsVisible);
        Assert.Equal(0, center.RowDefinitions[2].Height.Value);

        window.FindControl<ToggleButton>("TextToggle")!.IsChecked = true;

        Assert.True(tabs.IsVisible);
        Assert.Equal(180, center.RowDefinitions[2].Height.Value);
        window.Close();
    });

    [Fact]
    public void ADrawnDocumentIsMappedAndAnElementCanBeSelected() => TestApp.Run(() =>
    {
        MainWindow window = new();
        window.Show();
        window.Open(document);
        var equals = window.Document!.Xml!.Children.ToList().FindIndex(element => element["name"] == "equals");
        var bounds = window.Map!.BoundsOf(equals)!.Value;

        var found = window.Map.ElementAt(bounds.X + 2, bounds.Y + 2);
        window.Select(found);

        Assert.Equal(equals, window.Selection);
        Assert.Throws<ArgumentOutOfRangeException>(() => window.Select(window.Document.Xml.Children.Count));
        window.Select(null);
        Assert.Null(window.Selection);
        window.Close();
    });

    [Fact]
    public void OpeningADocumentClearsTheSelection() => TestApp.Run(() =>
    {
        MainWindow window = new();
        window.Show();
        window.Open(document);
        window.Select(0);

        window.Open(document);

        Assert.Null(window.Selection);
        window.Close();
    });

    [Fact]
    public void ClickingTheCanvasSelectsWhatsThereWithHandles() => TestApp.Run(() =>
    {
        var (window, equals) = OpenCalculator();
        var bounds = window.Map!.BoundsOf(equals)!.Value;

        Click(window, bounds.X + 10, bounds.Y + 10);

        Assert.Equal(equals, window.Selection);
        Assert.Equal(9, Layer(window).Children.Count);

        Click(window, window.Map.Content.X + 2, window.Map.Content.Y + window.Map.Content.Height - 2);
        // The window has the right and bottom edges and their corner to resize it by.
        Assert.Equal(window.Map.Window, window.Selection);
        Assert.Equal(4, Layer(window).Children.Count);

        Click(window, 2, 2);
        Assert.Null(window.Selection);
        Assert.Empty(Layer(window).Children);
        window.Close();
    });

    [Fact]
    public void DraggingAControlMovesItInOneEditThatUndoes() => TestApp.Run(() =>
    {
        var (window, equals) = OpenCalculator();
        var before = window.Text;
        var bounds = window.Map!.BoundsOf(equals)!.Value;

        window.MouseDown(At(window, bounds.X + 10, bounds.Y + 10), MouseButton.Left);
        window.MouseMove(At(window, bounds.X + 15, bounds.Y + 12));
        window.MouseMove(At(window, bounds.X + 20, bounds.Y + 15));

        // While the drag goes on, the canvas follows it and the text waits.
        Assert.Equal(before, window.Text);
        Assert.Equal(bounds.X + 10, window.Map.BoundsOf(equals)!.Value.X);

        window.MouseUp(At(window, bounds.X + 20, bounds.Y + 15), MouseButton.Left);

        Assert.Contains("""<Button name="equals" x="184" y="261" width="46" height="40" text="="/>""", window.Text);
        Assert.Equal(equals, window.Selection);
        var editor = window.FindControl<AvaloniaEdit.TextEditor>("Editor")!;
        editor.Undo();
        Assert.Equal(before, window.Text);
        window.Close();
    });

    [Fact]
    public void DraggingAHandleResizes() => TestApp.Run(() =>
    {
        var (window, equals) = OpenCalculator();
        var bounds = window.Map!.BoundsOf(equals)!.Value;
        Click(window, bounds.X + 10, bounds.Y + 10);

        window.MouseDown(At(window, bounds.X + bounds.Width, bounds.Y + bounds.Height), MouseButton.Left);
        window.MouseMove(At(window, bounds.X + bounds.Width + 10, bounds.Y + bounds.Height + 4));
        window.MouseUp(At(window, bounds.X + bounds.Width + 10, bounds.Y + bounds.Height + 4), MouseButton.Left);

        Assert.Contains("""<Button name="equals" x="174" y="256" width="56" height="44" text="="/>""", window.Text);
        window.Close();
    });

    [Fact]
    public void AClickWithoutADragChangesNothing() => TestApp.Run(() =>
    {
        var (window, equals) = OpenCalculator();
        var before = window.Text;
        var bounds = window.Map!.BoundsOf(equals)!.Value;

        Click(window, bounds.X + 10, bounds.Y + 10);

        Assert.Equal(before, window.Text);
        Assert.False(window.FindControl<AvaloniaEdit.TextEditor>("Editor")!.CanUndo);
        window.Close();
    });

    [Fact]
    public void ArrowKeysNudgeByAPixelAndShiftByEight() => TestApp.Run(() =>
    {
        var (window, equals) = OpenCalculator();
        var bounds = window.Map!.BoundsOf(equals)!.Value;
        Click(window, bounds.X + 10, bounds.Y + 10);

        window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
        window.KeyPress(Key.Down, RawInputModifiers.Shift, PhysicalKey.ArrowDown, null);

        Assert.Contains("""<Button name="equals" x="175" y="264" """, window.Text);
        window.Close();
    });

    [Fact]
    public void AtTwiceTheSizeADragMovesHalfAsFar() => TestApp.Run(() =>
    {
        var (window, _) = OpenCalculator();
        window.SetZoom(true);
        Assert.Equal(2, window.Zoom);
        window.UpdateLayout();

        // The clear button is near the top, so it's still in view at twice the size.
        var clear = window.Document!.Xml!.Children.ToList().FindIndex(element => element["name"] == "clear");
        var bounds = window.Map!.BoundsOf(clear)!.Value;
        var screenStart = At(window, bounds.X + 10, bounds.Y + 10);

        window.MouseDown(screenStart, MouseButton.Left);
        window.MouseMove(screenStart + new Point(40, 0));
        window.MouseUp(screenStart + new Point(40, 0), MouseButton.Left);

        // 40 of the screen's pixels are 20 of VentureOS's at 200%.
        Assert.Contains("""<Button name="clear" x="32" y="64" """, window.Text);
        window.Close();
    });

    [Fact]
    public void AMoveVentureOSWouldRefuseSaysWhyAndLeavesTheText() => TestApp.Run(() =>
    {
        var (window, equals) = OpenCalculator();
        var before = window.Text;
        window.Text = before.Replace("""x="174" y="256" width="46" height="40" text="="/>""", """x="174" y="256" width="46" height="40" text="=" scrolls="display"/>""", StringComparison.Ordinal);
        window.RenderNow();

        // A button that scrolls something that isn't a list is refused, so nothing can be dragged onto the canvas.
        Assert.False(window.Document!.IsEditable);
        var bounds = window.Map!.BoundsOf(equals)!.Value;
        window.MouseDown(At(window, bounds.X + 10, bounds.Y + 10), MouseButton.Left);
        window.MouseMove(At(window, bounds.X + 20, bounds.Y + 10));
        window.MouseUp(At(window, bounds.X + 20, bounds.Y + 10), MouseButton.Left);

        Assert.DoesNotContain("x=\"184\"", window.Text);
        window.Close();
    });

    [Fact]
    public void TheOutlineListsEveryElementAndFollowsTheSelectionBothWays() => TestApp.Run(() =>
    {
        var (window, equals) = OpenCalculator();
        var outline = window.FindControl<ListBox>("OutlineList")!;
        var labels = (string[])outline.ItemsSource!;

        Assert.Equal(window.Document!.Xml!.Children.Count, labels.Length);
        Assert.Equal("Window", labels[0]);
        Assert.Equal("Button  equals", labels[equals]);

        var bounds = window.Map!.BoundsOf(equals)!.Value;
        Click(window, bounds.X + 10, bounds.Y + 10);
        Assert.Equal(equals, outline.SelectedIndex);

        // A style isn't drawn, so the outline is the way to select it.
        outline.SelectedIndex = 1;
        Assert.Equal(1, window.Selection);
        Assert.Empty(Layer(window).Children);
        Assert.Contains("fontSize", Inspector(window).Fields.Keys);
        window.Close();
    });

    [Fact]
    public void TheInspectorShowsTheSelectedElementsAttributesInGroups() => TestApp.Run(() =>
    {
        var (window, equals) = OpenCalculator();
        window.Select(equals);

        var inspector = Inspector(window);
        Assert.Equal("174", ((TextBox)inspector.Fields["x"].Editor).Text);
        Assert.Equal("", ((TextBox)inspector.Fields["style"].Editor).Text);
        Assert.IsType<ComboBox>(inspector.Fields["alignment"].Editor);
        Assert.DoesNotContain("placeholder", inspector.Fields.Keys);
        var headings = inspector.Children.OfType<TextBlock>().Select(block => block.Text).ToList();
        Assert.Equal(["<Button>", "Identity", "Frame", "Text", "Appearance", "Behaviour"], headings);
        window.Close();
    });

    [Fact]
    public void AValueTypedInTheInspectorIsWrittenOnEnter() => TestApp.Run(() =>
    {
        var (window, equals) = OpenCalculator();
        window.Select(equals);
        var x = (TextBox)Inspector(window).Fields["x"].Editor;

        x.Focus();
        x.Text = "180";
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);

        Assert.Contains("""<Button name="equals" x="180" y="256" """, window.Text);
        Assert.Equal(180, window.Map!.BoundsOf(equals)!.Value.X - window.Map.Content.X);
        Assert.Null(Inspector(window).Fields["x"].Problem);
        window.Close();
    });

    [Fact]
    public void AValueVentureOSRefusesShowsWhyUnderItsFieldAndChangesNothing() => TestApp.Run(() =>
    {
        var (window, equals) = OpenCalculator();
        window.Select(equals);
        var before = window.Text;
        var field = Inspector(window).Fields["width"];
        var width = (TextBox)field.Editor;

        width.Focus();
        width.Text = "wide";
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);

        Assert.Equal(before, window.Text);
        Assert.Contains("width", field.Problem);
        Assert.Equal("wide", width.Text);

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Assert.Equal("46", width.Text);
        Assert.Null(field.Problem);
        window.Close();
    });

    [Fact]
    public void EmptyingAFieldRemovesItsAttributeUnlessItsRequired() => TestApp.Run(() =>
    {
        var (window, equals) = OpenCalculator();
        window.Select(equals);
        var inspector = Inspector(window);

        var text = (TextBox)inspector.Fields["text"].Editor;
        text.Focus();
        text.Text = "";
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Assert.Contains("""<Button name="equals" x="174" y="256" width="46" height="40"/>""", window.Text);

        var name = (TextBox)inspector.Fields["name"].Editor;
        name.Focus();
        name.Text = "";
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Assert.Contains("name", inspector.Fields["name"].Problem);
        window.Close();
    });

    [Fact]
    public void ChoicesAndFlagsHaveTheirOwnFields() => TestApp.Run(() =>
    {
        var (window, equals) = OpenCalculator();
        window.Select(equals);
        ((ComboBox)Inspector(window).Fields["alignment"].Editor).SelectedItem = "center";
        Assert.Contains("""text="=" alignment="center"/>""", window.Text);

        window.Select(0);
        ((CheckBox)Inspector(window).Fields["resizable"].Editor).IsChecked = true;
        Assert.Contains("""<Window width="232" height="308" resizable="true"/>""", window.Text);
        ((CheckBox)Inspector(window).Fields["resizable"].Editor).IsChecked = false;
        Assert.Contains("""<Window width="232" height="308"/>""", window.Text);
        window.Close();
    });

    [Fact]
    public void AttributesVentureOSDoesntReadAreShownUnderOther() => TestApp.Run(() =>
    {
        var (window, equals) = OpenCalculator();
        window.Text = window.Text.Replace("""text="="/>""", """text="=" note="keep me"/>""", StringComparison.Ordinal);
        window.RenderNow();
        window.Select(equals);

        var inspector = Inspector(window);
        Assert.Equal("keep me", ((TextBox)inspector.Fields["note"].Editor).Text);
        Assert.Contains("Other", inspector.Children.OfType<TextBlock>().Select(block => block.Text));
        window.Close();
    });

    [Fact]
    public void TheOutlineAndInspectorWaitWhileVentureOSRefusesTheText() => TestApp.Run(() =>
    {
        var (window, _) = OpenCalculator();
        window.Select(0);

        window.Text = window.Text.Replace("</UIScript>", "<Hologram/></UIScript>", StringComparison.Ordinal);
        window.RenderNow();

        Assert.False(window.FindControl<DockPanel>("OutlinePane")!.IsEnabled);
        Assert.False(window.FindControl<DockPanel>("InspectorPane")!.IsEnabled);
        Assert.Empty(Inspector(window).Fields);
        window.Close();
    });

    [Fact]
    public void TheLibraryAddsAControlAndSelectsIt() => TestApp.Run(() =>
    {
        var (window, _) = OpenCalculator();
        var count = window.Document!.Xml!.Children.Count;

        Assert.True(window.AddControl("Slider"));

        Assert.Equal(count, window.Selection);
        Assert.Contains("""<Slider name="slider" x="46" y="144" width="140" height="20"/>""", window.Text);
        Assert.NotNull(window.Map!.BoundsOf(count));
        Assert.Equal("Slider  slider", ((string[])window.FindControl<ListBox>("OutlineList")!.ItemsSource!)[count]);
        window.Close();
    });

    [Fact]
    public void DeleteDuplicateAndRestackWorkFromTheCanvasKeys() => TestApp.Run(() =>
    {
        var (window, equals) = OpenCalculator();
        var bounds = window.Map!.BoundsOf(equals)!.Value;
        Click(window, bounds.X + 10, bounds.Y + 10);

        window.KeyPress(Key.D, RawInputModifiers.Control, PhysicalKey.D, "d");
        Assert.Equal(equals + 1, window.Selection);
        Assert.Contains("""<Button name="equals2" x="182" y="264" """, window.Text);

        window.KeyPress(Key.OemOpenBrackets, RawInputModifiers.Control, PhysicalKey.BracketLeft, "[");
        Assert.Equal(equals, window.Selection);
        Assert.Equal("equals2", window.Document!.Xml!.Children[equals]["name"]);

        window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);
        Assert.Null(window.Selection);
        Assert.DoesNotContain("equals2", window.Text);
        window.Close();
    });

    [Fact]
    public void DeletingTheWindowIsRefusedWithTheReason() => TestApp.Run(() =>
    {
        var (window, _) = OpenCalculator();
        var before = window.Text;
        window.Select(0);

        Assert.False(window.DeleteSelection());

        Assert.Equal(before, window.Text);
        Assert.Equal(0, window.Selection);
        Assert.Contains("needs a <Window>", window.Status);
        window.Close();
    });

    [Fact]
    public void DeleteInTheTextEditorStillDeletesText() => TestApp.Run(() =>
    {
        var (window, equals) = OpenCalculator();
        window.Select(equals);
        var editor = window.FindControl<AvaloniaEdit.TextEditor>("Editor")!;
        var length = window.Text.Length;

        editor.TextArea.Focus();
        editor.CaretOffset = 0;
        window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);

        Assert.Equal(length - 1, window.Text.Length);
        Assert.Equal(equals, window.Selection);
        window.Close();
    });

    [Fact]
    public void TheMenusFollowTheSelection() => TestApp.Run(() =>
    {
        var (window, equals) = OpenCalculator();
        Assert.True(window.FindControl<Button>("AddButton")!.IsEnabled);
        Assert.False(Command(window, "Edit", "Delete").IsEnabled!());

        window.Select(1);
        Assert.True(Command(window, "Edit", "Duplicate").IsEnabled!());
        Assert.False(Command(window, "Arrange", "Bring Forward").IsEnabled!());

        window.Select(equals);
        Assert.True(Command(window, "Arrange", "Bring Forward").IsEnabled!());
        window.Close();
    });

    [Fact]
    public void TheGlobalMenuHasEveryMenuWithItsShortcuts() => TestApp.Run(() =>
    {
        var (window, equals) = OpenCalculator();
        window.Select(equals);
        var menu = NativeMenu.GetMenu(window)!;

        Assert.Equal(["File", "Edit", "Insert", "Arrange", "View", "Run"], menu.Items.OfType<NativeMenuItem>().Select(item => item.Header));
        var edit = menu.Items.OfType<NativeMenuItem>().Single(item => item.Header == "Edit").Menu!;
        var duplicate = edit.Items.OfType<NativeMenuItem>().Single(item => item.Header == "Duplicate");
        Assert.Equal(new KeyGesture(Key.D, KeyModifiers.Control), duplicate.Gesture);
        Assert.True(duplicate.IsEnabled);

        window.Select(null);
        edit = NativeMenu.GetMenu(window)!.Items.OfType<NativeMenuItem>().Single(item => item.Header == "Edit").Menu!;
        Assert.False(edit.Items.OfType<NativeMenuItem>().Single(item => item.Header == "Duplicate").IsEnabled);
        window.Close();
    });

    [Fact]
    public void RunningAMenuCommandDoesWhatItSays() => TestApp.Run(() =>
    {
        var (window, equals) = OpenCalculator();
        window.Select(equals);

        Command(window, "Edit", "Duplicate").Run();
        Assert.Contains("equals2", window.Text);

        Command(window, "Edit", "Undo").Run();
        Assert.DoesNotContain("equals2", window.Text);
        Command(window, "Edit", "Redo").Run();
        Assert.Contains("equals2", window.Text);

        Command(window, "View", "Dark").Run();
        Assert.True(window.IsDark);
        Assert.True(Command(window, "View", "Dark").IsChecked!());

        Command(window, "View", "Teal").Run();
        Assert.Equal(1, window.Accent);
        Assert.True(Command(window, "View", "Teal").IsChecked!());

        Command(window, "Insert", "Slider").Run();
        Assert.Contains("<Slider name=\"slider\"", window.Text);
        window.Close();
    });

    [Fact]
    public void TheMenuButtonOpensTheSameMenus() => TestApp.Run(() =>
    {
        var (window, _) = OpenCalculator();

        var flyout = Menus.AppMenu.Flyout(window.Menus);

        Assert.Equal(["File", "Edit", "Insert", "Arrange", "View", "Run"], flyout.Items.OfType<MenuItem>().Select(item => (string)item.Header!));
        Assert.True(window.FindControl<Button>("MenuButton")!.IsVisible);
        window.Close();
    });

    [Fact]
    public void UndoAndRedoInTheBarFollowTheEditor() => TestApp.Run(() =>
    {
        var (window, equals) = OpenCalculator();
        var undo = window.FindControl<Button>("UndoButton")!;
        Assert.False(undo.IsEnabled);

        window.Select(equals);
        window.DuplicateSelection();

        Assert.True(undo.IsEnabled);
        window.Undo();
        Assert.True(window.FindControl<Button>("RedoButton")!.IsEnabled);
        window.Close();
    });

    [Fact]
    public void TheStatusBarShowsWhereTheCaretIs() => TestApp.Run(() =>
    {
        var (window, _) = OpenCalculator();
        var editor = window.FindControl<AvaloniaEdit.TextEditor>("Editor")!;

        editor.TextArea.Caret.Line = 3;
        editor.TextArea.Caret.Column = 5;

        Assert.Equal("Ln 3, Col 5", window.FindControl<TextBlock>("CaretText")!.Text);
        window.Close();
    });

    [Fact]
    public void TheBarShowsTheDocumentAndItsFolderOnOneLine() => TestApp.Run(() =>
    {
        var (window, _) = OpenCalculator();

        // The test's copy sits in a plain folder, so its folder follows the name.
        Assert.StartsWith("main.uix — roundhouse-window-", window.FindControl<TextBlock>("DocumentTitle")!.Text);
        window.Close();
    });

    [Fact]
    public void RunOpensVentureOSWithTheEditedAppOnceAndItsCopyGoesWhenItCloses() => TestApp.Run(() =>
    {
        var (window, equals) = OpenCalculator();
        window.Apply(window.Document!.SetAttribute(equals, "text", "Go"));

        Command(window, "Run", "Run in Simulation").Run();
        var simulator = window.Simulator!;
        window.Run();

        Assert.Same(simulator, window.Simulator);
        var run = simulator.Run!;
        Assert.Contains(run.Apps.Running, app => app.Bundle.Name == "Files");
        var root = run.Disk.Root;

        // The test's copy of Calculator's document isn't in an app, so it runs as a window of its own, as edited.
        Assert.Contains(run.Session.Windows.Windows, shown => shown.Title == "main.uix");
        Assert.True(Command(window, "Run", "Stop").IsEnabled!());

        Command(window, "Run", "Stop").Run();
        Assert.Null(window.Simulator);
        Assert.False(Directory.Exists(root));
        window.Close();
    });

    [Fact]
    public void RestartStartsAFreshRunAndClosingTheDesignerEndsIt() => TestApp.Run(() =>
    {
        var (window, _) = OpenCalculator();
        window.Run();
        var first = window.Simulator!;
        var firstRoot = first.Run!.Disk.Root;

        window.RestartRun();

        Assert.NotSame(first, window.Simulator);
        Assert.False(Directory.Exists(firstRoot));
        var secondRoot = window.Simulator!.Run!.Disk.Root;
        window.Close();
        Assert.False(Directory.Exists(secondRoot));
    });

    [Fact]
    public void TheSimulatorPassesThePointerAndKeysToVentureOS() => TestApp.Run(() =>
    {
        var (window, _) = OpenCalculator();
        window.Run();
        var simulator = window.Simulator!;
        var run = simulator.Run!;

        // Click VentureOS's own menu, then Escape closes it again.
        simulator.PointAt(12, 10, down: true);
        simulator.Frame();
        simulator.PointAt(12, 10, down: false);
        simulator.Frame();
        Assert.True(run.Session.MenuBar.IsOpen);

        simulator.Press(new Venture.OS.Desktop.KeyPress(Venture.OS.Desktop.DesktopKey.Escape));
        simulator.Frame();
        Assert.False(run.Session.MenuBar.IsOpen);
        window.Close();
    });

    [Fact]
    public void WindowButtonsAreVentureOSsDotsWhereRoundhouseDrawsTheFrame() => TestApp.Run(() =>
    {
        var (window, _) = OpenCalculator();
        var buttons = window.FindControl<StackPanel>("WindowButtons")!;

        Assert.Equal(window.DrawsWindowButtons, buttons.IsVisible);
        var palette = Venture.OS.Desktop.DesktopPalette.Light;
        var close = (Avalonia.Media.SolidColorBrush)window.FindControl<Avalonia.Controls.Shapes.Ellipse>("CloseDot")!.Fill!;
        Assert.Equal((palette.CloseButton.Red, palette.CloseButton.Green, palette.CloseButton.Blue), (close.Color.R, close.Color.G, close.Color.B));
        Assert.Equal(["CloseButton", "MinimizeButton", "MaximizeButton"], buttons.Children.Select(child => child.Name));

        // VentureOS's order: the window's buttons first, at the left end, then the start items.
        var start = window.FindControl<StackPanel>("StartItems")!;
        Assert.Same(buttons, start.Children[0]);
        window.Close();
    });

    [Fact]
    public void TheNameAndBundleAreTallEnoughForTheirGlyphsAndFitTheBar() => TestApp.Run(() =>
    {
        var (window, _) = OpenCalculator();
        window.UpdateLayout();

        // A text block clips to its bounds, so each must be at least as tall as its font's line, or glyphs are cut.
        foreach (var name in (string[])["DocumentTitle"])
        {
            var text = window.FindControl<TextBlock>(name)!;
            Assert.True(double.IsNaN(text.LineHeight), $"{name} sets a line height, which can cut its glyphs.");
            Assert.True(text.Bounds.Height >= text.FontSize * 1.2, $"{name} is {text.Bounds.Height} tall at {text.FontSize} pt.");
        }

        var stack = window.FindControl<StackPanel>("TitleStack")!;
        Assert.True(stack.Bounds.Top >= 0 && stack.Bounds.Bottom <= window.FindControl<Border>("TitleBar")!.Bounds.Height);
        window.Close();
    });

    [Fact]
    public void DraggingTheWindowsCornerResizesTheWindow() => TestApp.Run(() =>
    {
        var (window, _) = OpenCalculator();
        var content = window.Map!.Content;
        Click(window, content.X + 2, content.Y + content.Height - 2);
        Assert.Equal(0, window.Selection);
        Assert.Equal(4, Layer(window).Children.Count);

        window.MouseDown(At(window, content.X + content.Width, content.Y + content.Height), MouseButton.Left);
        window.MouseMove(At(window, content.X + content.Width + 20, content.Y + content.Height + 10));
        window.MouseUp(At(window, content.X + content.Width + 20, content.Y + content.Height + 10), MouseButton.Left);

        Assert.Contains("""<Window width="252" height="318"/>""", window.Text);
        window.Close();
    });

    [Fact]
    public void TheInsertMenuOffersTheWindowsPartsAndAMenuItemJoinsTheSelectedMenu() => TestApp.Run(() =>
    {
        var (window, _) = OpenCalculator();

        Command(window, "Insert", "Menu").Run();
        var menu = window.Selection!.Value;
        Command(window, "Insert", "Menu Item").Run();

        Assert.Equal(menu + 1, window.Selection);
        Assert.Contains("requires=\"menus\"", window.Text);
        Assert.Contains("""<MenuItem menu="menu" name="item" title="Item"/>""", window.Text);
        Assert.Equal("MenuItem  item", ((string[])window.FindControl<ListBox>("OutlineList")!.ItemsSource!)[menu + 1]);
        window.Close();
    });

    [Fact]
    public void RunOnVentureOSOpensAWindowThatSaysWhyItStopped() => TestApp.Run(() =>
    {
        var (window, _) = OpenCalculatorBundle();
        window.VentureOSRunner = () => new Vdb.VentureOSRun(
            _ => throw new Venture.OS.Vdb.VdbClientException(Venture.OS.Vdb.VdbClientProblem.Connection, "Nothing answered here."),
            () => "0123456789abcdef0123456789abcdef");

        Assert.True(Command(window, "Run", "Run on VentureOS").IsEnabled!());
        Command(window, "Run", "Run on VentureOS").Run();
        var run = window.VentureOSRun!;
        Assert.True(run.Running.Wait(TimeSpan.FromSeconds(10)));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Calculator.app on VentureOS", run.Title);
        Assert.Equal("Nothing answered here.", run.Status);
        run.Close();
        Assert.Null(window.VentureOSRun);
        window.Close();
    });

    [Fact]
    public void RunOnVentureOSNeedsAnApp() => TestApp.Run(() =>
    {
        var (window, _) = OpenCalculator();

        Assert.False(Command(window, "Run", "Run on VentureOS").IsEnabled!());
        Assert.False(window.RunOnVentureOS());
        Assert.Null(window.VentureOSRun);
        window.Close();
    });

    private static Menus.MenuCommand Command(MainWindow window, string menu, string title) =>
        Menus.AppMenu.Find(window.Menus, menu, title) ?? throw new InvalidOperationException($"No {menu} ▸ {title}.");

    private static Designer.InspectorPanel Inspector(MainWindow window) => window.FindControl<Designer.InspectorPanel>("Inspector")!;

    [Fact]
    public void ADocumentWithMenusShowsThemAboveTheWindowAndOneWithoutDoesnt() => TestApp.Run(() =>
    {
        var (window, _) = OpenCalculator();
        Assert.Null(window.MenusShown);

        var files = OpenFiles(window);
        var bar = window.MenusShown!;

        // The copy isn't in an app, so the app's menu takes the document's name; in Files.app, it'd say Files.
        Assert.Equal(["files", "File", "Go"], bar.Menus.Select(menu => menu.Menu.Title));
        Assert.True(bar.Menus[0].Emphasized);
        Assert.True(window.Map!.Content.Y > Venture.OS.Desktop.MenuBar.Height);
        Assert.NotNull(files);
        window.Close();
    });

    [Fact]
    public void ClickingAMenuSelectsItAndClickingAnItemSelectsTheItem() => TestApp.Run(() =>
    {
        var (window, _) = OpenCalculator();
        var children = OpenFiles(window);
        var bar = window.MenusShown!;
        var file = bar.TitleBounds(1)!.Value;

        window.MouseDown(At(window, file.X + 4, 10), MouseButton.Left);
        window.MouseUp(At(window, file.X + 4, 10), MouseButton.Left);

        Assert.True(bar.IsOpen);
        Assert.Equal("fileMenu", children[window.Selection!.Value]["name"]);

        // The second entry is Quick Look.
        var look = bar.EntryBounds(1)!.Value;
        Click(window, look.X + 10, look.Y + (look.Height / 2));

        Assert.False(bar.IsOpen);
        Assert.Equal("lookItem", children[window.Selection!.Value]["name"]);
        window.Close();
    });

    [Fact]
    public void EscapeClosesAnOpenMenuAndAClickElsewhereClosesItAndSelectsThere() => TestApp.Run(() =>
    {
        var (window, _) = OpenCalculator();
        OpenFiles(window);
        var bar = window.MenusShown!;
        var go = bar.TitleBounds(2)!.Value;

        Click(window, go.X + 4, 10);
        Assert.True(bar.IsOpen);
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Assert.False(bar.IsOpen);

        Click(window, go.X + 4, 10);
        var content = window.Map!.Content;
        Click(window, content.X + content.Width - 4, content.Y + content.Height - 4);
        Assert.False(bar.IsOpen);
        Assert.Equal(window.Map.Window, window.Selection);
        window.Close();
    });

    [Fact]
    public void OpeningABundleListsItsFilesAndShowsItsInterface() => TestApp.Run(() =>
    {
        var (window, app) = OpenCalculatorBundle();

        Assert.True(window.FindControl<DockPanel>("FilesPane")!.IsVisible);
        Assert.Equal("Calculator.app", window.Bundle!.Name);
        Assert.Equal(Path.Combine(app, "Contents", "Resources", "main.uix"), window.CurrentFile);
        Assert.NotNull(window.Document);
        Assert.Equal("main.uix — Calculator.app", window.FindControl<TextBlock>("DocumentTitle")!.Text);
        window.Close();
    });

    [Fact]
    public void AnotherFileShowsInItsOwnEditorAndComingBackKeepsTheEdit() => TestApp.Run(() =>
    {
        var (window, app) = OpenCalculatorBundle();
        var main = window.CurrentFile!;
        var edited = window.Text.Replace("text=\"=\"", "text=\"Go\"", StringComparison.Ordinal);
        window.Text = edited;

        Select(window, Path.Combine(app, "Contents", "Lua", "main.lua"));

        Assert.True(window.FindControl<AvaloniaEdit.TextEditor>("FileEditor")!.IsVisible);
        Assert.False(window.FindControl<ScrollViewer>("CanvasScroller")!.IsVisible);
        Assert.Null(window.Document);
        Assert.True(window.Bundle!.IsUnsaved(main));

        Select(window, main);

        Assert.Equal(edited, window.Text);
        Assert.True(window.Save());
        Assert.False(window.Bundle.IsUnsaved(main));
        Assert.Contains("text=\"Go\"", File.ReadAllText(main));
        window.Close();
    });

    [Fact]
    public void ADocumentOutsideABundleHidesTheFiles() => TestApp.Run(() =>
    {
        var (window, _) = OpenCalculatorBundle();

        window.Open(document);

        Assert.Null(window.Bundle);
        Assert.False(window.FindControl<DockPanel>("FilesPane")!.IsVisible);
        window.Close();
    });

    [Fact]
    public void AFolderThatIsntABundleIsRefused() => TestApp.Run(() =>
    {
        MainWindow window = new();
        window.Show();

        window.Open(folder);

        Assert.Null(window.Bundle);
        Assert.Contains("isn't a bundle", window.Status);
        window.Close();
    });

    [Fact]
    public void RunFromAnotherFileOfTheBundleRunsTheApp() => TestApp.Run(() =>
    {
        var (window, app) = OpenCalculatorBundle();
        Select(window, Path.Combine(app, "Contents", "Info.xml"));

        window.Run();

        Assert.Contains(window.Simulator!.Run!.Apps.Running, running => running.Bundle.Name == "Calculator");
        window.Close();
    });

    [Fact]
    public void LuaOpensInTheFileEditorAndAnUnsavedEditIsMarkedUntilSaved() => TestApp.Run(() =>
    {
        var (window, app) = OpenCalculatorBundle();
        var lua = Path.Combine(app, "Contents", "Lua", "main.lua");
        Select(window, lua);
        var editor = window.FindControl<AvaloniaEdit.TextEditor>("FileEditor")!;

        Assert.Equal(File.ReadAllText(lua), editor.Text);
        Assert.Equal("main.lua — Calculator.app", window.FindControl<TextBlock>("DocumentTitle")!.Text);

        editor.Document.Insert(0, "-- edited\n");

        Assert.True(window.Bundle!.IsUnsaved(lua));
        Assert.Contains("main.lua ●", Labels(window));
        Assert.True(window.FindControl<Button>("UndoButton")!.IsEnabled);

        Assert.True(window.Save());
        Assert.StartsWith("-- edited\n", File.ReadAllText(lua));
        Assert.DoesNotContain("main.lua ●", Labels(window));
        Assert.Contains("main.lua", Labels(window));
        window.Close();
    });

    [Fact]
    public void ATableSaysWhatsWrongWithItAndCatchesUpAfterTyping() => TestApp.Run(() =>
    {
        var (window, app) = OpenCalculatorBundle(app =>
            File.WriteAllText(Path.Combine(app, "Contents", "Resources", "_1_calculator.cst"), "1 ^one^\n1 ^again^\n"));
        var table = Path.Combine(app, "Contents", "Resources", "_1_calculator.cst");
        Select(window, table);
        var editor = window.FindControl<AvaloniaEdit.TextEditor>("FileEditor")!;
        Dispatcher.UIThread.RunJobs();

        Assert.True(editor.IsVisible);
        Assert.Contains("the key \"1\" is used again", window.Status);
        Assert.Contains("_1_calculator.cst, line 2", window.FindControl<AvaloniaEdit.TextEditor>("LogView")!.Text);

        // Fixed by typing: the check waits for a pause, as the canvas's redraw does.
        editor.Document.Replace(0, editor.Document.TextLength, "1 ^one^\n2 ^two^\n");
        Pause();

        Assert.Equal("VentureOS reads this as table 1.", window.Status);
        window.Close();
    });

    [Fact]
    public void ATableNamedSoNoDocumentFindsItSaysSo() => TestApp.Run(() =>
    {
        var (window, app) = OpenCalculatorBundle(app =>
            File.WriteAllText(Path.Combine(app, "Contents", "Resources", "strings.cst"), "1 ^one^\n"));
        Select(window, Path.Combine(app, "Contents", "Resources", "strings.cst"));

        Assert.Contains("isn't named like a table", window.Status);
        window.Close();
    });

    [Fact]
    public void TheDesignerDrawsATablesUnsavedText() => TestApp.Run(() =>
    {
        var (window, app) = OpenCalculatorBundle(app =>
        {
            var resources = Path.Combine(app, "Contents", "Resources");
            File.WriteAllText(Path.Combine(resources, "_1_calculator.cst"), "1 ^one^\n");
            File.WriteAllText(Path.Combine(resources, "main.uix"), """
                <UIScript version="1">
                  <Window width="200" height="80"/>
                  <String name="two" table="1" index="2"/>
                  <Text name="label" x="10" y="10" width="180" height="20" string="two"/>
                </UIScript>
                """);
        });
        var table = Path.Combine(app, "Contents", "Resources", "_1_calculator.cst");
        var document = Path.Combine(app, "Contents", "Resources", "main.uix");
        Select(window, document);
        Assert.Contains("no text for the string \"two\"", window.Status);

        // Edited, not saved: the designer reads the table as the bundle has it.
        Select(window, table);
        window.FindControl<AvaloniaEdit.TextEditor>("FileEditor")!.Document.Insert(0, "2 ^two^\n");
        Select(window, document);

        Assert.Equal("VentureOS reads this document.", window.Status);
        Assert.False(window.Bundle!.IsUnsaved(document));
        Assert.True(window.Bundle.IsUnsaved(table));
        window.Close();
    });

    [Fact]
    public void UndoInTheFileEditorUndoesTyping() => TestApp.Run(() =>
    {
        var (window, app) = OpenCalculatorBundle();
        var lua = Path.Combine(app, "Contents", "Lua", "main.lua");
        Select(window, lua);
        var editor = window.FindControl<AvaloniaEdit.TextEditor>("FileEditor")!;
        var before = editor.Text;

        editor.Document.Insert(0, "-- edited\n");
        window.Undo();

        Assert.Equal(before, editor.Text);
        Assert.False(window.Bundle!.IsUnsaved(lua));
        window.Close();
    });

    [Fact]
    public void SaveAllSavesEveryEditedFileTheDesignersToo() => TestApp.Run(() =>
    {
        var (window, app) = OpenCalculatorBundle();
        var main = window.CurrentFile!;
        var lua = Path.Combine(app, "Contents", "Lua", "main.lua");
        window.Text = window.Text.Replace("text=\"=\"", "text=\"Go\"", StringComparison.Ordinal);
        Select(window, lua);
        window.FindControl<AvaloniaEdit.TextEditor>("FileEditor")!.Document.Insert(0, "-- edited\n");

        Assert.True(window.SaveAll());

        Assert.Contains("text=\"Go\"", File.ReadAllText(main));
        Assert.StartsWith("-- edited\n", File.ReadAllText(lua));
        Assert.Empty(window.Bundle!.Unsaved);
        Assert.Equal("Saved 2 files.", window.Status);
        window.Close();
    });

    [Fact]
    public void RunRunsEveryUnsavedFile() => TestApp.Run(() =>
    {
        var (window, app) = OpenCalculatorBundle();
        var lua = Path.Combine(app, "Contents", "Lua", "main.lua");
        Select(window, lua);
        window.FindControl<AvaloniaEdit.TextEditor>("FileEditor")!.Document.Insert(0, "-- edited in Roundhouse\n");

        window.Run();

        var run = window.Simulator!.Run!;
        Assert.StartsWith("-- edited in Roundhouse", File.ReadAllText(run.Disk.HostPath("/mnt/user/apps/Calculator.app/Contents/Lua/main.lua")));
        Assert.DoesNotContain("edited in Roundhouse", File.ReadAllText(lua));
        window.Close();
    });

    [Fact]
    public void APictureIsShownAndAFileThatIsntTextIsSaidSo() => TestApp.Run(() =>
    {
        var (window, app) = OpenCalculatorBundle();
        var icon = Path.Combine(app, "Contents", "Resources", "Icon.png");
        var data = Path.Combine(app, "Contents", "Resources", "data.bin");
        File.Copy(Path.Combine(SkiaPainterTests.RepositoryRoot(), "rootfs", "system", "icons", "VentureOS.png"), icon);
        File.WriteAllBytes(data, [0xFF, 0xFE, 0xC3, 0x28]);
        window.Open(app);

        Select(window, icon);
        Assert.True(window.FindControl<ScrollViewer>("ImageView")!.IsVisible);
        Assert.NotNull(window.FindControl<Image>("FileImage")!.Source);

        Select(window, data);
        var placeholder = window.FindControl<TextBlock>("FilePlaceholder")!;
        Assert.True(placeholder.IsVisible);
        Assert.Contains("isn't text", placeholder.Text);
        window.Close();
    });

    [Fact]
    public void InfoXmlOpensAsAFormThatEditsTheTextInPlace() => TestApp.Run(() =>
    {
        var (window, app) = OpenCalculatorBundle();
        var info = Path.Combine(app, "Contents", "Info.xml");
        var saved = File.ReadAllText(info);
        Select(window, info);
        var form = window.FindControl<Bundles.InfoForm>("InfoForm")!;

        Assert.True(window.FindControl<ScrollViewer>("InfoView")!.IsVisible);
        var name = (TextBox)form.Fields["name"];
        Assert.Equal("Calculator", name.Text);

        window.UpdateLayout();
        Assert.True(name.Focus());
        name.Text = "Adding Machine";
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);

        Assert.Equal(saved.Replace("name=\"Calculator\"", "name=\"Adding Machine\"", StringComparison.Ordinal), window.Bundle!.Text(info));
        Assert.True(window.Bundle.IsUnsaved(info));
        Assert.Contains("Info.xml ●", Labels(window));

        // The form is updated, not built again, so the field typed in keeps its place for Tab.
        Assert.Same(name, form.Fields["name"]);

        Assert.True(window.Save());
        Assert.Contains("name=\"Adding Machine\"", File.ReadAllText(info));
        window.Close();
    });

    [Fact]
    public void TheFormsNeedsAreCheckBoxesAndARefusedValueSaysWhyUnderItsField() => TestApp.Run(() =>
    {
        var (window, app) = OpenCalculatorBundle();
        var info = Path.Combine(app, "Contents", "Info.xml");
        Select(window, info);
        var form = window.FindControl<Bundles.InfoForm>("InfoForm")!;

        ((CheckBox)window.FindControl<Bundles.InfoForm>("InfoForm")!.Fields["needs.files"]).IsChecked = true;
        Assert.Equal(Venture.OS.Scripting.ScriptScopes.Files, window.Info!.Bundle!.Needs & Venture.OS.Scripting.ScriptScopes.Files);
        Assert.Contains("files", window.Bundle!.Text(info));

        var identifier = (TextBox)form.Fields["identifier"];
        window.UpdateLayout();
        Assert.True(identifier.Focus());
        identifier.Text = "not an identifier";
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);

        Assert.Contains("reverse-DNS", form.Problem("identifier"));
        Assert.DoesNotContain("not an identifier", window.Bundle.Text(info));
        window.Close();
    });

    [Fact]
    public void TheMainScriptIsChosenFromTheBundlesLuaFiles() => TestApp.Run(() =>
    {
        var (window, app) = OpenCalculatorBundle();
        File.WriteAllText(Path.Combine(app, "Contents", "Lua", "other.lua"), "print('other')");
        window.Open(app);
        var info = Path.Combine(app, "Contents", "Info.xml");
        Select(window, info);
        var main = (ComboBox)window.FindControl<Bundles.InfoForm>("InfoForm")!.Fields["main"];

        Assert.Contains("Contents/Lua/other.lua", (IEnumerable<string>)main.ItemsSource!);
        main.SelectedItem = "Contents/Lua/other.lua";

        Assert.Contains("main=\"Contents/Lua/other.lua\"", window.Bundle!.Text(info));
        window.Close();
    });

    [Fact]
    public void AnInfoXmlVentureOSRefusesShowsWhyOverItsTextAndTheFormComesBackOnceItsFixed() => TestApp.Run(() =>
    {
        var (window, app) = OpenCalculatorBundle();
        var info = Path.Combine(app, "Contents", "Info.xml");
        var good = File.ReadAllText(info);
        File.WriteAllText(info, "<App version=\"1\"><Info name=\"Calculator\"/></App>");

        Select(window, info);
        var form = window.FindControl<Bundles.InfoForm>("InfoForm")!;

        Assert.True(window.FindControl<ScrollViewer>("InfoView")!.IsVisible);
        Assert.Contains("identifier", form.Refusal);
        Assert.Equal("<App version=\"1\"><Info name=\"Calculator\"/></App>", window.Text);

        window.Text = good;
        window.RenderNow();

        Assert.Null(form.Refusal);
        Assert.Equal("Calculator", ((TextBox)form.Fields["name"]).Text);
        Assert.True(window.Bundle!.IsUnsaved(info));
        window.Close();
    });

    [Fact]
    public void InfoXmlsTextIsInTheTextTabAndTheFormAndTextFollowEachOther() => TestApp.Run(() =>
    {
        var (window, app) = OpenCalculatorBundle();
        var info = Path.Combine(app, "Contents", "Info.xml");
        Select(window, info);
        var form = window.FindControl<Bundles.InfoForm>("InfoForm")!;
        var saved = File.ReadAllText(info);

        // The Text tab holds Info.xml's text, as it holds an interface's.
        Assert.Equal(saved, window.Text);
        Assert.True(window.FindControl<TabItem>("TextTab")!.IsVisible);

        // Typing in the text changes the form, once the typing stops.
        window.Text = saved.Replace("name=\"Calculator\"", "name=\"Adder\"", StringComparison.Ordinal);
        window.RenderNow();
        Assert.Equal("Adder", ((TextBox)form.Fields["name"]).Text);

        // A change in the form goes into the text, where Undo takes it back.
        ((CheckBox)form.Fields["needs.files"]).IsChecked = true;
        Assert.Contains("files", window.Text);
        window.Undo();
        Assert.DoesNotContain("files", window.Text);
        Assert.False(((CheckBox)form.Fields["needs.files"]).IsChecked);
        window.Close();
    });

    [Fact]
    public void TheLogTabShowsWhatsLoggedAndTakesTheTextTabsPlaceForOtherFiles() => TestApp.Run(() =>
    {
        var (window, app) = OpenCalculatorBundle();
        var logView = window.FindControl<AvaloniaEdit.TextEditor>("LogView")!;
        var tabs = window.FindControl<TabControl>("BottomTabs")!;

        Assert.Same(window.FindControl<TabItem>("TextTab"), tabs.SelectedItem);

        // A line no test has logged before, so it can only be there if the tab follows the log.
        var unique = $"followed {Guid.NewGuid():N}";
        Logging.RoundhouseLog.For("Test").Info(unique);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(unique, logView.Text);
        Assert.Contains(Logging.RoundhouseLog.Buffer.Lines, line => line.Contains("Opened " + app, StringComparison.Ordinal));

        Select(window, Path.Combine(app, "Contents", "Lua", "main.lua"));
        Assert.False(window.FindControl<TabItem>("TextTab")!.IsVisible);
        Assert.Same(window.FindControl<TabItem>("LogTab"), tabs.SelectedItem);

        Select(window, Path.Combine(app, "Contents", "Resources", "main.uix"));
        Assert.Same(window.FindControl<TabItem>("TextTab"), tabs.SelectedItem);
        window.Close();
    });

    [Fact]
    public void AnErrorNothingCaughtIsLoggedAndReported() => TestApp.Run(() =>
    {
        var (window, _) = OpenCalculator();

        window.ReportError(new InvalidOperationException("a test's own failure"));

        Assert.Contains("a test's own failure", window.Status);
        Assert.Contains("Log tab", window.Status);
        Assert.Contains(Logging.RoundhouseLog.Buffer.Lines, line => line.Contains("ERROR", StringComparison.Ordinal) && line.Contains("a test's own failure", StringComparison.Ordinal));
        window.Close();
    });

    [Fact]
    public void ANewAppOpensInTheDesignerWithItsFiles() => TestApp.Run(() =>
    {
        MainWindow window = new();
        window.Show();

        var project = window.CreateApp("Counter", "dev.example.counter", folder);

        // A new app is a project with the app in it, so no .app sits in the folder.
        Assert.Equal("Counter.rproj", window.Bundle!.Name);
        Assert.Equal("Counter", window.Project!.Name);
        Assert.Equal(Path.Combine(project, "App", "Resources", "main.uix"), window.CurrentFile);
        Assert.NotNull(window.Document!.Script!.Control("press"));
        Assert.False(Directory.Exists(Path.Combine(folder, "Counter.app")));
        Assert.Equal("main.uix — Counter.rproj", window.FindControl<TextBlock>("DocumentTitle")!.Text);
        Assert.True(Command(window, "File", "New App…").IsEnabled?.Invoke() ?? true);
        window.Close();
    });

    [Fact]
    public void TheNewAppDialogFollowsTheNameUntilTheIdentifierIsChangedAndSaysWhatsWrong() => TestApp.Run(() =>
    {
        Bundles.NewAppWindow dialog = new(folder);
        dialog.Show();

        dialog.AppName = "Dice Roller";
        Assert.Equal("dev.example.diceroller", dialog.Identifier);
        dialog.Identifier = "dev.mine.dice";
        dialog.AppName = "Dice";
        Assert.Equal("dev.mine.dice", dialog.Identifier);

        dialog.AppName = "a/b";
        Assert.Null(dialog.Create());
        Assert.Contains("can't have", dialog.Problem);

        dialog.AppName = "Dice";
        Assert.Equal(Path.Combine(folder, "Dice.rproj"), dialog.Create());
        var app = Projects.RoundhouseProject.Open(Path.Combine(folder, "Dice.rproj")).Assemble(Path.Combine(folder, "out"));
        Assert.Equal("dev.mine.dice", Venture.OS.Bundles.Bundle.Read(app.Replace('\\', '/'), Venture.OS.Bundles.BundleKind.App).Identifier);
    });

    [Fact]
    public void ANewProjectIsEmptyAndItsAppIsAddedOnce() => TestApp.Run(() =>
    {
        Bundles.NewAppWindow empty = new(folder, withApp: false);
        empty.Show();
        empty.AppName = "Dialogs";
        var root = empty.Create()!;
        Assert.Equal(["Project.xml"], Directory.GetFileSystemEntries(root).Select(Path.GetFileName));

        MainWindow window = new() { AskToLeave = DontSave };
        window.Show();
        window.Open(root);
        Assert.False(window.Project!.HasApp);
        Assert.False(Command(window, "File", "Export…").IsEnabled!());
        Assert.False(Command(window, "Run", "Run on VentureOS").IsEnabled!());

        Bundles.NewAppWindow add = new(window.Project);
        add.Show();
        add.AppName = "Dialogs";
        Assert.Equal(root, add.Create());
        window.Open(root);

        Assert.True(window.Project.HasApp);
        Assert.Equal(Path.Combine(root, "App", "Resources", "main.uix"), window.CurrentFile);
        Assert.True(Command(window, "File", "Export…").IsEnabled!());

        // A project holds one app.
        Bundles.NewAppWindow again = new(window.Project);
        again.Show();
        again.AppName = "Second";
        Assert.Null(again.Create());
        Assert.Contains("already has its app", again.Problem);
        window.Close();
    });

    [Fact]
    public void AProjectRunsItsAppAssembledWithItsEditsAndOpensAtTheWindowBeingDesigned() => TestApp.Run(() =>
    {
        var (window, project) = OpenCounterProject();
        window.Text = window.Text.Replace("text=\"Press\"", "text=\"Go\"", StringComparison.Ordinal);

        window.Run();
        var run = window.Simulator!.Run!;

        // It runs as a user app, from an .app made from the project in a folder of Roundhouse's own, never the project's.
        var running = Assert.Single(run.Apps.Running, app => app.Bundle.Name == "Counter");
        Assert.Contains("\"Go\"", File.ReadAllText(run.Disk.HostPath("/mnt/user/apps/Counter.app/Contents/Resources/main.uix")));
        Assert.DoesNotContain(Directory.GetFileSystemEntries(project), entry => entry.EndsWith(".app", StringComparison.Ordinal));
        Assert.NotNull(running);
        Assert.True(Directory.Exists(window.RunCopies));
        window.StopRun();
        window.Close();
        Assert.False(Directory.Exists(window.RunCopies));
    });

    [Fact]
    public void ALooseInterfaceInAProjectRunsAsAWindowOfItsOwn() => TestApp.Run(() =>
    {
        var (window, project) = OpenCounterProject();
        var loose = Path.Combine(project, "Interfaces", "open.uix");
        Directory.CreateDirectory(Path.GetDirectoryName(loose)!);
        File.WriteAllText(loose, "<UIScript version=\"1\"><Window width=\"200\" height=\"100\"/></UIScript>");
        window.Open(loose);

        window.Run();

        Assert.Contains(window.Simulator!.Run!.Session.Windows.Windows, shown => shown.Title == "open.uix");
        window.StopRun();
        window.Close();
    });

    [Fact]
    public void RunOnVentureOSSendsTheProjectsAppAssembled() => TestApp.Run(() =>
    {
        var (window, project) = OpenCounterProject();
        window.VentureOSRunner = () => new Vdb.VentureOSRun(
            _ => throw new Venture.OS.Vdb.VdbClientException(Venture.OS.Vdb.VdbClientProblem.Connection, "Nothing answered here."),
            () => "0123456789abcdef0123456789abcdef");

        Assert.True(Command(window, "Run", "Run on VentureOS").IsEnabled!());
        Assert.True(window.RunOnVentureOS());
        var run = window.VentureOSRun!;
        Assert.True(run.Running.Wait(TimeSpan.FromSeconds(10)));
        Dispatcher.UIThread.RunJobs();

        // What's sent is the app assembled from the project, named as its Info.xml names it, and not made in the project.
        Assert.Equal("Counter.app on VentureOS", run.Title);
        Assert.DoesNotContain(Directory.GetFileSystemEntries(project), entry => entry.EndsWith(".app", StringComparison.Ordinal));
        run.Close();
        window.Close();
    });

    [Fact]
    public void ExportWritesTheAppAndAsksBeforeReplacingOne() => TestApp.Run(() =>
    {
        var (window, _) = OpenCounterProject();
        var apps = Path.Combine(folder, "apps");
        Directory.CreateDirectory(apps);
        window.Text = window.Text.Replace("text=\"Press\"", "text=\"Go\"", StringComparison.Ordinal);
        var asked = 0;
        window.AskToReplace = _ =>
        {
            asked++;
            return Task.FromResult(asked > 1);
        };

        var app = window.ExportTo(apps).GetAwaiter().GetResult();
        Assert.Equal(Path.Combine(apps, "Counter.app"), app);
        Assert.Contains("\"Go\"", File.ReadAllText(Path.Combine(app!, "Contents", "Resources", "main.uix")));
        Assert.Equal(0, asked);

        Assert.Null(window.ExportTo(apps).GetAwaiter().GetResult());
        Assert.Equal(app, window.ExportTo(apps).GetAwaiter().GetResult());
        Assert.Equal(2, asked);
        window.Close();
    });

    [Fact]
    public void AStrayAppIsOfferedAsAProjectAndOneInRootfsIsnt() => TestApp.Run(() =>
    {
        var stray = Bundles.AppTemplate.Create("Dice", "dev.example.dice", folder);
        MainWindow window = new() { AskToLeave = DontSave, AskToMakeProject = _ => Task.FromResult(true) };
        window.Show();

        Assert.True(window.OpenAsync(stray).GetAwaiter().GetResult());

        Assert.Equal("Dice.rproj", window.Bundle!.Name);
        Assert.True(File.Exists(Path.Combine(folder, "Dice.rproj", "App", "Info.xml")));
        Assert.True(Directory.Exists(stray));
        Assert.False(MainWindow.IsStrayApp(Path.Combine(SkiaPainterTests.RepositoryRoot(), "rootfs", "user", "apps", "Calculator.app")));
        Assert.Contains(window.Recent, item => item.Path == Path.Combine(folder, "Dice.rproj"));
        window.Close();
    });

    [Fact]
    public void AProjectRoundhouseCantReadLeavesWhatsOpen() => TestApp.Run(() =>
    {
        var (window, project) = OpenCounterProject();
        var newer = Path.Combine(folder, "Newer.rproj");
        Directory.CreateDirectory(newer);
        File.WriteAllText(Path.Combine(newer, "Project.xml"), "<Project version=\"9\"><Info name=\"Newer\"/></Project>");

        window.Open(newer);

        Assert.Equal(project, window.Bundle!.Root);
        Assert.Contains("needs a newer Roundhouse", window.Status);
        window.Close();
    });

    [Fact]
    public void TheRunButtonOffersBothPlacesToRun() => TestApp.Run(() =>
    {
        var (window, _) = OpenCounterProject();
        var button = window.FindControl<Button>("RunButton")!;

        Assert.Equal("▶ Run ▾", button.Content);
        Assert.Equal(["Run in Simulation", "Run on VentureOS"],
            Menus.AppMenu.Find(window.Menus, "Run", "Run in Simulation") is { } simulation && Menus.AppMenu.Find(window.Menus, "Run", "Run on VentureOS") is { } venture
                ? [simulation.Title, venture.Title] : (string[])[]);
        Assert.Equal(new Avalonia.Input.KeyGesture(Avalonia.Input.Key.R, Avalonia.Input.KeyModifiers.Control | Avalonia.Input.KeyModifiers.Alt),
            Menus.AppMenu.Find(window.Menus, "Run", "Run on VentureOS")!.Gesture);
        window.Close();
    });

    [Fact]
    public void OpeningAnotherBundleFromTheDesignerOpensIt() => TestApp.Run(() =>
    {
        var (window, _) = OpenCalculatorBundle();
        window.Text = window.Text.Replace("text=\"=\"", "text=\"Go\"", StringComparison.Ordinal);
        var files = CopyFiles();

        window.Open(files);

        Assert.Equal("Files.app", window.Bundle!.Name);
        Assert.Equal(Path.Combine(files, "Contents", "Resources", "main.uix"), window.CurrentFile);
        window.Close();
    });

    [Theory]
    [InlineData(Bundles.LeaveChoice.Cancel, "Calculator.app", true)]
    [InlineData(Bundles.LeaveChoice.DontSave, "Files.app", true)]
    [InlineData(Bundles.LeaveChoice.SaveAll, "Files.app", false)]
    public void LeavingABundleWithUnsavedEditsAsksFirst(Bundles.LeaveChoice answer, string opened, bool editUnsavedOnDisk) => TestApp.Run(() =>
    {
        var (window, app) = OpenCalculatorBundle();
        var main = window.CurrentFile!;
        var saved = File.ReadAllText(main);
        window.Text = window.Text.Replace("text=\"=\"", "text=\"Go\"", StringComparison.Ordinal);
        IReadOnlyList<string>? asked = null;
        window.AskToLeave = files =>
        {
            asked = files;
            return Task.FromResult(answer);
        };

        var files = CopyFiles();
        window.OpenAsync(files).GetAwaiter().GetResult();

        Assert.Equal([main], asked);
        Assert.Equal(opened, window.Bundle!.Name);
        Assert.Equal(editUnsavedOnDisk, File.ReadAllText(main) == saved);
        window.AskToLeave = DontSave;
        window.Close();
    });

    [Fact]
    public void AnotherFileInTheSameBundleDoesntAsk() => TestApp.Run(() =>
    {
        var (window, app) = OpenCalculatorBundle();
        window.Text = window.Text.Replace("text=\"=\"", "text=\"Go\"", StringComparison.Ordinal);
        window.AskToLeave = _ => throw new InvalidOperationException("Asked about another file of the same bundle.");

        Assert.True(window.OpenAsync(Path.Combine(app, "Contents", "Lua", "main.lua")).GetAwaiter().GetResult());
        Assert.True(window.Bundle!.IsUnsaved(Path.Combine(app, "Contents", "Resources", "main.uix")));
        window.AskToLeave = DontSave;
        window.Close();
    });

    [Fact]
    public void ClosingWithUnsavedEditsAsksAndCancelKeepsTheWindowOpen() => TestApp.Run(() =>
    {
        var (window, _) = OpenCalculatorBundle();
        window.Text = window.Text.Replace("text=\"=\"", "text=\"Go\"", StringComparison.Ordinal);
        var asked = 0;
        window.AskToLeave = _ =>
        {
            asked++;
            return Task.FromResult(Bundles.LeaveChoice.Cancel);
        };
        var closed = false;
        window.Closed += (_, _) => closed = true;

        window.Close();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, asked);
        Assert.False(closed);

        window.AskToLeave = DontSave;
        window.Close();
        Dispatcher.UIThread.RunJobs();
        Assert.True(closed);
    });

    [Fact]
    public void ADocumentOutsideABundleCountsItsOwnUnsavedEdit() => TestApp.Run(() =>
    {
        var (window, _) = OpenCalculator();
        Assert.Empty(window.UnsavedFiles());

        window.Text = window.Text.Replace("text=\"=\"", "text=\"Go\"", StringComparison.Ordinal);

        Assert.Equal([document], window.UnsavedFiles());
        window.Close();
    });

    private static Task<Bundles.LeaveChoice> DontSave(IReadOnlyList<string> files) => Task.FromResult(Bundles.LeaveChoice.DontSave);

    /// <summary>Copies Files.app beside the test's other files, and gives its folder.</summary>
    private string CopyFiles()
    {
        var files = Path.Combine(folder, "Files.app");
        var source = Path.Combine(SkiaPainterTests.RepositoryRoot(), "rootfs", "system", "apps", "Files.app");
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(files, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        return files;
    }

    /// <summary>The navigator's labels, as shown, unsaved marks included.</summary>
    private static List<string> Labels(MainWindow window)
    {
        var tree = window.FindControl<TreeView>("FileTree")!;
        window.UpdateLayout();
        return [.. tree.GetLogicalDescendants().OfType<TextBlock>().Select(block => block.Text ?? "")];
    }

    /// <summary>Opens a copy of Calculator.app, by its folder.</summary>
    /// <param name="prepare">Changes the copy's files, given its folder, before it's opened.</param>
    private (MainWindow Window, string App) OpenCalculatorBundle(Action<string>? prepare = null)
    {
        var app = Path.Combine(folder, "Calculator.app");
        var source = Path.Combine(SkiaPainterTests.RepositoryRoot(), "rootfs", "user", "apps", "Calculator.app");
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(app, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        prepare?.Invoke(app);

        // The copy is outside VentureOS's rootfs, a stray; tests that want the offer of a project answer it themselves.
        MainWindow window = new() { AskToLeave = DontSave, AskToMakeProject = _ => Task.FromResult(false) };
        window.Show();
        window.Open(app);
        Dispatcher.UIThread.RunJobs();
        return (window, app);
    }

    /// <summary>Makes a project with Counter's app from the template, and opens it.</summary>
    private (MainWindow Window, string Project) OpenCounterProject()
    {
        MainWindow window = new() { AskToLeave = DontSave };
        window.Show();
        var project = window.CreateApp("Counter", "dev.example.counter", folder);
        Dispatcher.UIThread.RunJobs();
        return (window, project);
    }

    /// <summary>Lets timers that wait for a pause in typing run, as the window's own thread would.</summary>
    private static void Pause()
    {
        DispatcherFrame frame = new();
        DispatcherTimer.RunOnce(() => frame.Continue = false, MainWindow.PreviewDelay * 3);
        Dispatcher.UIThread.PushFrame(frame);
    }

    /// <summary>Chooses a file in the navigator, as a click would.</summary>
    private static void Select(MainWindow window, string file)
    {
        var tree = window.FindControl<TreeView>("FileTree")!;
        tree.SelectedItem = Find(((Bundles.BundleItem[])tree.ItemsSource!)[0]);

        Bundles.BundleItem? Find(Bundles.BundleItem item) =>
            item.Path == file ? item : item.Children.Select(Find).FirstOrDefault(found => found is not null);
    }

    /// <summary>Opens a copy of Files' window, which has menus, and gives its elements.</summary>
    private IReadOnlyList<Venture.OS.Interface.Xml.FlatXmlElement> OpenFiles(MainWindow window)
    {
        var files = Path.Combine(folder, "files.uix");
        File.Copy(Path.Combine(SkiaPainterTests.RepositoryRoot(), "rootfs", "system", "apps", "Files.app", "Contents", "Resources", "main.uix"), files);
        window.Open(files);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return window.Document!.Xml!.Children;
    }

    private (MainWindow Window, int EqualsKey) OpenCalculator()
    {
        MainWindow window = new() { AskToLeave = DontSave };
        window.Show();
        window.Open(document);
        Dispatcher.UIThread.RunJobs();
        var equals = window.Document!.Xml!.Children.ToList().FindIndex(element => element["name"] == "equals");
        return (window, equals);
    }

    private static Canvas Layer(MainWindow window) => window.FindControl<Canvas>("SelectionLayer")!;

    /// <summary>A point on the canvas, in VentureOS's pixels, in the window's own coordinates.</summary>
    private static Point At(MainWindow window, float x, float y) =>
        window.FindControl<Image>("PreviewImage")!.TranslatePoint(new Point(x, y), window)!.Value;

    private static void Click(MainWindow window, float x, float y)
    {
        window.MouseDown(At(window, x, y), MouseButton.Left);
        window.MouseUp(At(window, x, y), MouseButton.Left);
    }

    private static uint Pixel(WriteableBitmap bitmap, int x, int y)
    {
        using var locked = bitmap.Lock();
        var pixel = new int[1];
        System.Runtime.InteropServices.Marshal.Copy(locked.Address + (y * locked.RowBytes) + (x * 4), pixel, 0, 1);
        return (uint)pixel[0];
    }
}
