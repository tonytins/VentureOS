using Avalonia;
using Avalonia.Input;
using Avalonia.Controls;
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

        Assert.Matches(@"^Roundhouse 0\.6\.\d+$", window.Title);
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
        var editor = window.FindControl<AvaloniaEdit.TextEditor>("Editor")!;
        center.RowDefinitions[2].Height = new GridLength(180);

        window.FindControl<ToggleButton>("TextToggle")!.IsChecked = false;

        Assert.False(window.IsTextShowing);
        Assert.False(editor.IsVisible);
        Assert.Equal(0, center.RowDefinitions[2].Height.Value);

        window.FindControl<ToggleButton>("TextToggle")!.IsChecked = true;

        Assert.True(editor.IsVisible);
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

        Assert.Equal(["File", "Edit", "Insert", "Arrange", "View"], menu.Items.OfType<NativeMenuItem>().Select(item => item.Header));
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

        Assert.Equal(["File", "Edit", "Insert", "Arrange", "View"], flyout.Items.OfType<MenuItem>().Select(item => (string)item.Header!));
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
    public void TheBarShowsTheDocumentsNameAndItsBundle() => TestApp.Run(() =>
    {
        var (window, _) = OpenCalculator();

        Assert.Equal("main.uix", window.FindControl<TextBlock>("DocumentTitle")!.Text);

        // The test's copy sits in a plain folder, so its folder is the subtitle.
        var subtitle = window.FindControl<TextBlock>("DocumentSubtitle")!;
        Assert.True(subtitle.IsVisible);
        Assert.StartsWith("roundhouse-window-", subtitle.Text);
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
        foreach (var name in (string[])["DocumentTitle", "DocumentSubtitle"])
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

    private static Menus.MenuCommand Command(MainWindow window, string menu, string title) =>
        Menus.AppMenu.Find(window.Menus, menu, title) ?? throw new InvalidOperationException($"No {menu} ▸ {title}.");

    private static Designer.InspectorPanel Inspector(MainWindow window) => window.FindControl<Designer.InspectorPanel>("Inspector")!;

    private (MainWindow Window, int EqualsKey) OpenCalculator()
    {
        MainWindow window = new();
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
