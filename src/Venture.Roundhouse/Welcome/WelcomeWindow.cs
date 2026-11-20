using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Venture.Roundhouse.Welcome;

/// <summary>One way to start from the welcome window: its icon, its title, the hint the status bar gives, and what it does.</summary>
public sealed record WelcomeAction(string Icon, string Title, string Hint, Func<Task> Run);

/// <summary>
/// What Roundhouse shows when it starts with nothing named, after Xcode's and Rider's: its logo, name, and version
/// with the ways to start under them, centred; the recent projects, apps, and interfaces beside them; and a status
/// bar that explains what's under the pointer and holds "Show at startup".
/// </summary>
/// <remarks>
/// It knows nothing of the main window: the actions and opening a recent item are given to it, and whoever shows it
/// closes it once something's open.
/// </remarks>
public sealed class WelcomeWindow : Window
{
    /// <summary>What the status bar says with nothing under the pointer.</summary>
    public const string Prompt = "Choose a recent item, or start something new.";

    private readonly TextBlock hint = new() { Text = Prompt, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly CheckBox showAtStartup = new() { Content = "Show at startup", VerticalAlignment = VerticalAlignment.Center };
    private readonly ListBox recent = new() { Background = Brushes.Transparent, BorderThickness = new(0) };
    private readonly Func<string, Task> open;
    private readonly Action<string> forget;

    /// <param name="version">Roundhouse's version, such as 0.7.604.</param>
    /// <param name="actions">The ways to start, in order.</param>
    /// <param name="items">The recent items, newest first.</param>
    /// <param name="showing">Whether "Show at startup" starts checked.</param>
    /// <param name="setShowing">Keeps "Show at startup" when it's changed.</param>
    /// <param name="open">Opens a recent item, by its path.</param>
    /// <param name="forget">Takes a recent item off the list, by its path.</param>
    public WelcomeWindow(
        string version,
        IReadOnlyList<WelcomeAction> actions,
        IReadOnlyList<RecentItem> items,
        bool showing,
        Action<bool> setShowing,
        Func<string, Task> open,
        Action<string> forget)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(setShowing);
        this.open = open ?? throw new ArgumentNullException(nameof(open));
        this.forget = forget ?? throw new ArgumentNullException(nameof(forget));
        Actions = actions;
        Title = "Welcome to Roundhouse";
        Width = 680;
        // Tall enough for the bar, which is inside the window where it's the title bar, and room around the logo.
        Height = 470;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Roundhouse/Assets/Roundhouse.png")));

        showAtStartup.IsChecked = showing;
        showAtStartup.IsCheckedChanged += (_, _) => setShowing(showAtStartup.IsChecked == true);

        // The main window's bar, with the window's name in it; where the desktop allows, it's the title bar.
        TextBlock name = new() { Text = Title };
        name.Classes.Add("barTitle");
        TitleBar = new WindowTitleBar { Title = name };
        DockPanel.SetDock(TitleBar, Dock.Top);

        Border status = new()
        {
            Padding = new(12, 6),
            BorderThickness = new(0, 1, 0, 0),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0x80, 0x80, 0x80)),
            Child = new DockPanel { Children = { Docked(showAtStartup, Dock.Right), hint } },
        };
        DockPanel.SetDock(status, Dock.Bottom);

        // The left half, centred: the logo, the name, the version, then the ways to start, as links with their icons.
        StackPanel start = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 4 };
        start.Children.Add(new Image
        {
            Source = new Bitmap(AssetLoader.Open(new Uri("avares://Roundhouse/Assets/Roundhouse.png"))),
            Width = 72,
            Height = 72,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        start.Children.Add(new TextBlock { Text = "Roundhouse", FontSize = 22, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, 6, 0, 0) });
        start.Children.Add(new TextBlock { Text = $"Version {version}", Opacity = 0.6, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, 0, 0, 12) });
        foreach (var action in actions)
            start.Children.Add(Link(action));

        recent.ItemsSource = items.Select(item => new RecentRow(item, Exists(item.Path))).ToList();
        // Avalonia builds a row with no item while one taken off the list is cleared away.
        recent.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<RecentRow?>((row, _) => row is null ? new Panel() : Row(row));
        recent.DoubleTapped += (_, _) => _ = OpenSelectedAsync();
        recent.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
                _ = OpenSelectedAsync();
            else if (e.Key is Key.Delete or Key.Back && recent.SelectedItem is RecentRow row)
                Forget(row);
        };

        Border recents = new()
        {
            Background = new SolidColorBrush(Color.FromArgb(0x14, 0x80, 0x80, 0x80)),
            BorderThickness = new(1, 0, 0, 0),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0x80, 0x80, 0x80)),
            Child = items.Count == 0
                ? new TextBlock { Text = "Nothing opened yet.", Opacity = 0.6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                : new ScrollViewer { Content = recent },
        };

        Grid halves = new() { ColumnDefinitions = new("*,*") };
        halves.Children.Add(start);
        Grid.SetColumn(recents, 1);
        halves.Children.Add(recents);

        Content = new DockPanel { LastChildFill = true, Children = { TitleBar, status, halves } };
        TitleBar.Attach(this, TitleBars.Merges());
    }

    /// <summary>The coloured bar, the same as the main window's.</summary>
    public WindowTitleBar TitleBar { get; }

    /// <summary>The ways to start, in order.</summary>
    public IReadOnlyList<WelcomeAction> Actions { get; }

    /// <summary>What the status bar says now.</summary>
    public string Hint => hint.Text ?? "";

    /// <summary>Whether "Show at startup" is checked.</summary>
    public bool ShowAtStartup
    {
        get => showAtStartup.IsChecked == true;
        set => showAtStartup.IsChecked = value;
    }

    /// <summary>The recent items listed, and whether each is still there.</summary>
    public IReadOnlyList<RecentRow> Recent => (IReadOnlyList<RecentRow>?)recent.ItemsSource ?? [];

    /// <summary>Opens a recent item, as a double-click does; one that's gone says so instead.</summary>
    public async Task OpenAsync(RecentRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!Exists(row.Item.Path))
        {
            hint.Text = $"{row.Item.Path} isn't there any more; right-click it, or press Delete, to take it off the list.";
            return;
        }

        await open(row.Item.Path);
    }

    /// <summary>Takes a recent item off the list.</summary>
    public void Forget(RecentRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        forget(row.Item.Path);
        recent.ItemsSource = Recent.Where(other => other != row).ToList();
        hint.Text = $"Took {Path.GetFileName(row.Item.Path)} off the list.";
    }

    /// <summary>What the status bar says about a recent item.</summary>
    public static string HintFor(RecentRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!row.Exists)
            return $"{row.Item.Path} isn't there any more; right-click it, or press Delete, to take it off the list.";
        return row.Item.Kind switch
        {
            RecentKind.App => $"{row.Item.Path}: edited in place, as VentureOS installs it.",
            RecentKind.Interface => $"{row.Item.Path}: an interface of its own.",
            _ => $"{row.Item.Path}: double-click to open.",
        };
    }

    private async Task OpenSelectedAsync()
    {
        if (recent.SelectedItem is RecentRow row)
            await OpenAsync(row);
    }

    // A project, app, or interface: its name and tag over its folder, dimmed when it's gone, with a way off the list.
    private Control Row(RecentRow row)
    {
        var (name, tag, color) = row.Item.Kind switch
        {
            RecentKind.Project => (Path.GetFileNameWithoutExtension(row.Item.Path), "project", Color.Parse("#2190A4")),
            RecentKind.App => (Path.GetFileNameWithoutExtension(row.Item.Path), "app", Color.Parse("#8A8A8A")),
            _ => (Path.GetFileName(row.Item.Path), "interface", Color.Parse("#9B59B6")),
        };

        Border badge = new()
        {
            Background = new SolidColorBrush(color),
            CornerRadius = new(3),
            Padding = new(4, 0),
            Margin = new(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = tag, FontSize = 10, Foreground = Brushes.White },
        };
        StackPanel lines = new()
        {
            Margin = new(4, 3),
            Opacity = row.Exists ? 1 : 0.45,
            Background = Brushes.Transparent,
            Children =
            {
                new StackPanel { Orientation = Orientation.Horizontal, Children = { new TextBlock { Text = name, FontWeight = FontWeight.SemiBold }, badge } },
                new TextBlock { Text = Shortened(Path.GetDirectoryName(row.Item.Path) ?? ""), FontSize = 11, Opacity = 0.65, TextTrimming = TextTrimming.CharacterEllipsis },
            },
        };

        MenuItem remove = new() { Header = "Remove from Recent" };
        remove.Click += (_, _) => Forget(row);
        lines.ContextMenu = new ContextMenu { ItemsSource = new[] { remove } };
        lines.PointerEntered += (_, _) => hint.Text = HintFor(row);
        lines.PointerExited += (_, _) => hint.Text = Prompt;
        return lines;
    }

    private Control Link(WelcomeAction action)
    {
        Button link = new()
        {
            Background = Brushes.Transparent,
            BorderThickness = new(0),
            Padding = new(8, 3),
            HorizontalAlignment = HorizontalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = action.Icon, Foreground = new SolidColorBrush(Color.Parse("#2190A4")), Width = 16, TextAlignment = TextAlignment.Center },
                    new TextBlock { Text = action.Title, Foreground = new SolidColorBrush(Color.Parse("#1F6FD1")) },
                },
            },
        };
        link.Click += (_, _) => _ = action.Run();
        link.PointerEntered += (_, _) => hint.Text = action.Hint;
        link.PointerExited += (_, _) => hint.Text = Prompt;
        return link;
    }

    // A path with the home folder as ~, as the mock-up shows them.
    private static string Shortened(string folder)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return home.Length > 0 && folder.StartsWith(home, StringComparison.Ordinal) ? "~" + folder[home.Length..] : folder;
    }

    private static bool Exists(string path) => Directory.Exists(path) || File.Exists(path);

    private static Control Docked(Control control, Dock dock)
    {
        DockPanel.SetDock(control, dock);
        return control;
    }
}

/// <summary>A recent item as the welcome window lists it, and whether it's still there.</summary>
public sealed record RecentRow(RecentItem Item, bool Exists);
