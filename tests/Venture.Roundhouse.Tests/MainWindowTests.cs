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

        window.FindControl<CheckBox>("DarkBox")!.IsChecked = true;

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
        Assert.Equal(window.Map.Window, window.Selection);
        Assert.Single(Layer(window).Children);

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
        window.FindControl<ComboBox>("ZoomBox")!.SelectedIndex = 1;
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
