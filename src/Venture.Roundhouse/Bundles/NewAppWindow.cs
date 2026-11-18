using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Venture.Roundhouse.Simulator;

namespace Venture.Roundhouse.Bundles;

/// <summary>
/// File ▸ New App's dialog: the app's name, its identifier, which follows the name until it's changed, and where to
/// make it. Creating closes the dialog with the new app's folder; a problem, such as a name already taken, shows in
/// the dialog, which stays open.
/// </summary>
public sealed class NewAppWindow : Window
{
    private readonly TextBox name = new() { PlaceholderText = "Counter" };
    private readonly TextBox identifier = new();
    private readonly TextBox location = new();
    private readonly TextBlock problem = new() { Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private bool following = true;
    private bool setting;

    public NewAppWindow(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        Title = "New App";
        Width = 460;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        location.Text = folder;

        // Watching the text itself, which changes at once, whether typed or set; TextChanged comes a moment later.
        name.PropertyChanged += (_, e) =>
        {
            if (e.Property != TextBox.TextProperty || !following)
                return;
            setting = true;
            identifier.Text = AppTemplate.IdentifierFor(name.Text ?? "");
            setting = false;
        };
        identifier.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
                following = setting || (following && identifier.Text == AppTemplate.IdentifierFor(name.Text ?? ""));
        };

        Button browse = new() { Content = "Choose…" };
        browse.Click += async (_, _) =>
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Where to make the app", AllowMultiple = false });
            if (folders is [var chosen, ..] && chosen.TryGetLocalPath() is { } local)
                location.Text = local;
        };

        Button create = new() { Content = "Create", IsDefault = true };
        create.Click += (_, _) => Create();
        Button cancel = new() { Content = "Cancel", IsCancel = true };
        cancel.Click += (_, _) => Close(null);

        Grid fields = new() { ColumnDefinitions = new("100,*,Auto"), RowDefinitions = new("Auto,Auto,Auto"), RowSpacing = 8, ColumnSpacing = 8 };
        Add(fields, 0, "Name", name);
        Add(fields, 1, "Identifier", identifier);
        Add(fields, 2, "Make it in", location);
        Grid.SetRow(browse, 2);
        Grid.SetColumn(browse, 2);
        fields.Children.Add(browse);

        Content = new StackPanel
        {
            Margin = new(20),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = "A window app with a button that counts its presses, to start from.", TextWrapping = TextWrapping.Wrap, Opacity = 0.7 },
                fields,
                problem,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, create } },
            },
        };
        Opened += (_, _) => name.Focus();
    }

    /// <summary>The app's name, as typed.</summary>
    public string AppName
    {
        get => name.Text ?? "";
        set => name.Text = value;
    }

    /// <summary>The app's identifier, as typed or following the name.</summary>
    public string Identifier
    {
        get => identifier.Text ?? "";
        set => identifier.Text = value;
    }

    /// <summary>Where the app is made.</summary>
    public string Location
    {
        get => location.Text ?? "";
        set => location.Text = value;
    }

    /// <summary>Why the app couldn't be made, or <see langword="null"/>.</summary>
    public string? Problem => problem.IsVisible ? problem.Text : null;

    /// <summary>
    /// The folder File ▸ New App suggests: the repository's user apps while Roundhouse runs from it, so a new app is
    /// part of VentureOS, and the computer's documents otherwise.
    /// </summary>
    public static string SuggestedFolder()
    {
        if (SimulatedDisk.FindRootfs() is { } rootfs
            && !Path.GetFullPath(rootfs).StartsWith(Path.GetFullPath(AppContext.BaseDirectory), StringComparison.Ordinal))
            return Path.Combine(rootfs, "user", "apps");
        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    /// <summary>Makes the app, and closes with its folder; a problem is shown, and the dialog stays.</summary>
    /// <returns>The new app's folder, or <see langword="null"/>.</returns>
    public string? Create()
    {
        try
        {
            var app = AppTemplate.Create(AppName, Identifier, Location);
            Close(app);
            return app;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            problem.Text = exception.Message;
            problem.IsVisible = true;
            return null;
        }
    }

    private static void Add(Grid grid, int row, string label, Control field)
    {
        TextBlock text = new() { Text = label, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(text, row);
        Grid.SetRow(field, row);
        Grid.SetColumn(field, 1);
        grid.Children.Add(text);
        grid.Children.Add(field);
    }
}
