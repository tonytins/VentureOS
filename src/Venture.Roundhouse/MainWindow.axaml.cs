using System.Runtime.InteropServices;
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

    /// <summary>Opens a document and draws it; one that can't be read says why.</summary>
    public void Open(string file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var full = Path.GetFullPath(file);
        string text;
        try
        {
            text = File.ReadAllText(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = $"{file} couldn't be opened: {exception.Message}";
            return;
        }

        path = full;
        loading = true;
        Editor.Text = text;
        loading = false;
        SaveButton.IsEnabled = true;
        Title = $"{Path.GetFileName(full)} — {ProductTitle}";
        RenderNow();
    }

    /// <summary>Saves the text exactly as it is in the editor.</summary>
    /// <returns>Whether it was saved.</returns>
    public bool Save()
    {
        if (path is null)
            return false;

        try
        {
            File.WriteAllText(path, Editor.Text);
            StatusText.Text = $"Saved {Path.GetFileName(path)}.";
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
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

        UIScript script;
        try
        {
            script = UIScript.Parse(Editor.Text);
        }
        catch (UIScriptException exception)
        {
            // The reader's message already starts with the line it stopped on.
            StatusText.Text = $"{exception.Message.TrimEnd('.')}. The canvas shows the last version VentureOS could read.";
            return;
        }

        List<string> problems = [];
        var view = DocumentView.Open(script, Path.GetDirectoryName(path) ?? "/", file => SkiaImages.Load(file), problems.Add);
        using var painter = RenderCommand.Draw(view, Path.GetFileName(path), fonts);
        PreviewImage.Source = ToBitmap(painter);
        StatusText.Text = problems.Count == 0 ? "VentureOS reads this document." : string.Join(" ", problems);
    }

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
