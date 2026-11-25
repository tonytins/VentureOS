using Venture.Airo.Simulation;
using Venture.OS.Desktop;
using Venture.OS.Interface;
using Venture.Roundhouse.Drawing;
using Venture.Roundhouse.Simulator;

namespace Venture.Roundhouse;

/// <summary>
/// <c>Roundhouse --render document.uix picture.png</c>: draws a document as VentureOS would show it in a window, over
/// the sky, into a PNG file, with no window of Roundhouse's own, so a script or a test can look at a design.
/// </summary>
public static class RenderCommand
{
    /// <summary>The flag that asks for a render, first on the command line.</summary>
    public const string Flag = "--render";

    /// <summary>What it takes.</summary>
    public const string Usage =
        "Usage: Roundhouse --render <document.uix> <picture.png> [--dark] [--accent #RRGGBB] [--scrollbars classic|automatic]";

    // Room around the window for its shadow and a little of the sky.
    private const int Margin = 24;

    /// <summary>Renders, saying what went wrong on <paramref name="error"/>.</summary>
    /// <param name="arguments">What follows <see cref="Flag"/>.</param>
    /// <returns>0 when the picture was written, 1 when the document or picture couldn't be, and 2 for a wrong command.</returns>
    public static int Run(IReadOnlyList<string> arguments, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(error);

        List<string> paths = [];
        var dark = false;
        var accent = DesktopPalette.StandardAccent;
        var bars = ScrollBarStyle.Classic;
        for (var index = 0; index < arguments.Count; index++)
        {
            switch (arguments[index])
            {
                case "--dark":
                    dark = true;
                    break;
                case "--accent" when index + 1 < arguments.Count && DesktopPalette.TryParseColor(arguments[index + 1], out var chosen):
                    accent = chosen;
                    index++;
                    break;
                case "--accent":
                    error.WriteLine("Roundhouse: --accent needs a color written #RRGGBB, such as #3584E4.");
                    error.WriteLine(Usage);
                    return 2;
                case "--scrollbars" when index + 1 < arguments.Count && DesktopLook.TryParseScrollBars(arguments[index + 1], out var style):
                    bars = style;
                    index++;
                    break;
                case "--scrollbars":
                    error.WriteLine("Roundhouse: --scrollbars is classic or automatic.");
                    error.WriteLine(Usage);
                    return 2;
                case var word when word.StartsWith("--", StringComparison.Ordinal):
                    error.WriteLine($"Roundhouse: {word} isn't an option.");
                    error.WriteLine(Usage);
                    return 2;
                default:
                    paths.Add(arguments[index]);
                    break;
            }
        }

        if (paths.Count != 2)
        {
            error.WriteLine(Usage);
            return 2;
        }

        var (document, picture) = (paths[0], paths[1]);
        DesktopAppearance.Use(DesktopLook.Standard with { Mode = dark ? LookMode.Dark : LookMode.Light, Accent = accent, ScrollBars = bars });

        UIView view;
        try
        {
            // The document's own report names a picture that couldn't be read, so the loader needn't say it too.
            view = DocumentView.Open(Path.GetFullPath(document), path => SkiaImages.Load(path),
                problem => error.WriteLine($"Roundhouse: {document}: {problem}"), SimulatedDisk.FindSharedStrings());
        }
        catch (Exception exception) when (exception is UIScriptException or IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"Roundhouse: {document}: {exception.Message}");
            return 1;
        }

        // A picture can't scroll, so automatic bars show as they do just after scrolling, rather than not at all.
        view.ScrollBars = bars;
        view.FlashScrollBars();

