using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Venture.Roundhouse.Projects;

namespace Venture.Roundhouse.Bundles;

/// <summary>What File ▸ New… is making.</summary>
public enum NewKind
{
    /// <summary>An empty project, for an app, interfaces, or both later.</summary>
    Project,

    /// <summary>A project with its app, when no project is open.</summary>
    AppInNewProject,

    /// <summary>The open project's app.</summary>
    AppInProject,
}

/// <summary>
/// File ▸ New…'s dialog for a project or an app: a name; for an app, its identifier, which follows the name until
/// it's changed; and, for a new project, where to make it. Creating closes the dialog with the project's folder; a
/// problem, such as a name already taken, shows in the dialog, which stays open.
/// </summary>
public sealed class NewAppWindow : Window
{
    private readonly TextBox name = new() { PlaceholderText = "Counter" };
    private readonly TextBox identifier = new();
    private readonly TextBox location = new();
    private readonly TextBlock problem = new() { Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly NewKind kind;
    private readonly RoundhouseProject? project;
    private bool following = true;
    private bool setting;

    /// <summary>Asks for a new project, with its app when <paramref name="withApp"/>, made in <paramref name="folder"/>.</summary>
    public NewAppWindow(string folder, bool withApp = true)
        : this(withApp ? NewKind.AppInNewProject : NewKind.Project, folder, null)
    {
    }

    /// <summary>Asks for the app of <paramref name="project"/>, which has none yet.</summary>
    public NewAppWindow(RoundhouseProject project)
        : this(NewKind.AppInProject, project?.Root ?? throw new ArgumentNullException(nameof(project)), project)
    {
    }

    private NewAppWindow(NewKind kind, string folder, RoundhouseProject? project)
    {
        ArgumentNullException.ThrowIfNull(folder);
        this.kind = kind;
        this.project = project;
        Title = kind == NewKind.Project ? "New Project" : "New App";
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
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Where to make the project", AllowMultiple = false });
            if (folders is [var chosen, ..] && chosen.TryGetLocalPath() is { } local)
                location.Text = local;
        };

        Button create = new() { Content = "Create", IsDefault = true };
        create.Click += (_, _) => Create();
        Button cancel = new() { Content = "Cancel", IsCancel = true };
        cancel.Click += (_, _) => Close(null);

        // A project has no identifier, and an app in the open project goes in its App/, so neither asks.
        Grid fields = new() { ColumnDefinitions = new("100,*,Auto"), RowDefinitions = new("Auto,Auto,Auto"), RowSpacing = 8, ColumnSpacing = 8 };
        Add(fields, 0, "Name", name);
        if (kind != NewKind.Project)
            Add(fields, 1, "Identifier", identifier);
        if (kind != NewKind.AppInProject)
        {
            Add(fields, 2, "Make it in", location);
            Grid.SetRow(browse, 2);
            Grid.SetColumn(browse, 2);
            fields.Children.Add(browse);
        }

        var about = kind switch
        {
            NewKind.Project => "An empty project, for an app, interfaces, or both. Roundhouse makes its app into an .app to run or export it.",
            NewKind.AppInNewProject => "A project with a window app in it, with a button that counts its presses, to start from.",
            _ => $"{Path.GetFileName(project!.Root)}'s app: a window app with a button that counts its presses, to start from.",
        };

        Content = new StackPanel
        {
            Margin = new(20),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = about, TextWrapping = TextWrapping.Wrap, Opacity = 0.7 },
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
    /// The folder a new project is suggested in: the computer's documents. A project is work in progress, so it
    /// doesn't go among VentureOS's apps; File ▸ Export… puts its app there when it's ready.
    /// </summary>
    public static string SuggestedFolder() => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    /// <summary>Makes the project or app, and closes with the project's folder; a problem is shown, and the dialog stays.</summary>
    /// <returns>The project's folder, or <see langword="null"/>.</returns>
    public string? Create()
    {
        RoundhouseProject? made = null;
        try
        {
            var target = project ?? (made = RoundhouseProject.Create(AppName, Location));
            if (kind != NewKind.Project)
                AppTemplate.CreateIn(target, AppName, Identifier);
            Close(target.Root);
            return target.Root;
        }
        catch (Exception exception) when (exception is ArgumentException or ProjectException or IOException or UnauthorizedAccessException)
        {
            // A new project whose app couldn't be made goes too, so nothing half-made is left.
            if (made is not null && Directory.Exists(made.Root))
                Directory.Delete(made.Root, recursive: true);
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
