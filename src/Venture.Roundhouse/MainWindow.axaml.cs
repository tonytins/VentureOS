using Venture.Airo.Simulation;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
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
using Venture.OS.Interface.Strings;
using Venture.OS.Bundles;
using Venture.Roundhouse.Bundles;
using Venture.Roundhouse.Designer;
using Venture.Roundhouse.Drawing;
using Venture.Roundhouse.Highlighting;
using Venture.Roundhouse.Logging;
using Venture.Roundhouse.Menus;
using Venture.Airo.Projects;
using Venture.Airo.Testing;
using Venture.OS.Shell;
using Venture.Roundhouse.Simulator;
using Venture.Roundhouse.Welcome;

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

    // The shared string tables a preview finds shared <String>s in, from the rootfs Roundhouse runs, or null.
    private readonly string? sharedStrings = SimulatedDisk.FindSharedStrings();

    // The file editor's highlighting, sharing the designer's grammars and adding CST's, and whether it's being
    // filled, not typed in.
    private readonly RegistryOptions highlightOptions;
    private readonly Grammars grammars;
    private readonly TextMate.Installation fileHighlighting;
    private bool loadingFile;

    // What's remembered between launches, such as recent items.
    private readonly RoundhouseSettings settings;

    // Where a project's app is assembled to run it, one folder for each kind of run, deleted when the window closes.
    private readonly string runCopies = Path.Combine(Path.GetTempPath(), $"roundhouse-run-{Guid.NewGuid():N}");

    /// <summary>Where a project's app is assembled to run it; it's deleted when the window closes.</summary>
    public string RunCopies => runCopies;

    /// <summary>What New Interface starts from: a window and nothing else.</summary>
    public const string BlankInterface = """
        <?xml version="1.0" encoding="UTF-8"?>
        <UIScript version="1">
          <Window width="320" height="240"/>
        </UIScript>

        """;

    // An untitled interface's text lives here, in the window's own folder, until Save says where it goes; nothing is
    // beside it, so its images and strings are missing until then.
    private string UntitledFile => Path.Combine(runCopies, "untitled", "Untitled.uix");

    /// <summary>Whether the designer has an untitled interface, one New Interface opened that hasn't been saved yet.</summary>
    public bool IsUntitled => path is not null && path == UntitledFile;

    // Whether the untitled interface has anything to lose: it's been changed since New Interface opened it.
    private bool UntitledChanged => IsUntitled && Editor.Text != WithoutByteOrderMark(BlankInterface);

    /// <summary>Asks where an untitled interface goes, giving the file to write; tests answer it themselves.</summary>
    public Func<Task<string?>> AskWhereToSave { get; set; }

    // A table's check waits for a pause in typing, as the canvas's redraw does; what it last found is logged once.
    private readonly DispatcherTimer tableCheck = new() { Interval = PreviewDelay };
    private IReadOnlyList<string> tableProblems = [];

    // Whether closing has asked about unsaved edits already, and may go ahead.
    private bool closingAsked;

    // The Info.xml whose text the Text tab holds, while it's shown, or null.
    private string? infoPath;

    // Roundhouse's own log, shown in the Log tab and kept in a file.
    private static readonly NLog.Logger log = RoundhouseLog.For("Roundhouse");

    // The files last marked unsaved in the list.
    private string[] shownUnsaved = [];

    // The menu bar over the canvas, the document its menus came from, and whether it holds the pointer.
    private readonly MenuBar documentMenus = new();
    private UIDocument? menusOf;
    private bool menuPointer;

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
                     (Key.N, KeyModifiers.Control, () => _ = NewAppAsync()),
                     (Key.N, KeyModifiers.Control | KeyModifiers.Shift, () => _ = NewProjectAsync()),
                     (Key.N, KeyModifiers.Control | KeyModifiers.Alt, () => _ = NewInterfaceAsync()),
                     (Key.O, KeyModifiers.Control, () => _ = PickAndOpenAsync()),
                     (Key.W, KeyModifiers.Control, () => _ = CloseOpenAsync()),
                     (Key.O, KeyModifiers.Control | KeyModifiers.Shift, () => _ = PickAndOpenBundleAsync()),
                     (Key.S, KeyModifiers.Control, () => Save()),
                     (Key.S, KeyModifiers.Control | KeyModifiers.Shift, () => SaveAll()),
                     (Key.Q, KeyModifiers.Control, Close),
                     (Key.Z, KeyModifiers.Control, Undo),
                     (Key.Z, KeyModifiers.Control | KeyModifiers.Shift, Redo),
                     (Key.Y, KeyModifiers.Control, Redo),
                     (Key.OemPlus, KeyModifiers.Control, () => ZoomToggle.IsChecked = ZoomToggle.IsChecked != true),
                     (Key.R, KeyModifiers.Control, Run),
                     (Key.R, KeyModifiers.Control | KeyModifiers.Alt, () => RunOnVentureOS()),
                     (Key.U, KeyModifiers.Control, () => Test()),
                     (Key.R, KeyModifiers.Control | KeyModifiers.Shift, RestartRun),
                     (Key.OemPeriod, KeyModifiers.Control, StopRun),
                 ])
        {
            // Bound to the window, so they act only when the focused control, such as the editor, hasn't already.
            KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(key, modifiers), Command = new Command(run) });
        }

        TitleBar.Attach(this, TitleBars.Merges());

        // Ahead of the list's own handling, so Tab moves into a menu or pane rather than to the next control.
        OutlineList.AddHandler(KeyDownEvent, OnOutlineKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        OutlineList.DoubleTapped += (_, _) =>
        {
            if (Selection is { } element)
                SetFolded(element, !IsFolded(element));
        };
        Editor.TextArea.Caret.PositionChanged += (_, _) => ShowCaret();
        FileEditor.TextArea.Caret.PositionChanged += (_, _) => ShowCaret();

        // A file with edits not yet saved is marked, as editors mark them.
        FileTree.ItemTemplate = new FuncTreeDataTemplate<BundleItem>(
            (item, _) => new TextBlock
            {
                Text = Bundle?.IsUnsaved(item.Path) == true ? $"{item.Name} ●" : item.Name,
                FontWeight = item.IsFolder ? FontWeight.SemiBold : FontWeight.Normal,
            },
            item => item.Children);

        // Clicking the canvas focuses it for the arrow keys; scrolling it into view then would slide it under the
        // pointer mid-drag, so it stays where the person put it.
        CanvasHost.AddHandler(RequestBringIntoViewEvent, (_, e) => e.Handled = true);
        // The Log tab shows what's been logged, and follows it; NLog may write from any thread.
        LogView.Text = string.Join("\n", RoundhouseLog.Buffer.Lines);
        Action<string> follow = line => Dispatcher.UIThread.Post(() => ShowLogLine(line));
        RoundhouseLog.Buffer.Added += follow;
        Closed += (_, _) => RoundhouseLog.Buffer.Added -= follow;

        Inspector.Commit = CommitAttribute;
        InfoForm.Commit = CommitInfo;
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
        highlightOptions = new(HighlightTheme());
        var highlighting = Editor.InstallTextMate(highlightOptions);
        highlighting.SetGrammar(highlightOptions.GetScopeByLanguageId(highlightOptions.GetLanguageByExtension(".xml").Id));
        grammars = new Grammars(highlightOptions);
        fileHighlighting = FileEditor.InstallTextMate(grammars);
        ActualThemeVariantChanged += (_, _) =>
        {
            highlighting.SetTheme(highlightOptions.LoadTheme(HighlightTheme()));
            fileHighlighting.SetTheme(highlightOptions.LoadTheme(HighlightTheme()));
        };

        redraw.Tick += (_, _) =>
        {
            redraw.Stop();
            RenderNow();
        };
        tableCheck.Tick += (_, _) =>
        {
            tableCheck.Stop();
            if (CurrentFile is { } table && IsTable(table))
                CheckTable(table);
        };
        AskToLeave = files => new UnsavedDialog(files).ShowDialog<LeaveChoice>(this);
        AskWhereToSave = () => new SaveInterfaceWindow(Project, NewAppWindow.SuggestedFolder()).ShowDialog<string?>(this);
        AskToReplace = app => ConfirmDialog.AskAsync(this, "Replace App",
            $"There's already {Path.GetFileName(app)} in {Path.GetDirectoryName(app)}. Replace it?",
            "The app there is deleted and the project's put in its place.", "Replace");
        AskToMakeProject = app => ConfirmDialog.AskAsync(this, "Make a Project",
            $"{Path.GetFileName(app)} isn't among VentureOS's apps. Make it a project?",
            $"An .app is something that runs. Work in progress belongs in a project: {Path.GetFileNameWithoutExtension(app)}.rproj, made beside it, "
            + "with the app's files in its App/. The .app is left as it is, for you to delete.",
            "Make Project", "Edit in Place");

        string? problem;
        (settings, problem) = RoundhouseSettings.Load(RoundhouseSettings.Location);
        if (problem is not null)
            log.Warn(problem);
        Closing += (_, e) =>
        {
            // Unsaved edits are asked about before the window goes; the answer closes it, or doesn't.
            if (closingAsked || UnsavedFiles().Count == 0)
                return;
            e.Cancel = true;
            _ = CloseAfterAskingAsync();
        };
        Closed += (_, _) =>
        {
            // The run goes with the designer, so its copy of VentureOS's files is deleted, and so do apps assembled to run.
            StopRun();
            fonts.Dispose();
            try
            {
                if (Directory.Exists(runCopies))
                    Directory.Delete(runCopies, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                log.Warn("A run's copy of an app, in {Folder}, couldn't be deleted: {Reason}", runCopies, exception.Message);
            }
        };
    }

    /// <summary>The window's title: Roundhouse and its version, such as <c>Roundhouse 0.6.101</c>.</summary>
    public static string ProductTitle => $"Roundhouse {ThisAssembly.Git.SemVer.Major}.{ThisAssembly.Git.SemVer.Minor}.{ThisAssembly.Git.SemVer.Patch}";

    /// <summary>
    /// Roundhouse's menus, shown in the desktop's global menu bar where there is one and behind the ☰ button where
    /// there isn't. Each command reads whether it can run, and whether it's checked, when the menus are shown.
    /// </summary>
    public IReadOnlyList<AppMenuGroup> Menus { get; }


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
        var editor = ActiveEditor;
        if (!editor.CanUndo)
            return;
        editor.Undo();
        if (ReferenceEquals(editor, Editor))
            RenderNow();
    }

    /// <summary>Redoes what <see cref="Undo"/> undid, and redraws.</summary>
    public void Redo()
    {
        var editor = ActiveEditor;
        if (!editor.CanRedo)
            return;
        editor.Redo();
        if (ReferenceEquals(editor, Editor))
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

    /// <summary>
    /// The pane the canvas shows, by name, kept across redraws and edits; the document's first pane when it's
    /// <see langword="null"/> or names none. Selecting a pane, or anything in one, shows it.
    /// </summary>
    public string? ShownPane { get; private set; }

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

        // What's selected shows in the outline, so the menus and panes holding it unfold.
        var children = Document?.Xml?.Children;
        for (var holder = element is { } inside && children is not null ? children[inside].Parent : -1; holder >= 0; holder = children![holder].Parent)
        {
            if (children![holder]["name"] is { } holderName)
                folded.Remove(holderName);
        }

        if (element is { } selected && PaneOf(Document!.Xml!, selected) is { } pane && pane != ShownPane)
        {
            ShownPane = pane;
            RedrawCanvas();
        }

        ShowSelection();
        ShowPanels();
    }

    /// <summary>The pane an element shows on: a pane's own, or the one a control is in; <see langword="null"/> for the rest.</summary>
    // A pane is its own; a control is in the pane that holds it, or the one it names.
    private static string? PaneOf(OS.Interface.Xml.FlatXmlDocument xml, int index)
    {
        var element = xml.Children[index];
        if (element.Name == "Pane")
            return element["name"];
        return element.Parent >= 0 && xml.Children[element.Parent].Name == "Pane" ? xml.Children[element.Parent]["name"] : element["pane"];
    }

    /// <summary>Selects the pane the canvas shows in a view about to be drawn, as a click on its row would.</summary>
    private void ShowPane(UIView view, UIScript script)
    {
        if (view.SidebarName is not { } sidebar || script.Panes.Count == 0)
            return;

        var index = 0;
        for (var each = 0; each < script.Panes.Count; each++)
        {
            if (script.Panes[each].Name == ShownPane)
                index = each;
        }

        view.List(sidebar)!.SelectedIndex = index;
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
            Say($"VentureOS wouldn't read that: {exception.Message}", NLog.LogLevel.Warn);
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

        ReplaceEditorText(edited.Text);
        RenderNow();
    }

    /// <summary>
    /// Puts edited text in the Text tab by replacing only what changed, so the editor keeps its place and its Undo
    /// takes the change back; the byte order mark stays with the file, not the editor.
    /// </summary>
    private void ReplaceEditorText(string edited)
    {
        var text = WithoutByteOrderMark(edited);
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
    }

    /// <summary>Opens a document and draws it; one that can't be read says why.</summary>
    public void Open(string file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(file));

        // A bundle's folder opens the bundle; a file in one opens the bundle at that file.
        var bundle = OpenBundle.Find(full);
        if (Directory.Exists(full) && bundle != full)
        {
            Say($"{file} isn't a bundle, such as an .app.", NLog.LogLevel.Warn);
            return;
        }

        // A project is read before anything is let go of, so one Roundhouse can't read leaves what's open as it was.
        var project = Project;
        if (bundle != Bundle?.Root)
        {
            project = null;
            if (bundle is not null && RoundhouseProject.IsProject(bundle))
            {
                try
                {
                    project = RoundhouseProject.Open(bundle);
                }
                catch (ProjectException exception)
                {
                    Say(exception.Message, NLog.LogLevel.Warn);
                    return;
                }
            }
        }

        KeepEdit();
        if (bundle != Bundle?.Root)
        {
            // The old bundle's file is let go of first, so nothing of it is kept in the new one.
            LeaveDesigner(null);
            CurrentFile = null;
            Bundle = bundle is null ? null : new OpenBundle(bundle);
            Project = project;
        }

        Remember(bundle ?? full);

        // Read the folder again, even for the bundle already open, so files added since are listed.
        ShowFiles();

        log.Info("Opened {Path}", full);
        if (full == bundle)
        {
            if (Bundle!.FirstFile() is { } first)
                ShowFile(first);
            else
                ShowPlaceholder(null);
            return;
        }

        ShowFile(full);
    }

    /// <summary>The bundle or project open, or <see langword="null"/> while a document outside one is.</summary>
    public OpenBundle? Bundle { get; private set; }

    /// <summary>The project open, when what's open is one.</summary>
    public RoundhouseProject? Project { get; private set; }

    /// <summary>The recent items, newest first, as the welcome window lists them.</summary>
    public IReadOnlyList<RecentItem> Recent => settings.Recent;

    // Puts a project, app, or loose interface at the top of the recent items, and saves them; a failure is logged.
    private void Remember(string path)
    {
        if (settings.Remember(path))
            SaveSettings();
    }

    // Writes what's remembered; a failure is logged, and a newer Roundhouse's file is left as it is.
    private void SaveSettings()
    {
        if (!settings.CanSave)
            return;
        try
        {
            settings.Save(RoundhouseSettings.Location);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            log.Warn("Roundhouse's settings couldn't be saved to {File}: {Reason}", RoundhouseSettings.Location, exception.Message);
        }
    }

    /// <summary>The file shown, by its path on this computer, or <see langword="null"/>.</summary>
    public string? CurrentFile { get; private set; }

    /// <summary>
    /// Shows a file: an interface in the designer, from its edited text if it has some, and anything else, for now,
    /// as a file Roundhouse has no editor for.
    /// </summary>
    /// <remarks>A document that can't be read is reported, and what was shown stays.</remarks>
    private void ShowFile(string full)
    {
        if (!full.EndsWith(".uix", StringComparison.OrdinalIgnoreCase))
        {
            KeepEdit();
            CurrentFile = full;
            SelectInFiles(full);
            ShowOtherFile(full);
            return;
        }

        // Kept first, so a document read from the bundle includes the edit being left, when it's the same one.
        KeepEdit();
        UIDocument document;
        try
        {
            document = Bundle is { } open && open.Contains(full) ? UIDocument.Read(open.Text(full)) : UIDocument.Load(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            Say($"{Path.GetFileName(full)} couldn't be opened: {exception.Message}", NLog.LogLevel.Warn);
            SelectInFiles(CurrentFile ?? "");
            return;
        }

        CurrentFile = full;
        SelectInFiles(full);
        ShowCenter(CanvasScroller);
        path = full;
        Document = document;
        Selection = null;
        ShownPane = null;
        folded.Clear();
        Map = null;
        byteOrderMark = document.Text.StartsWith('\uFEFF') ? "\uFEFF" : "";
        loading = true;
        Editor.Text = WithoutByteOrderMark(document.Text);
        Editor.Document.UndoStack.ClearAll();
        loading = false;
        Title = $"{Path.GetFileName(full)} — {ProductTitle}";
        DocumentTitle.Text = IsUntitled ? "Untitled.uix — not saved" : BarTitle(full);
        RenderNow();
    }

    /// <summary>The picture files the image view shows, by extension.</summary>
    public static IReadOnlyList<string> PictureExtensions { get; } = [".png", ".jpg", ".jpeg", ".bmp", ".gif"];

    /// <summary>The editor showing: the file editor while a text file is open, and otherwise the designer's text.</summary>
    private AvaloniaEdit.TextEditor ActiveEditor => FileEditor.IsVisible ? FileEditor : Editor;

    /// <summary>
    /// Shows a file other than an interface: a picture as it is, text in the file editor, highlighted as Lua or XML
    /// where it's either, and anything else as a file Roundhouse can't edit.
    /// </summary>
    private void ShowOtherFile(string file)
    {
        LeaveDesigner(file);
        if (PictureExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
        {
            try
            {
                FileImage.Source = new Bitmap(file);
                ShowCenter(ImageView);
                StatusText.Text = $"{Path.GetFileName(file)}, {FileImage.Source.Size.Width:0} by {FileImage.Source.Size.Height:0}.";
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            {
                ShowPlaceholder(file, $"{Path.GetFileName(file)} couldn't be shown: {exception.Message}");
            }

            return;
        }

        if (Bundle is not { } open)
        {
            ShowPlaceholder(file, $"Open {Path.GetFileName(file)}'s bundle to edit it.");
            return;
        }

        // A bundle's Info.xml shows as a form over its text, or why VentureOS refuses it, over the text to fix.
        if (IsInfo(open, file) && ShowInfo(open, file))
            return;

        string text;
        try
        {
            text = open.Text(file);
        }
        catch (InvalidDataException)
        {
            ShowPlaceholder(file, $"{Path.GetFileName(file)} isn't text.");
            return;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ShowPlaceholder(file, $"{Path.GetFileName(file)} couldn't be read: {exception.Message}");
            return;
        }

        loadingFile = true;
        FileEditor.Text = text;
        FileEditor.Document.UndoStack.ClearAll();
        loadingFile = false;
        fileHighlighting.SetGrammar(grammars.ScopeFor(file));
        ShowCenter(FileEditor);
        StatusText.Text = "";
        tableProblems = [];
        if (IsTable(file))
            CheckTable(file);
        ShowUndo();
        ShowCaret();
    }

    private static bool IsTable(string file) => file.EndsWith(".cst", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads a string table as VentureOS would, beside the documents that use it and over the shared tables, and says
    /// what's wrong with it: in the status line, and in the Log when that changes, so a pause in typing doesn't repeat
    /// it there.
    /// </summary>
    private void CheckTable(string file)
    {
        var name = Path.GetFileName(file);
        if (StringTables.TableNumber(name) is not { } number)
        {
            StatusText.Text = $"{name} isn't named like a table, such as _1_strings.cst, so no document finds its text.";
            return;
        }

        var problems = new StringTables(Path.GetDirectoryName(file) ?? "/", sharedStrings, ReadEdited()).Check(number);
        StatusText.Text = problems.Count switch
        {
            0 => $"VentureOS reads this as table {number}.",
            1 => problems[0],
            _ => $"{name} has {problems.Count} problems; see the Log.",
        };

        if (!problems.SequenceEqual(tableProblems))
        {
            // What the reader skipped names the file already; a reference that can't be followed names its table.
            foreach (var problem in problems)
                log.Warn(problem.StartsWith(name, StringComparison.Ordinal) ? problem : $"{name}: {problem}");
        }

        tableProblems = problems;
    }

    /// <summary>Reads a file as the bundle has it, unsaved edits included, so tables being edited show in the canvas.</summary>
    private Func<string, string>? ReadEdited() =>
        Bundle is { } open ? file => open.Contains(file) ? open.Text(file) : File.ReadAllText(file) : null;

    /// <summary>The bundle's <c>Info.xml</c> as last read, while it's the file shown, or <see langword="null"/>.</summary>
    public InfoDocument? Info { get; private set; }

    private static bool IsInfo(OpenBundle bundle, string file) =>
        Path.GetFullPath(file) == Path.GetFullPath(bundle.InfoFile);

    /// <summary>
    /// Shows a bundle's <c>Info.xml</c>: its text in the Text tab, as the designer's is, and above it the form, or why
    /// VentureOS refuses it. Each follows the other.
    /// </summary>
    /// <returns>Whether it did; it doesn't for a folder that isn't a kind of bundle, or text it can't read.</returns>
    private bool ShowInfo(OpenBundle bundle, string file)
    {
        if (InfoDocument.KindOf(bundle.Root) is null)
            return false;

        string text;
        try
        {
            text = bundle.Text(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return false;
        }

        infoPath = file;
        byteOrderMark = text.StartsWith('\uFEFF') ? "\uFEFF" : "";
        loading = true;
        Editor.Text = WithoutByteOrderMark(text);
        Editor.Document.UndoStack.ClearAll();
        loading = false;
        ShowCenter(InfoView);
        ReadInfo();
        StatusText.Text = "";
        ShowUndo();
        ShowCaret();
        return true;
    }

    /// <summary>Reads the Text tab's <c>Info.xml</c>, and shows its form, or why VentureOS refuses it.</summary>
    private void ReadInfo()
    {
        if (infoPath is null || Bundle is not { } open || InfoDocument.KindOf(open.Root) is not { } kind)
            return;

        Info = InfoDocument.Read(byteOrderMark + Editor.Text, open.Root, kind);
        if (Info.IsEditable)
            InfoForm.Show(Info, BundleFiles(open));
        else
            InfoForm.ShowProblem(Info.Problem!.Message);
    }

    /// <summary>The bundle's files, by their paths inside it with <c>/</c> between folders, for the form's choices.</summary>
    private static List<string> BundleFiles(OpenBundle bundle) =>
        [.. Directory.EnumerateFiles(bundle.Root, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(bundle.Root, file).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Commits a form change to the <c>Info.xml</c>, or says why VentureOS wouldn't take it.</summary>
    private string? CommitInfo(string attribute, string? value)
    {
        if (Info is not { } info || Bundle is not { } open || CurrentFile is not { } file)
            return "Open a bundle's Info.xml first.";

        try
        {
            var edited = value is null ? info.RemoveAttribute(attribute) : info.SetAttribute(attribute, value);
            if (ReferenceEquals(edited, info))
                return null;

            // The change goes into the text, where Undo can take it back, and the bundle keeps it.
            Info = edited;
            ReplaceEditorText(edited.Text);
            open.Edit(file, edited.Text);
            InfoForm.Show(edited, BundleFiles(open));
            ShowUnsaved();
            ShowUndo();
            return null;
        }
        catch (Exception exception) when (exception is BundleException or ArgumentException or InvalidOperationException)
        {
            log.Warn("Info.xml: {Attribute} refused: {Reason}", attribute, exception.Message);
            return exception.Message;
        }
    }

    /// <summary>
    /// Reports an error that nothing caught: in full in the log, and in the status line, pointing to the Log tab.
    /// </summary>
    public void ReportError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        log.Error(exception, "An error nothing caught: {Message}", exception.Message);
        StatusText.Text = $"Something went wrong: {exception.Message} The Log tab has the details.";
    }

    /// <summary>Adds a logged line to the Log tab, keeping it to the buffer's length, and scrolls to it.</summary>
    private void ShowLogLine(string line)
    {
        var document = LogView.Document;
        document.Insert(document.TextLength, document.TextLength == 0 ? line : "\n" + line);
        if (document.LineCount > LogBuffer.Capacity)
            document.Remove(0, document.GetLineByNumber(document.LineCount - LogBuffer.Capacity + 1).Offset);
        LogView.ScrollToEnd();
    }

    /// <summary>Shows a message in the status line and records it in the log, so it can be read after it's gone.</summary>
    private void Say(string message, NLog.LogLevel level)
    {
        StatusText.Text = message;
        log.Log(level, message);
    }

    /// <summary>Shows that Roundhouse has no editor for a file, or that a bundle has nothing to show, and why.</summary>
    private void ShowPlaceholder(string? file, string? why = null)
    {
        LeaveDesigner(file);
        ShowCenter(FilePlaceholder);
        FilePlaceholder.Text = why ?? $"{Bundle?.Name} has no interface or Info.xml to show; choose a file.";
    }

    /// <summary>Leaves the designer's document, for a file it doesn't show, and names the file in the bar.</summary>
    private void LeaveDesigner(string? file)
    {
        path = null;
        infoPath = null;
        ShownPane = null;
        Document = null;
        Map = null;
        MenusShown = null;
        Selection = null;
        PreviewImage.Source = null;
        loading = true;
        Editor.Text = "";
        Editor.Document.UndoStack.ClearAll();
        loading = false;
        Title = $"{Path.GetFileName(file ?? Bundle?.Root ?? "")} — {ProductTitle}";
        DocumentTitle.Text = file is null ? Bundle?.Name ?? "Roundhouse" : BarTitle(file);
        StatusText.Text = "";
        ShowSelection();
        ShowPanels();
    }

    /// <summary>Shows one of the centre's views: the designer, the file editor, a picture, or a placeholder.</summary>
    private void ShowCenter(Control shown)
    {
        foreach (var view in (Control[])[CanvasScroller, FileEditor, ImageView, InfoView, FilePlaceholder])
            view.IsVisible = ReferenceEquals(view, shown);
        if (!ReferenceEquals(shown, InfoView))
            Info = null;

        // The Text tab holds a .uix's or Info.xml's text; for anything else, the Log is what's below. Coming back to
        // text shows the Text tab again.
        var hasText = ReferenceEquals(shown, CanvasScroller) || ReferenceEquals(shown, InfoView);
        var hadText = TextTab.IsVisible;
        TextTab.IsVisible = hasText;
        if (!hasText)
            BottomTabs.SelectedItem = LogTab;
        else if (!hadText)
            BottomTabs.SelectedItem = TextTab;
    }

    private void OnFileTextChanged(object? sender, EventArgs e)
    {
        if (loadingFile || Bundle is not { } open || CurrentFile is not { } file)
            return;

        open.Edit(file, FileEditor.Text);
        ShowUnsaved();
        ShowUndo();
        if (IsTable(file))
        {
            tableCheck.Stop();
            tableCheck.Start();
        }
    }

    /// <summary>
    /// Marks the files with unsaved edits in the list, redrawing it only when which ones has changed, so typing
    /// doesn't rebuild it.
    /// </summary>
    private void ShowUnsaved()
    {
        var unsaved = Bundle?.Unsaved.Order(StringComparer.Ordinal).ToArray() ?? [];
        if (unsaved.SequenceEqual(shownUnsaved))
            return;

        shownUnsaved = unsaved;
        ShowFiles();
        if (CurrentFile is { } file)
            SelectInFiles(file);
    }

    /// <summary>
    /// Saves every file of the bundle with edits, the designer's included; one that can't be saved keeps its edit,
    /// and the status line names it.
    /// </summary>
    /// <returns>Whether every one was saved.</returns>
    public bool SaveAll()
    {
        if (Bundle is not { } open)
            return Save();

        KeepEdit();
        List<string> failed = [];
        var saved = 0;
        foreach (var file in open.Unsaved.ToList())
        {
            try
            {
                if (open.Save(file))
                    saved++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or EncoderFallbackException)
            {
                failed.Add($"{Path.GetFileName(file)} ({exception.Message})");
            }
        }

        Say(failed.Count == 0
            ? $"Saved {saved} {(saved == 1 ? "file" : "files")}."
            : $"Saved {saved}; couldn't save {string.Join(", ", failed)}.", failed.Count == 0 ? NLog.LogLevel.Info : NLog.LogLevel.Warn);
        ShowUnsaved();
        return failed.Count == 0;
    }

    /// <summary>
    /// Keeps the designer's text, edited or not, in the open bundle, so moving to another file and back keeps the
    /// edit until it's saved.
    /// </summary>
    private void KeepEdit()
    {
        if (Bundle is { } open && (path ?? infoPath) is { } file && open.Contains(file) && File.Exists(file))
            open.Edit(file, byteOrderMark + Editor.Text);
    }

    /// <summary>
    /// The files with edits not yet saved: the bundle's, or a document's outside one. Asking keeps the designer's
    /// text in the bundle first, so it counts.
    /// </summary>
    public IReadOnlyList<string> UnsavedFiles()
    {
        KeepEdit();
        if (Bundle is { } open)
            return [.. open.Unsaved.Order(StringComparer.Ordinal), .. UntitledChanged ? (string[])[UntitledFile] : []];

        if (IsUntitled)
            return UntitledChanged ? [UntitledFile] : [];
        if (path is null || !File.Exists(path))
            return [];
        try
        {
            return UIDocument.Load(path).Text == Read().Text ? [] : [path];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return [path];
        }
    }

    /// <summary>
    /// Asks what to do with unsaved edits before leaving them: shows the files and offers Save All, Don't Save, or
    /// Cancel. Tests answer it themselves.
    /// </summary>
    public Func<IReadOnlyList<string>, Task<LeaveChoice>> AskToLeave { get; set; }

    /// <summary>
    /// Makes ready to leave the open bundle or document: asks about any unsaved edits, and saves them if asked to.
    /// </summary>
    /// <returns>Whether to go on: there were none, they were saved, or they're to be left; not when Cancel is chosen or saving fails.</returns>
    public async Task<bool> LeaveAsync()
    {
        var unsaved = UnsavedFiles();
        if (unsaved.Count == 0)
            return true;

        return await AskToLeave(unsaved) switch
        {
            // An untitled interface is asked where it goes first; not saying is staying.
            LeaveChoice.SaveAll => (!UntitledChanged || await SaveUntitledAsync()) && SaveAll(),
            LeaveChoice.DontSave => true,
            _ => false,
        };
    }

    /// <summary>
    /// Opens a file or bundle as <see cref="Open"/> does, first asking about unsaved edits when that leaves the
    /// bundle or document open now.
    /// </summary>
    /// <returns>Whether it opened.</returns>
    public async Task<bool> OpenAsync(string file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var leaving = Bundle is { } open ? !open.Contains(file) && Path.GetFullPath(file) != open.Root : path is not null && Path.GetFullPath(file) != path;
        leaving |= UntitledChanged;
        if (leaving && !await LeaveAsync())
            return false;

        // An .app outside VentureOS's rootfs is offered as a project, where work in progress belongs.
        if (leaving || Bundle is null)
        {
            if (OpenBundle.Find(file) is { } bundle && IsStrayApp(bundle) && await AskToMakeProject(bundle))
                return MakeProject(bundle) is not null;
        }

        Open(file);
        return true;
    }

    /// <summary>Lists the open bundle's files, or hides the list while a document outside one is open.</summary>
    private void ShowFiles()
    {
        var showing = Bundle is not null;
        FilesPane.IsVisible = FilesSplitter.IsVisible = showing;
        LeftPane.RowDefinitions[0].Height = showing ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        syncing = true;
        FileTree.ItemsSource = Bundle is { } open ? (BundleItem[])[open.Tree()] : null;
        syncing = false;
    }

    /// <summary>Selects a file in the list, as the file shown.</summary>
    private void SelectInFiles(string file)
    {
        if (FileTree.ItemsSource is not BundleItem[] { Length: 1 } items)
            return;

        var top = items[0];

        syncing = true;
        FileTree.SelectedItem = Find(top);
        syncing = false;

        BundleItem? Find(BundleItem item) =>
            item.Path == file ? item : item.Children.Select(Find).FirstOrDefault(found => found is not null);
    }

    private void OnFileSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (syncing || FileTree.SelectedItem is not BundleItem { IsFolder: false } item || item.Path == CurrentFile)
            return;

        // Leaving an untitled interface with changes asks first, as leaving a bundle does.
        if (UntitledChanged)
            _ = LeaveUntitledThenShowAsync(item.Path);
        else
            ShowFile(item.Path);
    }

    /// <summary>
    /// Saves the text exactly as it is in the editor, even text VentureOS would refuse, with the byte order mark the
    /// file had; a file nobody edited is saved byte for byte.
    /// </summary>
    /// <returns>Whether it was saved.</returns>
    public bool Save()
    {
        // An untitled interface is asked where it goes, which takes a moment, so Save returns before it's written.
        if (IsUntitled)
        {
            _ = SaveUntitledAsync();
            return false;
        }

        KeepEdit();

        // A bundle's file other than the designer's is saved from the bundle, whichever editor shows it.
        if (path is null && Bundle is { } open && CurrentFile is { } file)
        {
            try
            {
                open.Save(file);
                Say($"Saved {Path.GetFileName(file)}.", NLog.LogLevel.Info);
                ShowUnsaved();
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or EncoderFallbackException)
            {
                Say($"{Path.GetFileName(file)} couldn't be saved: {exception.Message}", NLog.LogLevel.Warn);
                return false;
            }
        }

        if (path is null)
            return false;

        try
        {
            var document = Read();
            document.Save(path);
            Bundle?.Edit(path, document.Text);
            Say($"Saved {Path.GetFileName(path)}.", NLog.LogLevel.Info);
            ShowUnsaved();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or EncoderFallbackException)
        {
            Say($"{Path.GetFileName(path)} couldn't be saved: {exception.Message}", NLog.LogLevel.Warn);
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
        if (infoPath is not null)
        {
            ReadInfo();
            return;
        }

        if (path is null)
            return;

        var document = Read();
        KeepEdit();
        ShowUnsaved();
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
        StatusText.Text = IsUntitled
            ? "Untitled: VentureOS reads it, and its images and strings are found once it's saved."
            : problems.Count == 0 ? "VentureOS reads this document." : string.Join(" ", problems);
    }

    /// <summary>Draws a document VentureOS reads onto the canvas, with the selection over it; the text isn't touched.</summary>
    /// <returns>What couldn't be loaded, such as a missing image.</returns>
    private List<string> Draw(UIDocument document, UIScript script)
    {
        var file = path ?? throw new InvalidOperationException("Only an open document is drawn.");
        List<string> problems = [];
        // The tables as edited, so a change to one shows when the document is drawn again.
        var folder = Path.GetDirectoryName(file) ?? "/";
        var view = DocumentView.Open(
            script, folder, new StringTables(folder, sharedStrings, ReadEdited()), image => SkiaImages.Load(image), problems.Add);
        ShowPane(view, script);
        var menus = MenusFor(document, script, file);
        using var painter = RenderCommand.Draw(view, Path.GetFileName(file), fonts, out var drawn, menus);
        PreviewImage.Source = ToBitmap(painter);
        Map = new DesignMap(document, drawn);
        MenusShown = menus;
        ShowSelection();
        return problems;
    }

    /// <summary>
    /// The menu bar shown along the top of the canvas, as Xcode shows an app's menus over its design, or
    /// <see langword="null"/> while the document has no menus.
    /// </summary>
    public MenuBar? MenusShown { get; private set; }

    /// <summary>
    /// The bar for a document with menus: the app's own menu, named in bold as VentureOS shows it, then the
    /// document's menus in order. It's given new menus only when the document changes, so a menu stays open while
    /// the canvas redraws around it.
    /// </summary>
    private MenuBar? MenusFor(UIDocument document, UIScript script, string file)
    {
        if (script.Menus.Count == 0)
            return null;
        if (ReferenceEquals(menusOf, document))
            return documentMenus;

        var app = SimulatedRun.BundleOf(file) is { } bundle ? Path.GetFileNameWithoutExtension(bundle) : Path.GetFileNameWithoutExtension(file);
        List<BarMenu> menus =
        [
            new(new UIMenu("app", app, null, [new UIMenuEntry("quit", $"Quit {app}", 'Q', new UIShortcut(true, false, false, 'Q'), 0)], 0),
                MenuGroup.App, Emphasized: true),
        ];
        menus.AddRange(script.Menus.Select(menu => new BarMenu(menu, MenuGroup.Window)));
        documentMenus.SetMenus(menus);
        menusOf = document;
        return documentMenus;
    }

    /// <summary>Draws the canvas again from the document as it is, for a menu opening or closing.</summary>
    private void RedrawCanvas()
    {
        if (Document is { Script: { } script } document)
            Draw(document, script);
    }

    /// <summary>
    /// Hands the menu bar a press, move, or release on the canvas: on the bar, or anywhere while a menu is open.
    /// Opening a menu selects its <c>&lt;Menu&gt;</c>; choosing an item selects its <c>&lt;MenuItem&gt;</c>.
    /// </summary>
    /// <returns>Whether the menu bar took it, so the designer doesn't handle it as well.</returns>
    private bool MenuPointer(Point point, bool pressed, bool released)
    {
        if (MenusShown is not { } bar || (!menuPointer && !bar.IsOpen && !(pressed && point.Y < MenuBar.Height)))
            return false;

        var wasOpen = bar.IsOpen;
        var choice = bar.UpdatePointer((int)point.X, (int)point.Y, pressed, released);
        var children = Document!.Xml!.Children;
        if (choice is { } chosen && chosen.Menu.Group == MenuGroup.Window)
        {
            // Names are unique in a document, so the item is found by its name, inside its menu or naming it.
            Select(Index(children, child => child.Name == "MenuItem" && child["name"] == chosen.Item.Name));
        }
        else if (pressed && bar.OpenMenu is { Group: MenuGroup.Window } opened)
        {
            Select(Index(children, child => child.Name == "Menu" && child["name"] == opened.Menu.Name));
        }

        RedrawCanvas();

        // A press outside an open menu only closed it; the designer handles the press as well, as a click there.
        if (pressed && wasOpen && !bar.IsOpen && point.Y >= MenuBar.Height)
            return false;
        return true;

        static int? Index(IReadOnlyList<Venture.OS.Interface.Xml.FlatXmlElement> elements, Func<Venture.OS.Interface.Xml.FlatXmlElement, bool> match)
        {
            for (var index = 0; index < elements.Count; index++)
            {
                if (match(elements[index]))
                    return index;
            }

            return null;
        }
    }

    /// <summary>
    /// Adds a control or another part of the window from the library, and selects it. A control goes in front of
    /// everything, in the pane on show; a menu item joins the selected menu; a menu, sidebar, or pane adds the feature
    /// it needs.
    /// </summary>
    /// <returns>Whether it was added; when it wasn't, the status line says why.</returns>
    public bool AddControl(string element) =>
        Edit(document => DesignEdits.Add(document, element, Selection, PaneOnShow(document)));

    /// <summary>The pane the canvas shows of <paramref name="document"/>'s panes, or <see langword="null"/> without any.</summary>
    private string? PaneOnShow(UIDocument document)
    {
        if (document.Script is not { Panes.Count: > 0 } script)
            return null;
        foreach (var pane in script.Panes)
        {
            if (pane.Name == ShownPane)
                return pane.Name;
        }

        return script.Panes[0].Name;
    }

    /// <summary>Copies the selection just in front of it, set off a little, and selects the copy.</summary>
    /// <returns>Whether it was copied; when it wasn't, the status line says why.</returns>
    public bool DuplicateSelection() =>
        Selection is { } element && Edit(document => DesignEdits.Duplicate(document, element));

    /// <summary>Deletes the selection with the comments above it, leaving nothing selected.</summary>
    /// <returns>Whether it was deleted; when it wasn't, such as the window, the status line says why.</returns>
    public bool DeleteSelection() =>
        Selection is { } element && Edit(document => (document.RemoveElement(element), null));

    /// <summary>Moves the selection into the menu or pane just before it, as the last thing there, and keeps it selected.</summary>
    /// <returns>Whether it moved; when it didn't, the status line says why.</returns>
    public bool MoveIntoSelection() =>
        Selection is { } element && Edit(document => DesignEdits.MoveInto(document, element));

    /// <summary>Moves the selection out of its menu or pane, to just after it, and keeps it selected.</summary>
    /// <returns>Whether it moved; when it didn't, the status line says why.</returns>
    public bool MoveOutSelection() =>
        Selection is { } element && Edit(document => DesignEdits.MoveOut(document, element));

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

            // Clear the selection first, so the redraw never shows the old index on a different element. The outline
            // keeps the keyboard through an edit made from it, such as Tab, so the next key carries on there.
            var fromOutline = OutlineList.IsKeyboardFocusWithin;
            Selection = null;
            Apply(edited);
            Select(select);
            if (fromOutline && !OutlineList.IsKeyboardFocusWithin)
                FocusOutlineRow();
            return true;
        }
        catch (Exception exception) when (exception is UIScriptException or ArgumentException)
        {
            Say($"VentureOS wouldn't read that: {exception.Message}", NLog.LogLevel.Warn);
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

        // A tree: each element indented by its depth, a menu or pane that holds anything marked open or folded, and
        // what's in a folded one left out.
        List<int> rows = [];
        List<string> labels = [];
        for (var index = 0; xml is not null && index < xml.Children.Count;)
        {
            var row = xml.Children[index];
            var holds = xml.End(index) > index + 1;
            rows.Add(index);
            labels.Add(new string(' ', 4 * row.Depth) + (holds ? IsFolded(index) ? "▸ " : "▾ " : "") + OutlineLabel(row));
            index = holds && IsFolded(index) ? xml.End(index) : index + 1;
        }

        outlineRows = [.. rows];
        syncing = true;

        // New rows replace the old ones, the focused one too, so the keyboard goes back to the selected row, letting
        // Left, Right, and Tab carry on in the outline.
        var keyboard = OutlineList.IsKeyboardFocusWithin;
        if (OutlineList.ItemsSource is not string[] shown || !shown.SequenceEqual(labels))
            OutlineList.ItemsSource = labels.ToArray();
        OutlineList.SelectedIndex = Selection is { } chosen ? Array.IndexOf(outlineRows, chosen) : -1;
        if (keyboard && !OutlineList.IsKeyboardFocusWithin)
            FocusOutlineRow();

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
            log.Warn("{File}: {Attribute} refused: {Reason}", Path.GetFileName(path), attribute, exception.Message);
            return exception.Message;
        }
    }

    private void OnOutlineSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (!syncing && OutlineList.SelectedIndex is var index)
            Select(index >= 0 && index < outlineRows.Length ? outlineRows[index] : null);
    }

    // The menus and panes folded in the outline, by name, which survives edits that renumber elements.
    private readonly HashSet<string> folded = new(StringComparer.Ordinal);
    private int[] outlineRows = [];

    /// <summary>Gives the keyboard to the outline's selected row, when there is one.</summary>
    private void FocusOutlineRow()
    {
        OutlineList.UpdateLayout();
        if (OutlineList.SelectedIndex >= 0 && OutlineList.ContainerFromIndex(OutlineList.SelectedIndex) is { } row)
            row.Focus();
    }

    /// <summary>The element each row of the outline shows, top to bottom; what's in a folded menu or pane isn't there.</summary>
    public IReadOnlyList<int> OutlineRows => outlineRows;

    /// <summary>Whether a menu or pane is folded in the outline, hiding what it holds.</summary>
    public bool IsFolded(int element) =>
        Document?.Xml is { } xml && element >= 0 && element < xml.Children.Count
        && xml.Children[element]["name"] is { } name && folded.Contains(name);

    /// <summary>
    /// Folds or unfolds a menu or pane in the outline; anything else, or one holding nothing, stays as it is. A
    /// selection inside what's folded moves to the menu or pane itself.
    /// </summary>
    public void SetFolded(int element, bool fold)
    {
        if (Document?.Xml is not { } xml || element < 0 || element >= xml.Children.Count
            || xml.End(element) == element + 1 || xml.Children[element]["name"] is not { } name)
            return;

        if (fold)
            folded.Add(name);
        else
            folded.Remove(name);

        if (fold && Selection is { } selected && selected > element && selected < xml.End(element))
            Select(element);
        else
            ShowPanels();
    }

    /// <summary>
    /// The outline's own keys: Left folds a menu or pane or goes up to the one holding the selection, Right unfolds,
    /// and Tab and Shift+Tab move the selection into the menu or pane before it and back out, as an outliner indents.
    /// </summary>
    private void OnOutlineKeyDown(object? sender, KeyEventArgs e)
    {
        if (Selection is not { } element || Document?.Xml is not { } xml)
            return;

        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var holds = xml.End(element) > element + 1;
        switch (e.Key)
        {
            case Key.Left when holds && !IsFolded(element):
                SetFolded(element, true);
                break;
            case Key.Left when xml.Children[element].Parent >= 0:
                Select(xml.Children[element].Parent);
                break;
            case Key.Right when holds && IsFolded(element):
                SetFolded(element, false);
                break;
            case Key.Tab when shift:
                MoveOutSelection();
                break;
            case Key.Tab:
                MoveIntoSelection();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    /// <summary>Whether the designer can change an element: one with a frame, in a document VentureOS reads as drawn.</summary>
    private bool CanEdit(int element) => Kind(element) is { } kind && FrameEdit.IsFramed(kind);

    /// <summary>The handles the selected element can be resized by on the canvas: none when it can't be.</summary>
    private IReadOnlyList<Handle> HandlesOf(int element) =>
        Kind(element) is { } kind ? FrameEdit.HandlesFor(kind, Document?.Script?.Sidebar?.Edge == UIEdge.Right) : [];

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
        if (MenuPointer(point, pressed: true, released: false))
        {
            menuPointer = true;
            e.Pointer.Capture(CanvasHost);
            e.Handled = true;
            return;
        }

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
        {
            // Over an open menu, the pointer picks out its items, as VentureOS's menu bar does.
            if (MenusShown is { IsOpen: true } || menuPointer)
                MenuPointer(e.GetPosition(PreviewImage), pressed: false, released: false);
            return;
        }

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
            Say($"VentureOS wouldn't read that: {exception.Message}", NLog.LogLevel.Warn);
        }
    }

    private void OnCanvasReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (menuPointer)
        {
            menuPointer = false;
            e.Pointer.Capture(null);
            MenuPointer(e.GetPosition(PreviewImage), pressed: false, released: true);
            return;
        }

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
        if (e.Key == Key.Escape && MenusShown is { IsOpen: true } bar)
        {
            bar.Close();
            RedrawCanvas();
            e.Handled = true;
            return;
        }

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
                new AppMenuGroup("New",
                [
                    new MenuCommand("New Interface…", () => _ = NewInterfaceAsync(), Ctrl(Key.N, KeyModifiers.Alt)),
                    new MenuCommand("New App…", () => _ = NewAppAsync(), Ctrl(Key.N)),
                    new MenuCommand("New Project…", () => _ = NewProjectAsync(), Ctrl(Key.N, KeyModifiers.Shift)),
                ]),
                new MenuSeparator(),
                new MenuCommand("Open…", () => _ = PickAndOpenAsync(), Ctrl(Key.O)),
                new MenuCommand("Open Project or Bundle…", () => _ = PickAndOpenBundleAsync(), Ctrl(Key.O, KeyModifiers.Shift)),
                new MenuCommand("Open as Project…", () => _ = PickAndMakeProjectAsync()),
                new MenuSeparator(),
                new MenuCommand("Export…", () => _ = ExportAsync(), IsEnabled: () => Project?.HasApp == true),
                new MenuSeparator(),
                new MenuCommand("Close", () => _ = CloseOpenAsync(), Ctrl(Key.W), () => !NothingOpen),
                new MenuCommand("Welcome to Roundhouse", () => ShowWelcome()),
                new MenuCommand("Save", () => Save(), Ctrl(Key.S), () => HasDocument() || FileEditor.IsVisible),
                new MenuCommand("Save All", () => SaveAll(), Ctrl(Key.S, KeyModifiers.Shift), () => Bundle is not null || HasDocument()),
                new MenuSeparator(),
                new MenuCommand("Quit", Close, Ctrl(Key.Q)),
            ]),
            new("Edit",
            [
                new MenuCommand("Undo", Undo, Ctrl(Key.Z), () => ActiveEditor.CanUndo),
                new MenuCommand("Redo", Redo, Ctrl(Key.Z, KeyModifiers.Shift), () => ActiveEditor.CanRedo),
                new MenuSeparator(),
                new MenuCommand("Duplicate", () => DuplicateSelection(), Ctrl(Key.D), Selected),
                new MenuCommand("Delete", () => DeleteSelection(), new KeyGesture(Key.Delete), Selected),
                new MenuSeparator(),
                new MenuCommand("Move Into", () => MoveIntoSelection(), IsEnabled: () =>
                    Editable() && Selection is { } element && DesignEdits.MoveIntoTarget(Document!, element) is not null),
                new MenuCommand("Move Out", () => MoveOutSelection(), IsEnabled: () =>
                    Editable() && Selection is { } element && element < Document!.Xml!.Children.Count
                    && Document.Xml.Children[element].Parent >= 0),
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
                new MenuCommand("Run in Simulation", Run, Ctrl(Key.R)),
                new MenuCommand("Restart", RestartRun, Ctrl(Key.R, KeyModifiers.Shift), () => Simulator is not null),
                new MenuCommand("Stop", StopRun, Ctrl(Key.OemPeriod), () => Simulator is not null),
                new MenuSeparator(),
                new MenuCommand("Run on VentureOS", () => RunOnVentureOS(), Ctrl(Key.R, KeyModifiers.Alt), () => AppToSend() is not null),
                new MenuSeparator(),
                new MenuCommand("Test", () => Test(), Ctrl(Key.U), () => Project is { HasApp: true } && Testing is null),
            ]),
        ];
    }

    /// <summary>Whether the coloured bar is the window's title bar.</summary>
    public bool IsTitleBarMerged => TitleBar.IsMerged;

    /// <summary>Whether Roundhouse draws the window's buttons in the bar: where the bar is the title bar on Linux.</summary>
    public bool DrawsWindowButtons => TitleBar.DrawsWindowButtons;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == NativeMenu.IsNativeMenuExportedProperty && MenuButton is not null)
            MenuButton.IsVisible = !NativeMenu.GetIsNativeMenuExported(this);
    }

    private void OnMenuClicked(object? sender, RoutedEventArgs e)
    {
        var flyout = AppMenu.Flyout(Menus);
        flyout.Placement = PlacementMode.BottomEdgeAlignedLeft;
        flyout.ShowAt(MenuButton);
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

        if (RunTarget("simulation", needsApp: false) is not var (document, edits))
            return;

        var simulator = new SimulatorWindow(document, edits);
        Simulator = simulator;
        simulator.Closed += (_, _) =>
        {
            if (ReferenceEquals(Simulator, simulator))
                Simulator = null;
        };
        simulator.Show();
        log.Info("Run started{Edits}", UnsavedCount() is var count and > 0 ? $", with {count} unsaved {(count == 1 ? "edit" : "edits")}" : "");
    }

    /// <summary>The project's tests, while they run.</summary>
    public ProjectTests? Testing { get; private set; }

    /// <summary>Tells a test when the tests that were running have finished; tests use it to wait.</summary>
    public event EventHandler? TestingFinished;

    private RoundhouseProject? testedProject;
    private int testsPassed;
    private int testsFailed;
    private static readonly NLog.Logger testLog = RoundhouseLog.For("Test");

    /// <summary>
    /// Runs the open project's tests, its <c>Tests/*.lua</c>, against its app as it's edited, saved or not, as
    /// <c>airo test</c> does. Each result goes to the Log as it comes, one a turn of the window's loop, so Roundhouse
    /// stays usable; the counts go in the status line at the end.
    /// </summary>
    /// <returns>Whether the tests started; they don't without a project with an app and tests, or while they run.</returns>
    public bool Test()
    {
        if (Testing is not null)
        {
            Say("The tests are still running.", NLog.LogLevel.Info);
            return false;
        }

        if (Project is not { } project || !project.HasApp)
        {
            Say(Project is { } open
                ? $"{Path.GetFileName(open.Root)} has no app yet; add one with New App."
                : "Test runs a project's tests, and what's open isn't a project.", NLog.LogLevel.Warn);
            return false;
        }

        KeepEdit();
        Dictionary<string, string> edits = [];
        if (Bundle is { } bundle)
        {
            foreach (var file in bundle.Unsaved)
                edits[file] = bundle.Text(file);
        }

        // The designer's document, as Run takes it.
        if (path is not null && !edits.ContainsKey(Path.GetFullPath(path)))
            edits[Path.GetFullPath(path)] = byteOrderMark + Editor.Text;

        if (ProjectTests.Files(project, edits).Count == 0)
        {
            Say($"{Path.GetFileName(project.Root)} has no tests: there are no .lua files in its {ProjectTests.FolderName}/ folder.", NLog.LogLevel.Warn);
            return false;
        }

        if (SimulatedDisk.FindRootfs() is not { } rootfs)
        {
            Say("VentureOS's files (rootfs/) aren't beside Roundhouse.", NLog.LogLevel.Error);
            return false;
        }

        try
        {
            Testing = ProjectTests.Start(project, rootfs, SimulatedRun.Version, new LoggedOutput(new NoOutput(), testLog), edits);
        }
        catch (ProjectException exception)
        {
            Say(exception.Message, NLog.LogLevel.Warn);
            return false;
        }

        testedProject = project;
        testsPassed = 0;
        testsFailed = 0;
        BottomTabs.SelectedItem = LogTab;
        Say($"Testing {Path.GetFileName(project.Root)}…", NLog.LogLevel.Info);
        Dispatcher.UIThread.Post(TestStep, DispatcherPriority.Background);
        return true;
    }

    // Runs the next case and logs it, then lets the window draw before the one after; a project closed or changed
    // in between ends the run.
    private void TestStep()
    {
        if (Testing is not { } tests)
            return;

        var name = Path.GetFileName(testedProject!.Root);
        if (!ReferenceEquals(Project, testedProject))
        {
            EndTests($"{name}'s tests stopped: {testsPassed} passed, {testsFailed} failed before then.", NLog.LogLevel.Warn);
            return;
        }

        AppTestResult? result;
        try
        {
            result = tests.Next();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ProjectException)
        {
            EndTests($"{name}'s tests stopped: {exception.Message}", NLog.LogLevel.Error);
            return;
        }

        if (result is null)
        {
            EndTests($"{name}: {testsPassed} passed, {testsFailed} failed", testsFailed == 0 ? NLog.LogLevel.Info : NLog.LogLevel.Warn);
            return;
        }

        if (result.Passed)
            testsPassed++;
        else
            testsFailed++;
        testLog.Log(result.Passed ? NLog.LogLevel.Info : NLog.LogLevel.Warn, result.Describe());
        Dispatcher.UIThread.Post(TestStep, DispatcherPriority.Background);
    }

    private void EndTests(string message, NLog.LogLevel level)
    {
        Testing?.Dispose();
        Testing = null;
        testedProject = null;
        Say(message, level);
        TestingFinished?.Invoke(this, EventArgs.Empty);
    }

    // The tests' own print goes to the log alone.
    private sealed class NoOutput : IShellOutput
    {
        public void WriteLine(string text)
        {
        }

        public void Clear()
        {
        }
    }

    /// <summary>The window of the run on VentureOS, while one is open.</summary>
    public Vdb.VentureOSRunWindow? VentureOSRun { get; private set; }

    /// <summary>How a run on VentureOS connects; tests give it their own.</summary>
    public Func<Vdb.VentureOSRun> VentureOSRunner { get; set; } = () => Vdb.VentureOSRun.OverTcp();

    /// <summary>
    /// What Run on VentureOS would send: the open project's app, or the <c>.app</c> the open file is in; or
    /// <see langword="null"/> when there's none.
    /// </summary>
    private string? AppToSend() =>
        Project is { } project ? (project.HasApp ? project.Root : null) : CurrentFile is { } file ? SimulatedRun.BundleOf(file) : null;

    /// <summary>
    /// Sends the app being edited, with every unsaved edit, to a running VentureOS over vdb and opens it, in a window
    /// that shows what it prints. Running it again ends the last run first.
    /// </summary>
    /// <returns>Whether a run started; it doesn't when there's no app to send.</returns>
    public bool RunOnVentureOS()
    {
        if (AppToSend() is null)
        {
            Say(Project is { } project
                ? $"{Path.GetFileName(project.Root)} has no app yet; add one with New App."
                : "Run on VentureOS sends an app, and the open file isn't in one.", NLog.LogLevel.Warn);
            return false;
        }

        if (RunTarget("venture", needsApp: true) is not var (document, edits) || SimulatedRun.BundleOf(document!) is not { } app)
            return false;

        VentureOSRun?.Close();
        var runner = VentureOSRunner();
        var name = Path.GetFileName(app);
        var window = new Vdb.VentureOSRunWindow(name,
            (report, stop) => Task.Run(() => runner.Run(app, edits, report, stop), CancellationToken.None));
        VentureOSRun = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(VentureOSRun, window))
                VentureOSRun = null;
        };
        window.Show(this);
        log.Info("Run on VentureOS started for {App}{Edits}", name,
            UnsavedCount() is var count and > 0 ? $", with {count} unsaved {(count == 1 ? "edit" : "edits")}" : "");
        return true;
    }

    // How many files run with edits that aren't saved, for the log.
    private int UnsavedCount() => Bundle?.Unsaved.Count ?? (path is not null && UnsavedFiles().Count > 0 ? 1 : 0);

    /// <summary>
    /// What a run starts from: the document to open, and the edits to run in place of what's saved. A bundle or a
    /// document outside one runs as it is, with its edits. A project's app is assembled into an <c>.app</c> in its
    /// own folder for <paramref name="kind"/> of run, edits written in, and opens at the document being designed,
    /// or its <c>Info.xml</c>; a loose interface in a project runs as a window of its own, unless
    /// <paramref name="needsApp"/>.
    /// </summary>
    /// <returns>The document and edits, or <see langword="null"/> when there's nothing to run, which is said.</returns>
    private (string? Document, IReadOnlyDictionary<string, string> Edits)? RunTarget(string kind, bool needsApp)
    {
        // Every file's edits run, saved or not: the bundle's, or the document's own outside one.
        KeepEdit();
        Dictionary<string, string> edits = [];
        if (Bundle is { } bundle)
        {
            foreach (var file in bundle.Unsaved)
                edits[file] = bundle.Text(file);
        }
        if (path is not null && !edits.ContainsKey(Path.GetFullPath(path)))
        {
            edits[Path.GetFullPath(path)] = byteOrderMark + Editor.Text;
        }

        // An untitled interface runs as a window of its own, from its text, wherever it's going.
        if (Project is not { } project || IsUntitled)
            return (CurrentFile, edits);

        var loose = CurrentFile is { } shown && IsInside(shown, project.InterfacesFolder);
        if (loose && !needsApp)
            return (CurrentFile, edits);
        if (!project.HasApp)
        {
            Say($"{Path.GetFileName(project.Root)} has no app to run yet; add one with New App, or open one of its interfaces.", NLog.LogLevel.Warn);
            return null;
        }

        string app;
        try
        {
            app = project.Assemble(Path.Combine(runCopies, kind), edits, replace: true);
        }
        catch (Exception exception) when (exception is ProjectException or IOException or UnauthorizedAccessException)
        {
            Say($"{Path.GetFileName(project.Root)}'s app couldn't be made to run: {exception.Message}", NLog.LogLevel.Warn);
            return null;
        }

        // The run opens at the window being designed, as an .app's does, in the copy that's running.
        var document = CurrentFile is { } current && IsInside(current, project.AppFolder)
            ? Path.Combine(app, "Contents", Path.GetRelativePath(project.AppFolder, current))
            : Path.Combine(app, "Contents", "Info.xml");
        return (document, new Dictionary<string, string>());
    }

    private static bool IsInside(string file, string folder) =>
        Path.GetFullPath(file).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    /// <summary>Ends the run, deleting its copy of VentureOS's files.</summary>
    public void StopRun()
    {
        if (Simulator is not { } running)
            return;
        running.Close();
        log.Info("Run stopped");
    }

    /// <summary>Ends the run and starts a fresh one, with the document as it's edited now.</summary>
    public void RestartRun()
    {
        StopRun();
        Run();
    }

    // The bar's Run opens a menu of where to run, the Run menu's two, so every run picks where.
    private void OnRunClicked(object? sender, RoutedEventArgs e)
    {
        var where = AppMenu.Flyout(Menus.Single(group => group.Title == "Run").Entries
            .OfType<MenuCommand>()
            .Where(command => command.Title is "Run in Simulation" or "Run on VentureOS")
            .ToList<MenuEntry>());
        where.Placement = PlacementMode.BottomEdgeAlignedRight;
        where.ShowAt(RunButton);
    }

    /// <summary>What a document belongs to, shown after its name: the app or project it's in, such as Calculator.app or Dice.rproj, or its folder.</summary>
    public static string Subtitle(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        for (var folder = Path.GetDirectoryName(path); !string.IsNullOrEmpty(folder); folder = Path.GetDirectoryName(folder))
        {
            if (folder.EndsWith(".app", StringComparison.OrdinalIgnoreCase) || RoundhouseProject.IsProject(folder))
                return Path.GetFileName(folder);
        }

        return Path.GetFileName(Path.GetDirectoryName(path)) ?? "";
    }

    private void ShowUndo()
    {
        UndoButton.IsEnabled = ActiveEditor.CanUndo;
        RedoButton.IsEnabled = ActiveEditor.CanRedo;
    }

    private void ShowCaret()
    {
        var caret = ActiveEditor.TextArea.Caret;
        CaretText.Text = path is null && infoPath is null && !FileEditor.IsVisible ? "" : $"Ln {caret.Line}, Col {caret.Column}";
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
        // A redraw, or a new reading of Info.xml, waits for a pause in typing.
        if (loading || (path is null && infoPath is null))
            return;
        if (infoPath is not null && Bundle is { } open)
        {
            open.Edit(infoPath, byteOrderMark + Editor.Text);
            ShowUnsaved();
        }

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

        BottomTabs.IsVisible = TextSplitter.IsVisible = IsTextShowing;
    }

    private async Task PickAndOpenAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open a project, an app, or a .uix",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Projects, apps, and interfaces") { Patterns = ["*.uix", "Project.xml", "Info.xml"] },
                FilePickerFileTypes.All,
            ],
        });

        if (files is [var file, ..] && file.TryGetLocalPath() is { } local)
            await OpenAsync(Openable(local));
    }

    /// <summary>
    /// What choosing a file opens: a project's <c>Project.xml</c> opens its project, and an app's
    /// <c>Contents/Info.xml</c> its app, since a file picker can't choose a folder; anything else, the file.
    /// </summary>
    public static string Openable(string file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var folder = Path.GetDirectoryName(Path.GetFullPath(file)) ?? "";
        var name = Path.GetFileName(file);
        if (name == RoundhouseProject.FileName && RoundhouseProject.IsProject(folder))
            return folder;
        if (name == "Info.xml" && Path.GetFileName(folder) == "Contents" && Path.GetDirectoryName(folder) is { } bundle
            && bundle.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            return bundle;
        return file;
    }

    /// <summary>Whether nothing is open: no project, bundle, or document.</summary>
    public bool NothingOpen => Bundle is null && path is null && CurrentFile is null;

    /// <summary>The welcome window, while it's showing.</summary>
    public WelcomeWindow? Welcome { get; private set; }

    /// <summary>Whether Roundhouse shows the welcome window when it starts with nothing named.</summary>
    public bool ShowsWelcomeAtStartup => settings.ShowAtStartup;

    /// <summary>
    /// Shows the welcome window, or brings it forward: the ways to start and the recent items. Choosing one shows
    /// this window and does it here; when nothing ends up open, such as a dialog cancelled, the welcome window comes
    /// back. Closing it while this window is hidden quits.
    /// </summary>
    public WelcomeWindow ShowWelcome()
    {
        if (Welcome is { } showing)
        {
            showing.Activate();
            return showing;
        }

        var version = ProductTitle["Roundhouse ".Length..];
        WelcomeWindow welcome = new(
            version,
            [
                new("✎", "New Interface…", "Opens an untitled canvas: draw first, and choose where it goes when you save.", () => FromWelcomeAsync(() =>
                {
                    NewInterface();
                    return Task.CompletedTask;
                })),
                new("▣", "New App…", "Makes a project with an app in it, ready to run.", () => FromWelcomeAsync(NewAppAsync)),
                new("◫", "New Project…", "Makes an empty project, for an app, interfaces, or both.", () => FromWelcomeAsync(NewProjectAsync)),
                new("↥", "Open…", "Opens a project, an app, or a .uix.", () => FromWelcomeAsync(PickAndOpenAsync)),
            ],
            settings.Recent,
            settings.ShowAtStartup,
            show =>
            {
                settings.ShowAtStartup = show;
                SaveSettings();
            },
            file => FromWelcomeAsync(() => OpenAsync(file)),
            file =>
            {
                settings.Forget(file);
                SaveSettings();
            });
        Welcome = welcome;
        welcome.Closed += (_, _) =>
        {
            if (!ReferenceEquals(Welcome, welcome))
                return;
            Welcome = null;

            // Closing the welcome window with nothing else showing is quitting.
            if (!IsVisible)
                Close();
        };
        welcome.Show();
        return welcome;
    }

    // Does what the welcome window chose here, in this window; the welcome window goes once something's open.
    private async Task FromWelcomeAsync(Func<Task> action)
    {
        var welcome = Welcome;
        var wasHidden = !IsVisible;
        Show();
        Activate();
        await action();
        if (welcome is null || !ReferenceEquals(Welcome, welcome))
            return;

        if (NothingOpen && wasHidden)
        {
            Hide();
            welcome.Activate();
            return;
        }

        if (!NothingOpen)
        {
            Welcome = null;
            welcome.Close();
        }
    }

    /// <summary>
    /// Close: asks about unsaved edits, closes the project, bundle, or document open, and shows the welcome window in
    /// this one's place.
    /// </summary>
    /// <returns>Whether it closed; not when Cancel is chosen.</returns>
    public async Task<bool> CloseOpenAsync()
    {
        if (NothingOpen || !await LeaveAsync())
            return false;

        LeaveDesigner(null);
        CurrentFile = null;
        Bundle = null;
        Project = null;
        ShowFiles();
        ShowCenter(CanvasScroller);
        DocumentTitle.Text = "Roundhouse";
        Title = ProductTitle;
        StatusText.Text = "";
        Hide();
        ShowWelcome();
        return true;
    }

    private async Task CloseAfterAskingAsync()
    {
        if (!await LeaveAsync())
            return;
        closingAsked = true;
        Close();
    }

    /// <summary>
    /// Makes a project with a window app from the template, and opens the app's window in the designer: what New App
    /// does with no project open.
    /// </summary>
    /// <returns>The new project's folder.</returns>
    /// <exception cref="ArgumentException">The app couldn't be made, as <see cref="AppTemplate.CreateIn"/> says; nothing is left behind.</exception>
    /// <exception cref="ProjectException">The project couldn't be made, as <see cref="RoundhouseProject.Create"/> says.</exception>
    /// <exception cref="IOException">It couldn't be written.</exception>
    public string CreateApp(string name, string identifier, string folder)
    {
        var project = RoundhouseProject.Create(name, folder);
        try
        {
            AppTemplate.CreateIn(project, name, identifier);
        }
        catch
        {
            Directory.Delete(project.Root, recursive: true);
            throw;
        }

        Open(project.Root);
        Say($"Made {Path.GetFileName(project.Root)}, with its app, in {folder}.", NLog.LogLevel.Info);
        return project.Root;
    }

    /// <summary>
    /// New App: the open project's app, or, with none open, a new project with its app. A project holds one app, so
    /// one that has it says so.
    /// </summary>
    private async Task NewAppAsync()
    {
        if (Project is { } project)
        {
            if (project.HasApp)
            {
                Say($"{Path.GetFileName(project.Root)} already has its app; a project holds one.", NLog.LogLevel.Warn);
                return;
            }

            if (await new NewAppWindow(project).ShowDialog<string?>(this) is not null)
            {
                Open(project.Root);
                Say($"Added an app to {Path.GetFileName(project.Root)}.", NLog.LogLevel.Info);
            }

            return;
        }

        if (!await LeaveAsync())
            return;
        if (await new NewAppWindow(NewAppWindow.SuggestedFolder()).ShowDialog<string?>(this) is { } made)
        {
            Open(made);
            Say($"Made {Path.GetFileName(made)}, with its app.", NLog.LogLevel.Info);
        }
    }

    /// <summary>
    /// New Interface: an untitled canvas, at once, with no questions; Save asks where it goes. An untitled one with
    /// changes, or a document outside a bundle with edits, is asked about first; a bundle's edits are kept, as moving
    /// between its files keeps them.
    /// </summary>
    private async Task NewInterfaceAsync()
    {
        if ((Bundle is null || UntitledChanged) && !await LeaveAsync())
            return;
        NewInterface();
    }

    /// <summary>Opens a blank, untitled interface in the designer, beside whatever project is open.</summary>
    /// <exception cref="IOException">Its text couldn't be kept in Roundhouse's own folder.</exception>
    public void NewInterface()
    {
        KeepEdit();
        Directory.CreateDirectory(Path.GetDirectoryName(UntitledFile)!);
        File.WriteAllText(UntitledFile, BlankInterface);
        ShowFile(UntitledFile);
        SelectInFiles("");
        log.Info("New untitled interface");
    }

    /// <summary>Asks where the untitled interface goes, and saves it there.</summary>
    /// <returns>Whether it was saved.</returns>
    public async Task<bool> SaveUntitledAsync()
    {
        if (!IsUntitled)
            return false;
        return await AskWhereToSave() is { } target && SaveUntitledTo(target);
    }

    /// <summary>
    /// Writes the untitled interface to <paramref name="target"/>, which mustn't exist yet, and opens it there: in its
    /// project or bundle when it's in one, so its images and strings are found, or as a document of its own.
    /// </summary>
    /// <returns>Whether it was saved; why not is said.</returns>
    public bool SaveUntitledTo(string target)
    {
        ArgumentException.ThrowIfNullOrEmpty(target);
        if (!IsUntitled)
            return false;

        target = Path.GetFullPath(target);
        try
        {
            if (File.Exists(target))
                throw new IOException($"There's already {Path.GetFileName(target)} there.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, byteOrderMark + Editor.Text);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Say($"The interface couldn't be saved to {target}: {exception.Message}", NLog.LogLevel.Warn);
            return false;
        }

        // Let go of the untitled text first, so opening the saved file doesn't ask about it.
        LeaveDesigner(null);
        CurrentFile = null;
        File.Delete(UntitledFile);
        Open(target);
        Say($"Saved {Path.GetFileName(target)}.", NLog.LogLevel.Info);
        return true;
    }

    // Leaving an untitled interface for another file: asks, then shows it, or goes back to the untitled one.
    private async Task LeaveUntitledThenShowAsync(string file)
    {
        if (await LeaveAsync())
        {
            if (IsUntitled)
                ShowFile(file);
            else if (Bundle?.Contains(file) == true)
                ShowFile(file);
        }
        else
        {
            SelectInFiles("");
        }
    }

    /// <summary>New Project: an empty project, named and placed, opened.</summary>
    private async Task NewProjectAsync()
    {
        if (!await LeaveAsync())
            return;
        if (await new NewAppWindow(NewAppWindow.SuggestedFolder(), withApp: false).ShowDialog<string?>(this) is { } made)
        {
            Open(made);
            Say($"Made {Path.GetFileName(made)}.", NLog.LogLevel.Info);
        }
    }

    /// <summary>Chooses a project's or bundle's folder, such as a .rproj or an .app, and opens it.</summary>
    private async Task PickAndOpenBundleAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open a project or bundle, such as a .rproj or an .app",
            AllowMultiple = false,
        });

        if (folders is [var folder, ..] && folder.TryGetLocalPath() is { } local)
            await OpenAsync(local);
    }

    /// <summary>Asks before replacing an app that's there, by its folder; tests answer it themselves.</summary>
    public Func<string, Task<bool>> AskToReplace { get; set; }

    /// <summary>Offers to make a stray app a project, by its folder; tests answer it themselves.</summary>
    public Func<string, Task<bool>> AskToMakeProject { get; set; }

    /// <summary>
    /// Makes a project from an <c>.app</c>, beside it, and opens it; the <c>.app</c> is left as it was.
    /// </summary>
    /// <returns>The project's folder, or <see langword="null"/> when it couldn't be made, which is said.</returns>
    public string? MakeProject(string app)
    {
        try
        {
            var project = RoundhouseProject.FromApp(app);
            Open(project.Root);
            Say($"Made {Path.GetFileName(project.Root)} from {Path.GetFileName(app)}, which is left as it was.", NLog.LogLevel.Info);
            return project.Root;
        }
        catch (Exception exception) when (exception is ProjectException or IOException or UnauthorizedAccessException)
        {
            Say($"{Path.GetFileName(Path.TrimEndingDirectorySeparator(app))} couldn't be made a project: {exception.Message}", NLog.LogLevel.Warn);
            return null;
        }
    }

    /// <summary>Open as Project: chooses an .app and makes a project from it.</summary>
    private async Task PickAndMakeProjectAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose an .app to make a project from",
            AllowMultiple = false,
        });

        if (folders is [var folder, ..] && folder.TryGetLocalPath() is { } local && await LeaveAsync())
            MakeProject(local);
    }

    /// <summary>Whether an .app is a stray: outside any <c>rootfs/</c>, where VentureOS's apps are installed from.</summary>
    public static bool IsStrayApp(string bundle) =>
        bundle.EndsWith(".app", StringComparison.OrdinalIgnoreCase)
        && !Path.GetFullPath(bundle).Split(Path.DirectorySeparatorChar).Contains("rootfs");

    /// <summary>
    /// Writes the open project's app, as edited, to <paramref name="folder"/> as an <c>.app</c>, asking before
    /// replacing one that's there.
    /// </summary>
    /// <returns>The <c>.app</c>'s folder, or <see langword="null"/> when there's no app, it's not to be replaced, or it couldn't be written, which is said.</returns>
    public async Task<string?> ExportTo(string folder)
    {
        if (Project is not { HasApp: true } project)
        {
            Say("Export writes a project's app, and there's none open.", NLog.LogLevel.Warn);
            return null;
        }

        KeepEdit();
        Dictionary<string, string> edits = [];
        foreach (var file in Bundle!.Unsaved)
            edits[file] = Bundle.Text(file);

        try
        {
            var target = Path.Combine(folder, project.AppFolderName(edits));
            var replace = Directory.Exists(target) || File.Exists(target);
            if (replace && !await AskToReplace(target))
                return null;

            var app = project.Assemble(folder, edits, replace);
            Say($"Exported {Path.GetFileName(app)} to {folder}.", NLog.LogLevel.Info);
            return app;
        }
        catch (Exception exception) when (exception is ProjectException or IOException or UnauthorizedAccessException)
        {
            Say($"{Path.GetFileName(project.Root)}'s app couldn't be exported: {exception.Message}", NLog.LogLevel.Warn);
            return null;
        }
    }

    /// <summary>Export: chooses where, such as VentureOS's rootfs/user/apps, and writes the project's app there.</summary>
    private async Task ExportAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Where to put the app, such as VentureOS's rootfs/user/apps",
            AllowMultiple = false,
        });

        if (folders is [var folder, ..] && folder.TryGetLocalPath() is { } local)
            await ExportTo(local);
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