        try
        {
            File.WriteAllBytes(picture, Render(view, Path.GetFileName(document)));
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"Roundhouse: {picture}: {exception.Message}");
            return 1;
        }
    }

    /// <summary>Draws a view in a window over the sky, as a PNG file's bytes.</summary>
    /// <exception cref="FileNotFoundException">VentureOS's fonts aren't beside Roundhouse.</exception>
    public static byte[] Render(UIView view, string title)
    {
        using var fonts = SkiaFonts.LoadShipped();
        using var painter = Draw(view, title, fonts);
        return painter.EncodePng();
    }

    /// <summary>
    /// Draws a view in a window over the sky, as the desktop would, with some sky round it for the window's shadow.
    /// The caller disposes of the painter, whose <see cref="SkiaPainter.Pixels"/> hold the picture.
    /// </summary>
    public static SkiaPainter Draw(UIView view, string title, SkiaFonts fonts) => Draw(view, title, fonts, out _);

    /// <summary>Draws as <see cref="Draw(UIView, string, SkiaFonts)"/> does, and gives the window drawn, for finding what's where.</summary>
    public static SkiaPainter Draw(UIView view, string title, SkiaFonts fonts, out DesktopWindow drawn) =>
        Draw(view, title, fonts, out drawn, null);

    /// <summary>
    /// Draws as <see cref="Draw(UIView, string, SkiaFonts)"/> does, with <paramref name="menuBar"/>, when it has menus,
    /// along the top, as VentureOS's menu bar is, and any menu open in it over the window, the way Xcode shows an
    /// app's menus above its design. The canvas grows to hold the longest menu.
    /// </summary>
    public static SkiaPainter Draw(UIView view, string title, SkiaFonts fonts, out DesktopWindow drawn, MenuBar? menuBar)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(fonts);

        if (view.Script.StatusItem is { } status)
        {
            drawn = new DesktopWindow(title, view);
            return DrawStatusItem(view, status, fonts);
        }

        var bar = menuBar is { Menus.Count: > 0 } ? menuBar : null;
        var top = bar is null ? 0 : MenuBar.Height;
        DesktopWindow window = new(title, view);
        var width = window.Width + (2 * Margin);
        var height = window.Height + (2 * Margin) + top;
        if (bar is not null)
        {
            // Every entry as an item, the taller of the two, and a margin, so an open menu always fits.
            var longest = bar.Menus.Max(menu => menu.Menu.Entries.Count);
            height = Math.Max(height, MenuBar.Height + (longest * MenuRow) + Margin);
        }

        WindowManager windows = new(width, height);
        windows.Open(window);

        // A panel is drawn as designed, at its document's size, rather than pinned to an edge and sized to its content,
        // and an empty shelf shows a few sample icons, since its app fills it only when it runs.
        if (window.IsPanel)
        {
            view.SetWindowSize(view.Script.Window.Size);
            foreach (var control in view.Script.Controls)
            {
                if (view.Shelf(control.Name) is { Items.Count: 0 } shelf)
                    shelf.SetItems(SampleIcons);
            }
        }

        (window.X, window.Y) = (Margin, Margin + top);

        SkiaPainter painter = new(width, height, fonts);
        painter.DrawPixels(DesktopBackground.Render(width, height), 0, 0, width, height);
        windows.Draw(painter);
        bar?.Draw(painter);
        drawn = window;
        return painter;
    }

    /// <summary>
    /// A status item as the bar shows it: its title and symbol at the right of a bar, its menus dropping from it as
    /// they would, and, for one that holds controls, its panel below it on the shelf's card, open.
    /// </summary>
    private static SkiaPainter DrawStatusItem(UIView view, UIStatusItem status, SkiaFonts fonts)
    {
        var size = view.Script.Window.Size;
        var width = Math.Max(StatusBarWidth, (int)size.Width + (2 * Margin));
        var menuRows = view.Script.Menus.Sum(menu => menu.Entries.Count);
        var height = MenuBar.Height + Math.Max(Margin + (int)size.Height + Margin, menuRows * MenuRow + Margin);

        var icon = status.Image is { } image && view.Image(image) is { Height: > 0 } picture
            ? picture.Scaled(Math.Max(1, picture.Width * DesktopSession.StatusIconSize / picture.Height), DesktopSession.StatusIconSize)
            : null;
        IReadOnlyList<UIMenuEntry> entries = view.Script.Menus.Count == 1
            ? view.Script.Menus[0].Entries
            : [.. view.Script.Menus.Select(menu => new UIMenuEntry(menu.Name, menu.Title, null, null, menu.Line, menu))];
        MenuBar bar = new();
        bar.SetMenus([new BarMenu(new UIMenu(status.Name, status.Title ?? "", null, entries, status.Line), MenuGroup.Status, view, Icon: icon)]);

        SkiaPainter painter = new(width, height, fonts);
        painter.DrawPixels(DesktopBackground.Render(width, height), 0, 0, width, height);
        bar.Draw(painter);
        if (entries.Count > 0)
        {
            // Open, so its menu shows below it.
            bar.OpenAt(0);
            bar.Draw(painter);
        }
        else if (size.Width > 0 && size.Height > 0)
        {
            var x = width - Margin - (int)size.Width;
            NotificationCard.DrawBody(painter, x, MenuBar.Height + 4, (int)size.Width, (int)size.Height);
            view.Draw(painter, x, MenuBar.Height + 4);
        }

        return painter;
    }

    // Wide enough for a status item's bar to look like the bar's right end.
    private const int StatusBarWidth = 320;

    // What an empty shelf shows in a design: apps of the system's and the user's, and a minimized window.
    private static readonly ShelfItem[] SampleIcons =
        [new("Files"), new("Terminal"), new("Notes", IsOpen: true, Group: 1), new("Untitled", Group: 2, IsWindow: true)];

    // At least as tall as one of a menu's rows, separators included.
    private const int MenuRow = 22;
}
