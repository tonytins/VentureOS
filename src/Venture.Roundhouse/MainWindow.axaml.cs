using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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

    public MainWindow()
    {
        InitializeComponent();
        Title = ProductTitle;
        OpenCommand = new Command(() => _ = PickAndOpenAsync());
        SaveCommand = new Command(() => Save());
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.O, KeyModifiers.Control), Command = OpenCommand });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.S, KeyModifiers.Control), Command = SaveCommand });

        // The switches start on VentureOS's standard look, which the canvas shows until they change.
        DesktopAppearance.Use(DesktopLook.Standard);
        loading = true;
        AccentBox.ItemsSource = AccentNames;
        AccentBox.SelectedIndex = 0;
        loading = false;

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
        Closed += (_, _) => fonts.Dispose();
    }

    /// <summary>The window's title: Roundhouse and its version, such as <c>Roundhouse 0.6.101</c>.</summary>
    public static string ProductTitle => $"Roundhouse {ThisAssembly.Git.SemVer.Major}.{ThisAssembly.Git.SemVer.Minor}.{ThisAssembly.Git.SemVer.Patch}";

    /// <summary>Ctrl+O: choose a document and open it.</summary>
    public ICommand OpenCommand { get; }

    /// <summary>Ctrl+S: save the text as it is.</summary>
    public ICommand SaveCommand { get; }

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

    /// <summary>Selects an element, by its index among the root's children, or nothing.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The document has no such element.</exception>
    public void Select(int? element)
    {
        if (element is { } index && (Document?.Xml is not { } xml || index < 0 || index >= xml.Children.Count))
            throw new ArgumentOutOfRangeException(nameof(element), element, "The document has no such element.");
        Selection = element;
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
        SaveButton.IsEnabled = true;
        Title = $"{Path.GetFileName(full)} — {ProductTitle}";
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
        if (document.Script is not { } script)
        {
            // The reader's message already starts with the line it stopped on.
            StatusText.Text = $"{document.Problem!.Message.TrimEnd('.')}. The canvas shows the last version VentureOS "
                + "could read, and only the text can be edited until VentureOS reads it.";
            return;
        }

        List<string> problems = [];
        var view = DocumentView.Open(script, Path.GetDirectoryName(path) ?? "/", file => SkiaImages.Load(file), problems.Add);
        using var painter = RenderCommand.Draw(view, Path.GetFileName(path), fonts, out var drawn);
        PreviewImage.Source = ToBitmap(painter);
        Map = new DesignMap(document, drawn);
        if (Selection >= document.Xml!.Children.Count)
            Selection = null;
        StatusText.Text = problems.Count == 0 ? "VentureOS reads this document." : string.Join(" ", problems);
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
        redraw.Stop();
        redraw.Start();
    }

    private void OnLookChanged(object? sender, RoutedEventArgs e)
    {
        if (loading || AccentBox is null || DarkBox is null)
            return;

        var accent = DesktopPalette.Swatches[Math.Max(0, AccentBox.SelectedIndex)];
        DesktopAppearance.Use(DesktopLook.Standard with
        {
            Mode = DarkBox.IsChecked == true ? LookMode.Dark : LookMode.Light,
            Accent = accent,
        });
        RenderNow();
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

    private void OnOpenClicked(object? sender, RoutedEventArgs e) => _ = PickAndOpenAsync();

    private void OnSaveClicked(object? sender, RoutedEventArgs e) => Save();

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
