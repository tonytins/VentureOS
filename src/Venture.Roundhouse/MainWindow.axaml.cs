using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Rectangle = Avalonia.Controls.Shapes.Rectangle;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;
using Venture.OS.Desktop;
using Venture.OS.Interface;
using Venture.Roundhouse.Designer;
using Venture.Roundhouse.Drawing;
using Venture.Roundhouse.Menus;
using Venture.Roundhouse.Simulator;

namespace Venture.Roundhouse;

/// <summary>
/// Roundhouse's window: a <c>.uix</c> document's text beside the canvas, which shows it as VentureOS draws it and
/// redraws a moment after typing stops. Text VentureOS wouldn't read keeps the last good drawing, and the status
/// says why and on which line.
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>How long after the last keystroke the canvas redraws.</summary>
    public static readonly TimeSpan PreviewDelay = TimeSpan.FromMilliseconds(300);

    // GNOME's nine accents, as VentureOS's Settings offers them.
    private static readonly string[] AccentNames = ["Blue", "Teal", "Green", "Yellow", "Orange", "Red", "Pink", "Purple", "Slate"];

    private readonly DispatcherTimer redraw = new() { Interval = PreviewDelay };
    private readonly SkiaFonts fonts = SkiaFonts.LoadShipped();
    private string? path;
    private bool loading;

    // The editor shows the text without a byte order mark, and the document keeps it, so a save leaves it as it was.
    private string byteOrderMark = "";

    private GridLength textHeight = new(240);

    // The drag under way on the canvas, if any.
    private Drag? drag;

    // Set while the outline is updated to match, so it doesn't select in turn.
    private bool syncing;

    // The menus as the desktop's global menu bar shows them, refilled as what's enabled and checked changes.
    private readonly NativeMenu nativeMenu = new();

    public MainWindow()
    {
        InitializeComponent();
        Title = ProductTitle;
        // The look starts on VentureOS's standard one, which the canvas shows until View changes it.
        DesktopAppearance.Use(DesktopLook.Standard);
        loading = true;

        Menus = BuildMenus();
        NativeMenu.SetMenu(this, nativeMenu);
        foreach (var (key, modifiers, run) in (ReadOnlySpan<(Key, KeyModifiers, Action)>)
                 [
                     (Key.O, KeyModifiers.Control, () => _ = PickAndOpenAsync()),
                     (Key.S, KeyModifiers.Control, () => Save()),
                     (Key.Q, KeyModifiers.Control, Close),
                     (Key.Z, KeyModifiers.Control, Undo),
                     (Key.Z, KeyModifiers.Control | KeyModifiers.Shift, Redo),
                     (Key.Y, KeyModifiers.Control, Redo),
                     (Key.OemPlus, KeyModifiers.Control, () => ZoomToggle.IsChecked = ZoomToggle.IsChecked != true),
                     (Key.R, KeyModifiers.Control, Run),
                     (Key.R, KeyModifiers.Control | KeyModifiers.Shift, RestartRun),
                     (Key.OemPeriod, KeyModifiers.Control, StopRun),
                 ])
        {
            // Bound to the window, so they act only when the focused control, such as the editor, hasn't already.
            KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(key, modifiers), Command = new Command(run) });
        }

        MergeTitleBar(TitleBars.Merges());
        Editor.TextArea.Caret.PositionChanged += (_, _) => ShowCaret();

        // Clicking the canvas focuses it for the arrow keys; scrolling it into view then would slide it under the
        // pointer mid-drag, so it stays where the person put it.
        CanvasHost.AddHandler(RequestBringIntoViewEvent, (_, e) => e.Handled = true);
        Inspector.Commit = CommitAttribute;
        AddButton.Click += (_, _) =>
        {
            // The Insert menu's entries, read now, so what's enabled is current.
            var library = AppMenu.Flyout(Menus.Single(group => group.Title == "Insert").Entries);
            library.Placement = PlacementMode.BottomEdgeAlignedLeft;
            library.ShowAt(AddButton);
        };
        loading = false;
        ShowPanels();

        // XML highlighting, in colors that follow Roundhouse's own light or dark theme.
        RegistryOptions options = new(HighlightTheme());
        var highlighting = Editor.InstallTextMate(options);
        highlighting.SetGrammar(options.GetScopeByLanguageId(options.GetLanguageByExtension(".xml").Id));
        ActualThemeVariantChanged += (_, _) => highlighting.SetTheme(options.LoadTheme(HighlightTheme()));

        redraw.Tick += (_, _) =>
        {
            redraw.Stop();
            RenderNow();
        };
        Closed += (_, _) =>
        {
            // The run goes with the designer, so its copy of VentureOS's files is deleted.
            StopRun();
            fonts.Dispose();
        };
    }

    /// <summary>The window's title: Roundhouse and its version, such as <c>Roundhouse 0.6.101</c>.</summary>
    public static string ProductTitle => $"Roundhouse {ThisAssembly.Git.SemVer.Major}.{ThisAssembly.Git.SemVer.Minor}.{ThisAssembly.Git.SemVer.Patch}";

    /// <summary>
    /// Roundhouse's menus, shown in the desktop's global menu bar where there is one and behind the ☰ button where
    /// there isn't. Each command reads whether it can run, and whether it's checked, when the menus are shown.
    /// </summary>
    public IReadOnlyList<AppMenuGroup> Menus { get; }

    /// <summary>Whether the coloured bar is the window's title bar, with the window buttons at its end.</summary>
    public bool IsTitleBarMerged { get; private set; }

    /// <summary>Whether the canvas shows VentureOS's dark look.</summary>
    public bool IsDark { get; private set; }

    /// <summary>Which of VentureOS's nine accents the canvas shows, from 0, GNOME blue.</summary>
    public int Accent { get; private set; }

    /// <summary>Shows the canvas in VentureOS's light or dark look, and redraws it.</summary>
    public void SetDark(bool dark)
    {
        IsDark = dark;
        UseLook();
    }

    /// <summary>Shows the canvas with one of VentureOS's nine accents, from 0, and redraws it.</summary>
    /// <exception cref="ArgumentOutOfRangeException">There's no such accent.</exception>
    public void SetAccent(int accent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(accent);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(accent, DesktopPalette.Swatches.Count);
        Accent = accent;
        UseLook();
    }

    /// <summary>Undoes the last change to the text, from typing or the designer alike, and redraws.</summary>
    public void Undo()
    {
        if (!Editor.CanUndo)
            return;
        Editor.Undo();
        RenderNow();
    }

    /// <summary>Redoes what <see cref="Undo"/> undid, and redraws.</summary>
    public void Redo()
    {
        if (!Editor.CanRedo)
            return;
        Editor.Redo();
        RenderNow();
    }

    /// <summary>The document open, as a full path, or <see langword="null"/>.</summary>
    public string? DocumentPath => path;

    /// <summary>The canvas as last drawn, or <see langword="null"/> before anything has been.</summary>
    public WriteableBitmap? Preview => PreviewImage.Source as WriteableBitmap;

    /// <summary>What the status line says.</summary>
    public string Status => StatusText.Text ?? "";

    /// <summary>The document's text, as edited.</summary>
    public string Text
    {
        get => Editor.Text;
        set => Editor.Text = value;
    }

    /// <summary>
    /// The document as last read from the editor's text, with what VentureOS reads from it, or why it refuses it;
    /// <see langword="null"/> before one is open. It catches up with typing when the canvas redraws.
    /// </summary>
    public UIDocument? Document { get; private set; }

    /// <summary>Where each element was drawn on the canvas, or <see langword="null"/> before anything has been.</summary>
    public DesignMap? Map { get; private set; }

    /// <summary>The selected element, by its index among the root's children, or <see langword="null"/>.</summary>
    public int? Selection { get; private set; }

    /// <summary>Whether the text is showing below the canvas.</summary>
    public bool IsTextShowing => TextToggle.IsChecked == true;

    /// <summary>Shows or hides the text below the canvas.</summary>
    public void ShowText(bool show) => TextToggle.IsChecked = show;

    /// <summary>Selects an element, by its index among the root's children, or nothing.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The document has no such element.</exception>
    public void Select(int? element)
    {
        if (element is { } index && (Document?.Xml is not { } xml || index < 0 || index >= xml.Children.Count))
            throw new ArgumentOutOfRangeException(nameof(element), element, "The document has no such element.");
        Selection = element;
        ShowSelection();
        ShowPanels();
    }

    /// <summary>How much the canvas is magnified: 1 or 2, so each of VentureOS's pixels stays a whole number of the screen's.</summary>
    public double Zoom => ZoomToggle.IsChecked == true ? 2 : 1;

    /// <summary>Shows the canvas at 100% or, with <paramref name="twice"/>, 200%.</summary>
    public void SetZoom(bool twice) => ZoomToggle.IsChecked = twice;

    /// <summary>
    /// Moves the selected control or visual by whole pixels, as the arrow keys do. Nothing happens without one, and a
    /// move VentureOS would refuse says why in the status line.
    /// </summary>
    public void Nudge(int dx, int dy)
    {
        if (Selection is not { } element || !CanEdit(element) || Map?.BoundsOf(element) is not { } bounds)
            return;

        try
        {
            Apply(FrameEdit.Apply(Document!, element, bounds, new FrameChange(dx, dy, 0, 0)));
        }
        catch (UIScriptException exception)
        {
            StatusText.Text = $"VentureOS wouldn't read that: {exception.Message}";
        }
    }

    /// <summary>
    /// Shows an edited document, such as one the designer changed, by replacing only the text that changed, so the
    /// editor keeps its place and can undo the edit.
    /// </summary>
    /// <exception cref="InvalidOperationException">No document is open.</exception>
    public void Apply(UIDocument edited)
    {
        ArgumentNullException.ThrowIfNull(edited);
        if (path is null)
            throw new InvalidOperationException("Open a document before editing one.");

        var text = WithoutByteOrderMark(edited.Text);
        var current = Editor.Text;
        var prefix = 0;
        var limit = Math.Min(current.Length, text.Length);
        while (prefix < limit && current[prefix] == text[prefix])
            prefix++;
        var suffix = 0;
        while (suffix < limit - prefix && current[^(suffix + 1)] == text[^(suffix + 1)])
            suffix++;

        loading = true;
        Editor.Document.Replace(prefix, current.Length - prefix - suffix, text.Substring(prefix, text.Length - prefix - suffix));
        loading = false;
        RenderNow();
    }

    /// <summary>Opens a document and draws it; one that can't be read says why.</summary>
    public void Open(string file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var full = Path.GetFullPath(file);
        UIDocument document;
        try
        {
            document = UIDocument.Load(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            StatusText.Text = $"{file} couldn't be opened: {exception.Message}";
            return;
        }

        path = full;
        Document = document;
        Selection = null;
        Map = null;
        byteOrderMark = document.Text.StartsWith('\uFEFF') ? "\uFEFF" : "";
        loading = true;
        Editor.Text = WithoutByteOrderMark(document.Text);
        Editor.Document.UndoStack.ClearAll();
        loading = false;
        Title = $"{Path.GetFileName(full)} — {ProductTitle}";
        DocumentTitle.Text = BarTitle(full);
        RenderNow();
    }

    /// <summary>
    /// Saves the text exactly as it is in the editor, even text VentureOS would refuse, with the byte order mark the
    /// file had; a file nobody edited is saved byte for byte.
    /// </summary>
    /// <returns>Whether it was saved.</returns>
    public bool Save()
    {
        if (path is null)
            return false;

        try
        {
            Read().Save(path);
            StatusText.Text = $"Saved {Path.GetFileName(path)}.";
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or EncoderFallbackException)
        {
            StatusText.Text = $"{Path.GetFileName(path)} couldn't be saved: {exception.Message}";
            return false;
        }
    }

    /// <summary>
    /// Draws the text as VentureOS would, now, rather than after the delay. Text VentureOS refuses leaves the last
    /// drawing in place and says why.
    /// </summary>
    public void RenderNow()
    {
        redraw.Stop();
        if (path is null)
            return;

        var document = Read();
        ShowPanels();
        if (document.Script is not { } script)
        {
            // The reader's message already starts with the line it stopped on.
            StatusText.Text = $"{document.Problem!.Message.TrimEnd('.')}. The canvas shows the last version VentureOS "
                + "could read, and only the text can be edited until VentureOS reads it.";
            return;
        }

        if (Selection >= document.Xml!.Children.Count)
            Selection = null;
        var problems = Draw(document, script);
        ShowPanels();
        StatusText.Text = problems.Count == 0 ? "VentureOS reads this document." : string.Join(" ", problems);
    }

    /// <summary>Draws a document VentureOS reads onto the canvas, with the selection over it; the text isn't touched.</summary>
    /// <returns>What couldn't be loaded, such as a missing image.</returns>
    private List<string> Draw(UIDocument document, UIScript script)
    {
        var file = path ?? throw new InvalidOperationException("Only an open document is drawn.");
        List<string> problems = [];
        var view = DocumentView.Open(script, Path.GetDirectoryName(file) ?? "/", image => SkiaImages.Load(image), problems.Add);
        using var painter = RenderCommand.Draw(view, Path.GetFileName(file), fonts, out var drawn);
        PreviewImage.Source = ToBitmap(painter);
        Map = new DesignMap(document, drawn);
        ShowSelection();
        return problems;
    }

    /// <summary>
    /// Adds a control or another part of the window from the library, and selects it. A control goes in front of
    /// everything; a menu item joins the selected menu; a menu or sidebar adds the feature it needs.
    /// </summary>
    /// <returns>Whether it was added; when it wasn't, the status line says why.</returns>
    public bool AddControl(string element) =>
        Edit(document => DesignEdits.Add(document, element, Selection));

    /// <summary>Copies the selection just in front of it, set off a little, and selects the copy.</summary>
    /// <returns>Whether it was copied; when it wasn't, the status line says why.</returns>
    public bool DuplicateSelection() =>
        Selection is { } element && Edit(document => DesignEdits.Duplicate(document, element));

    /// <summary>Deletes the selection with the comments above it, leaving nothing selected.</summary>
    /// <returns>Whether it was deleted; when it wasn't, such as the window, the status line says why.</returns>
    public bool DeleteSelection() =>
        Selection is { } element && Edit(document => (document.RemoveElement(element), null));

    /// <summary>Moves the selected control or visual in front of the next one.</summary>
    /// <returns>Whether it moved.</returns>
    public bool BringForward() =>
        Selection is { } element && Edit(document => DesignEdits.BringForward(document, element));

    /// <summary>Moves the selected control or visual behind the one before it.</summary>
    /// <returns>Whether it moved.</returns>
    public bool SendBackward() =>
        Selection is { } element && Edit(document => DesignEdits.SendBackward(document, element));

    /// <summary>
    /// Makes one designer edit and selects what it says to, or says in the status line why VentureOS wouldn't read
    /// the result, leaving the document as it was.
    /// </summary>
    private bool Edit(Func<UIDocument, (UIDocument Document, int? Select)> edit)
    {
        if (Document is not { IsEditable: true } document)
            return false;

        try
        {
            var (edited, select) = edit(document);
            if (ReferenceEquals(edited, document))
                return false;

            // Clear the selection first, so the redraw never shows the old index on a different element.
            Selection = null;
            Apply(edited);
            Select(select);
            return true;
        }
        catch (Exception exception) when (exception is UIScriptException or ArgumentException)
        {
            StatusText.Text = $"VentureOS wouldn't read that: {exception.Message}";
            return false;
        }
    }

    /// <summary>
    /// Shows the document's elements in the outline and the selected one's attributes in the inspector. A document
    /// VentureOS refuses stays listed but can't be edited until it reads.
    /// </summary>
    private void ShowPanels()
    {
        var xml = Document?.Xml;
        var editable = Document?.IsEditable == true;
        string[] labels = xml is null ? [] : [.. xml.Children.Select(OutlineLabel)];

        syncing = true;
        if (OutlineList.ItemsSource is not string[] shown || !shown.SequenceEqual(labels))
            OutlineList.ItemsSource = labels;
        OutlineList.SelectedIndex = Selection ?? -1;
        syncing = false;

        OutlineHint.IsVisible = xml is null;
        AddButton.IsEnabled = editable;
        ShowUndo();
        AppMenu.Fill(nativeMenu, Menus);
        OutlinePane.IsEnabled = InspectorPane.IsEnabled = editable;
        InspectorHint.IsVisible = Selection is null;
        InspectorHint.Text = editable || Document is null
            ? "Select an element to see its attributes."
            : "VentureOS can't read the text as it is, so only the text can be edited.";
        Inspector.Show(editable && Selection is { } element && element < xml!.Children.Count ? xml.Children[element] : null);
    }

    /// <summary>An element as the outline lists it: its kind, and its name, or the menu it's in.</summary>
    private static string OutlineLabel(OS.Interface.Xml.FlatXmlElement element) =>
        element["name"] is { } name ? $"{element.Name}  {name}"
        : element["menu"] is { } menu ? $"{element.Name}  in {menu}"
        : element.Name;

    /// <summary>Commits an inspector change to the selected element, or says why it can't be made.</summary>
    private string? CommitAttribute(string attribute, string? value)
    {
        if (Selection is not { } element || Document is not { IsEditable: true } document)
            return "Select an element in a document VentureOS reads first.";

        try
        {
            Apply(value is null ? document.RemoveAttribute(element, attribute) : document.SetAttribute(element, attribute, value));
            return null;
        }
        catch (Exception exception) when (exception is UIScriptException or ArgumentException)
        {
            return exception.Message;
        }
    }

    private void OnOutlineSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (!syncing && OutlineList.SelectedIndex is var index)
            Select(index >= 0 ? index : null);
    }

    /// <summary>Whether the designer can change an element: one with a frame, in a document VentureOS reads as drawn.</summary>
    private bool CanEdit(int element) => Kind(element) is { } kind && FrameEdit.IsFramed(kind);

    /// <summary>The handles the selected element can be resized by on the canvas: none when it can't be.</summary>
    private IReadOnlyList<Handle> HandlesOf(int element) => Kind(element) is { } kind ? FrameEdit.HandlesFor(kind) : [];

    /// <summary>Whether an element can move in front of or behind others: a control, a visual, or a toolbar item.</summary>
    private bool CanRestack(int element) => Kind(element) is { } kind && (FrameEdit.IsFramed(kind) || kind == "ToolbarItem");

    /// <summary>An element's name, such as <c>Button</c>, in a document VentureOS reads as drawn; otherwise <see langword="null"/>.</summary>
    private string? Kind(int element) =>
        Document is { IsEditable: true, Xml: { } xml } && Map is not null && element >= 0 && element < xml.Children.Count
            ? xml.Children[element].Name
            : null;

    /// <summary>Outlines the selection on its own layer over the canvas, with handles on what can be resized.</summary>
    private void ShowSelection()
    {
        SelectionLayer.Children.Clear();
        if (PreviewImage.Source is { } image)
            (SelectionLayer.Width, SelectionLayer.Height) = (image.Size.Width, image.Size.Height);
        if (Selection is not { } element || Map?.BoundsOf(element) is not { } bounds)
            return;

        // A line one screen pixel wide at any zoom, in GNOME's blue, so it reads over any accent.
        var line = 1 / Zoom;
        IBrush accent = new SolidColorBrush(Color.FromRgb(0x35, 0x84, 0xE4));
        Rectangle outline = new() { Width = bounds.Width + line, Height = bounds.Height + line, Stroke = accent, StrokeThickness = line };
        Canvas.SetLeft(outline, bounds.X - (line / 2));
        Canvas.SetTop(outline, bounds.Y - (line / 2));
        SelectionLayer.Children.Add(outline);

        foreach (var handle in HandlesOf(element))
        {
            var (x, y) = FrameDrag.Centre(bounds, handle);
            var size = FrameDrag.HandleSize / Zoom;
            Rectangle box = new() { Width = size, Height = size, Fill = Brushes.White, Stroke = accent, StrokeThickness = line };
            Canvas.SetLeft(box, x - (size / 2));
            Canvas.SetTop(box, y - (size / 2));
            SelectionLayer.Children.Add(box);
        }
    }

    private void OnCanvasPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Map is null || Document is not { IsEditable: true } document || !e.GetCurrentPoint(CanvasHost).Properties.IsLeftButtonPressed)
            return;

        CanvasHost.Focus();
        var point = e.GetPosition(PreviewImage);

        // A handle of what's selected wins over whatever is under it; otherwise the click selects what's there.
        var handle = Handle.None;
        var element = Selection is { } selected && Map.BoundsOf(selected) is { } current
            && (handle = FrameDrag.HandleAt(current, (float)point.X, (float)point.Y, HandlesOf(selected))) != Handle.None
                ? selected
                : Map.ElementAt((float)point.X, (float)point.Y);
        Select(element);

        // A handle resizes what has one; dragging anywhere else moves only a control or visual.
        if (element is { } index && (handle != Handle.None || CanEdit(index)) && Map.BoundsOf(index) is { } bounds)
        {
            drag = new Drag(index, handle, point, bounds, document);
            e.Pointer.Capture(CanvasHost);
        }

        e.Handled = true;
    }

    private void OnCanvasMoved(object? sender, PointerEventArgs e)
    {
        if (drag is null)
            return;

        // Whole pixels of VentureOS's, whatever the zoom.
        var point = e.GetPosition(PreviewImage);
        var change = FrameDrag.Change(drag.Bounds, drag.Handle,
            (float)Math.Round(point.X - drag.Start.X), (float)Math.Round(point.Y - drag.Start.Y));
        if (change == drag.Change)
            return;
        drag.Change = change;

        try
        {
            // The canvas follows the drag from a document of its own; the text changes once, when it ends.
            drag.Pending = change.IsEmpty ? null : FrameEdit.Apply(drag.From, drag.Element, drag.Bounds, change);
            var shown = drag.Pending ?? drag.From;
            Draw(shown, shown.Script!);
        }
        catch (UIScriptException exception)
        {
            StatusText.Text = $"VentureOS wouldn't read that: {exception.Message}";
        }
    }

    private void OnCanvasReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (drag is null)
            return;

        var pending = drag.Pending;
        drag = null;
        e.Pointer.Capture(null);
        if (pending is not null)
            Apply(pending);
        else
            RenderNow();
    }

    private void OnCanvasKeyDown(object? sender, KeyEventArgs e)
    {
        OnDesignKeyDown(sender, e);
        if (e.Handled)
            return;

        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 8 : 1;
        (int X, int Y)? by = e.Key switch
        {
            Key.Left => (-step, 0),
            Key.Right => (step, 0),
            Key.Up => (0, -step),
            Key.Down => (0, step),
            _ => null,
        };
        if (by is not { } move || Selection is null)
            return;

        Nudge(move.X, move.Y);
        e.Handled = true;
    }

    /// <summary>
    /// The designer's shortcuts, while the canvas or outline has focus, so Delete and Ctrl+D still edit text in the
    /// editor and the inspector.
    /// </summary>
    private void OnDesignKeyDown(object? sender, KeyEventArgs e)
    {
        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        Func<bool>? action = (e.Key, control) switch
        {
            (Key.Delete or Key.Back, false) => DeleteSelection,
            (Key.D, true) => DuplicateSelection,
            (Key.OemCloseBrackets, true) => BringForward,
            (Key.OemOpenBrackets, true) => SendBackward,
            _ => null,
        };
        if (action is null || Selection is null)
            return;

        action();
        e.Handled = true;
    }

    /// <summary>Roundhouse's menus: File, Edit, Insert, Arrange, View, and Run.</summary>
    private List<AppMenuGroup> BuildMenus()
    {
        bool HasDocument() => path is not null;
        bool Editable() => Document?.IsEditable == true;
        bool Selected() => Editable() && Selection is not null;
        bool Stackable() => Editable() && Selection is { } element && CanRestack(element);
        KeyGesture Ctrl(Key key, KeyModifiers more = KeyModifiers.None) => new(key, KeyModifiers.Control | more);

        return
        [
            new("File",
            [
                new MenuCommand("Open…", () => _ = PickAndOpenAsync(), Ctrl(Key.O)),
                new MenuCommand("Save", () => Save(), Ctrl(Key.S), HasDocument),
                new MenuSeparator(),
                new MenuCommand("Quit", Close, Ctrl(Key.Q)),
            ]),
            new("Edit",
            [
                new MenuCommand("Undo", Undo, Ctrl(Key.Z), () => Editor.CanUndo),
                new MenuCommand("Redo", Redo, Ctrl(Key.Z, KeyModifiers.Shift), () => Editor.CanRedo),
                new MenuSeparator(),
                new MenuCommand("Duplicate", () => DuplicateSelection(), Ctrl(Key.D), Selected),
                new MenuCommand("Delete", () => DeleteSelection(), new KeyGesture(Key.Delete), Selected),
            ]),
            new("Insert",
            [
                .. DesignEdits.Library.Select(control =>
                    new MenuCommand(control.Element, () => AddControl(control.Element), IsEnabled: Editable)),
                new MenuSeparator(),
                .. DesignEdits.Parts.Select(part =>
                    new MenuCommand(part.Title, () => AddControl(part.Element), IsEnabled: Editable)),
            ]),
            new("Arrange",
            [
                new MenuCommand("Bring Forward", () => BringForward(), Ctrl(Key.OemCloseBrackets), Stackable),
                new MenuCommand("Send Backward", () => SendBackward(), Ctrl(Key.OemOpenBrackets), Stackable),
            ]),
            new("View",
            [
                new MenuCommand("Text", () => ShowText(!IsTextShowing), IsChecked: () => IsTextShowing),
                new MenuCommand("Twice the Size", () => SetZoom(Zoom != 2), Ctrl(Key.OemPlus), IsChecked: () => Zoom == 2),
                new MenuSeparator(),
                new MenuCommand("Dark", () => SetDark(!IsDark), IsChecked: () => IsDark),
                new AppMenuGroup("Accent", [.. AccentNames.Select((name, index) =>
                    new MenuCommand(name, () => SetAccent(index), IsChecked: () => Accent == index))]),
            ]),
            new("Run",
            [
                new MenuCommand("Run", Run, Ctrl(Key.R)),
                new MenuCommand("Restart", RestartRun, Ctrl(Key.R, KeyModifiers.Shift), () => Simulator is not null),
                new MenuCommand("Stop", StopRun, Ctrl(Key.OemPeriod), () => Simulator is not null),
            ]),
        ];
    }

    /// <summary>
    /// Makes the coloured bar the window's title bar, with Avalonia drawing the window buttons at its end, or leaves
    /// the window manager's title bar above it.
    /// </summary>
    private void MergeTitleBar(bool merged)
    {
        IsTitleBarMerged = merged;
        ExtendClientAreaToDecorationsHint = merged;
        ExtendClientAreaTitleBarHeightHint = merged ? TitleBar.Height : -1;

        // The bar drags the window; what's on it takes clicks.
        WindowDecorationProperties.SetElementRole(TitleBar, merged ? WindowDecorationsElementRole.TitleBar : WindowDecorationsElementRole.None);
        foreach (var control in TitleBar.GetLogicalDescendants().OfType<Button>())
            WindowDecorationProperties.SetElementRole(control, merged ? WindowDecorationsElementRole.User : WindowDecorationsElementRole.None);

        // On Linux, Avalonia draws the frame and Roundhouse the buttons, VentureOS's red, orange, and green dots; the
        // frame draws nothing else (see FrameOnly). Windows and macOS keep their own buttons beside the bar.
        if (merged && OperatingSystem.IsLinux())
            WindowDecorationsTheme = (ControlTheme)Resources["FrameOnly"]!;
        ShowWindowButtons();
        PlaceTitleBarContent();
    }

    /// <summary>Whether Roundhouse draws the window's buttons in the bar: where the bar is the title bar on Linux.</summary>
    public bool DrawsWindowButtons => IsTitleBarMerged && OperatingSystem.IsLinux() && WindowState != WindowState.FullScreen;

    private void ShowWindowButtons()
    {
        var palette = DesktopPalette.Light;
        CloseDot.Fill = Brush(palette.CloseButton);
        MinimizeDot.Fill = Brush(palette.MinimizeButton);
        ZoomDot.Fill = Brush(palette.ZoomButton);
        WindowButtons.IsVisible = DrawsWindowButtons;
        ToolTip.SetTip(MaximizeButton, WindowState == WindowState.Maximized ? "Restore" : "Zoom");

        static SolidColorBrush Brush(OS.Interface.UIColor color) => new(Color.FromRgb(color.Red, color.Green, color.Blue));
    }

    /// <summary>Keeps the bar's items clear of any window buttons the system draws beside it, as macOS's on the left.</summary>
    private void PlaceTitleBarContent()
    {
        var system = IsTitleBarMerged && !DrawsWindowButtons ? WindowDecorationMargin : default;
        TitleBarContent.Margin = new Thickness(10 + system.Left, 0, 10 + system.Right, 0);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowDecorationMarginProperty && TitleBarContent is not null)
            PlaceTitleBarContent();
        else if (change.Property == WindowStateProperty && MaximizeButton is not null)
        {
            ShowWindowButtons();
        }
        else if (change.Property == NativeMenu.IsNativeMenuExportedProperty && MenuButton is not null)
            MenuButton.IsVisible = !NativeMenu.GetIsNativeMenuExported(this);
    }

    private void OnMenuClicked(object? sender, RoutedEventArgs e)
    {
        var flyout = AppMenu.Flyout(Menus);
        flyout.Placement = PlacementMode.BottomEdgeAlignedLeft;
        flyout.ShowAt(MenuButton);
    }

    private void OnMinimizeClicked(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClicked(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        // Double-clicking the bar itself, not a button on it, maximizes or restores, as a title bar does.
        if (IsTitleBarMerged && e.Source is Visual source && source.FindAncestorOfType<Button>(includeSelf: true) is null)
            OnMaximizeClicked(sender, e);
    }

    private void OnUndoClicked(object? sender, RoutedEventArgs e) => Undo();

    private void OnRedoClicked(object? sender, RoutedEventArgs e) => Redo();

    /// <summary>The bar's title: the document and the app it's in, on one line, such as <c>main.uix — Calculator.app</c>.</summary>
    public static string BarTitle(string path) =>
        Subtitle(path) is { Length: > 0 } app ? $"{Path.GetFileName(path)} — {app}" : Path.GetFileName(path);

    /// <summary>The window VentureOS runs in, while it's open.</summary>
    public SimulatorWindow? Simulator { get; private set; }

    /// <summary>
    /// Opens VentureOS in its own window beside the designer, running the app being designed as it's edited, saved
    /// or not, or brings the open one forward.
    /// </summary>
    public void Run()
    {
        if (Simulator is { } open)
        {
            open.Activate();
            return;
        }

        var simulator = new SimulatorWindow(path, path is null ? null : Editor.Text);
        Simulator = simulator;
        simulator.Closed += (_, _) =>
        {
            if (ReferenceEquals(Simulator, simulator))
                Simulator = null;
        };
        simulator.Show();
    }

    /// <summary>Ends the run, deleting its copy of VentureOS's files.</summary>
    public void StopRun() => Simulator?.Close();

    /// <summary>Ends the run and starts a fresh one, with the document as it's edited now.</summary>
    public void RestartRun()
    {
        StopRun();
        Run();
    }

    private void OnRunClicked(object? sender, RoutedEventArgs e) => Run();

    /// <summary>What a document belongs to, shown after its name: the app bundle it's in, such as Calculator.app, or its folder.</summary>
    public static string Subtitle(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        for (var folder = Path.GetDirectoryName(path); !string.IsNullOrEmpty(folder); folder = Path.GetDirectoryName(folder))
        {
            if (folder.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                return Path.GetFileName(folder);
        }

        return Path.GetFileName(Path.GetDirectoryName(path)) ?? "";
    }

    private void ShowUndo()
    {
        UndoButton.IsEnabled = Editor.CanUndo;
        RedoButton.IsEnabled = Editor.CanRedo;
    }

    private void ShowCaret()
    {
        var caret = Editor.TextArea.Caret;
        CaretText.Text = path is null ? "" : $"Ln {caret.Line}, Col {caret.Column}";
    }

    private void OnZoomChanged(object? sender, RoutedEventArgs e)
    {
        if (CanvasZoom is null)
            return;
        CanvasZoom.LayoutTransform = new ScaleTransform(Zoom, Zoom);
        ShowSelection();
    }

    /// <summary>Reads the editor's text as the document, keeping the byte order mark the file had.</summary>
    private UIDocument Read()
    {
        var text = byteOrderMark + Editor.Text;
        if (Document?.Text != text)
            Document = UIDocument.Read(text);
        return Document;
    }

    private static string WithoutByteOrderMark(string text) => text.StartsWith('\uFEFF') ? text[1..] : text;

    private ThemeName HighlightTheme() => ActualThemeVariant == ThemeVariant.Dark ? ThemeName.DarkPlus : ThemeName.LightPlus;

    private void OnTextChanged(object? sender, EventArgs e)
    {
        // A redraw waits for a pause in typing, since drawing a document takes a moment.
        if (loading || path is null)
            return;
        ShowUndo();
        redraw.Stop();
        redraw.Start();
    }

    /// <summary>Shows the canvas in the look View has chosen, and redraws it.</summary>
    private void UseLook()
    {
        DesktopAppearance.Use(DesktopLook.Standard with
        {
            Mode = IsDark ? LookMode.Dark : LookMode.Light,
            Accent = DesktopPalette.Swatches[Accent],
        });
        RenderNow();
        AppMenu.Fill(nativeMenu, Menus);
    }

    private void OnTextToggled(object? sender, RoutedEventArgs e)
    {
        // The divider remembers where it was, so showing the text again puts it back.
        var row = CenterGrid.RowDefinitions[2];
        if (IsTextShowing)
            row.Height = textHeight;
        else
        {
            textHeight = row.Height;
            row.Height = new GridLength(0);
        }

        Editor.IsVisible = TextSplitter.IsVisible = IsTextShowing;
    }

    private async Task PickAndOpenAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open a .uix document",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("UIScript documents") { Patterns = ["*.uix"] }, FilePickerFileTypes.All],
        });

        if (files is [var file, ..] && file.TryGetLocalPath() is { } local)
            Open(local);
    }

    /// <summary>The painter's frame as an Avalonia bitmap: its pixels are BGRA, and always opaque.</summary>
    private static WriteableBitmap ToBitmap(SkiaPainter painter)
    {
        WriteableBitmap bitmap = new(new PixelSize(painter.Width, painter.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using var locked = bitmap.Lock();
        for (var row = 0; row < painter.Height; row++)
            Marshal.Copy(painter.Pixels, row * painter.Width, locked.Address + (row * locked.RowBytes), painter.Width);
        return bitmap;
    }

    /// <summary>
    /// A drag on the canvas: what it moves or resizes, from where, and the document it would leave, shown on the
    /// canvas until the drag ends.
    /// </summary>
    private sealed class Drag(int element, Handle handle, Point start, UIRect bounds, UIDocument from)
    {
        public int Element { get; } = element;

        public Handle Handle { get; } = handle;

        public Point Start { get; } = start;

        public UIRect Bounds { get; } = bounds;

        public UIDocument From { get; } = from;

        public FrameChange Change { get; set; }

        public UIDocument? Pending { get; set; }
    }

    /// <summary>A command that runs an action, for the window's key bindings.</summary>
    private sealed class Command(Action run) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => run();
    }
}
