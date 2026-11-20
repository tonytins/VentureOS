using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Venture.Roundhouse.Projects;

namespace Venture.Roundhouse.Bundles;

/// <summary>Where Save puts an untitled interface.</summary>
public enum InterfacePlace
{
    /// <summary>The open project's app, in its <c>App/Resources/</c>, as one of its windows.</summary>
    ProjectApp,

    /// <summary>The open project's loose interfaces, in its <c>Interfaces/</c>.</summary>
    ProjectInterfaces,

    /// <summary>A new project's <c>Interfaces/</c>.</summary>
    NewProject,

    /// <summary>A <c>.uix</c> of its own, anywhere.</summary>
    File,
}

/// <summary>
/// Asks where an untitled interface goes the first time it's saved: the open project's app or interfaces, a new
/// project, or a file of its own. Drawing came first, so this is the first time anything is asked. Choosing closes
/// it with the file to write; a problem, such as a name already taken, shows in the dialog, which stays open.
/// </summary>
public sealed class SaveInterfaceWindow : Window
{
    private readonly TextBox name = new() { Text = "Untitled" };
    private readonly TextBox projectName = new() { PlaceholderText = "Dialogs" };
    private readonly TextBox location = new();
    private readonly TextBox file = new();
    private readonly TextBlock problem = new() { Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly Dictionary<InterfacePlace, RadioButton> places = [];
    private readonly RoundhouseProject? project;

    /// <param name="project">The project open, whose app and interfaces are offered, or <see langword="null"/>.</param>
    /// <param name="folder">Where a new project or a file of its own is suggested.</param>
    public SaveInterfaceWindow(RoundhouseProject? project, string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        this.project = project;
        Title = "Save Interface";
        Width = 480;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        location.Text = folder;
        file.Text = Path.Combine(folder, "Untitled.uix");

        StackPanel choices = new() { Spacing = 6 };
        if (project is not null)
        {
            var label = Path.GetFileName(project.Root);
            Offer(choices, InterfacePlace.ProjectApp, $"In {label}'s app, as one of its windows", enabled: project.HasApp);
            Offer(choices, InterfacePlace.ProjectInterfaces, $"Among {label}'s interfaces");
        }

        Offer(choices, InterfacePlace.NewProject, "In a new project");
        Offer(choices, InterfacePlace.File, "As a file of its own");
        Place = project is null ? InterfacePlace.NewProject : project.HasApp ? InterfacePlace.ProjectApp : InterfacePlace.ProjectInterfaces;

        Button browseFolder = new() { Content = "Choose…" };
        browseFolder.Click += async (_, _) =>
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Where to make the project", AllowMultiple = false });
            if (folders is [var chosen, ..] && chosen.TryGetLocalPath() is { } local)
                location.Text = local;
        };
        Button browseFile = new() { Content = "Choose…" };
        browseFile.Click += async (_, _) =>
        {
            var chosen = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save the interface as",
                SuggestedFileName = $"{InterfaceName.Trim()}.uix",
                DefaultExtension = "uix",
                FileTypeChoices = [new FilePickerFileType("UIScript documents") { Patterns = ["*.uix"] }],
            });
            if (chosen?.TryGetLocalPath() is { } local)
                file.Text = local;
        };

        Grid newProject = Fields(("Project", projectName, null), ("Make it in", location, browseFolder));
        Grid ownFile = Fields(("File", file, browseFile));
        Grid named = Fields(("Name", name, null));
        void Show()
        {
            newProject.IsVisible = Place == InterfacePlace.NewProject;
            ownFile.IsVisible = Place == InterfacePlace.File;
            named.IsVisible = Place != InterfacePlace.File;
        }

        foreach (var choice in places.Values)
            choice.IsCheckedChanged += (_, _) => Show();
        Show();

        Button save = new() { Content = "Save", IsDefault = true };
        save.Click += (_, _) => Choose();
        Button cancel = new() { Content = "Cancel", IsCancel = true };
        cancel.Click += (_, _) => Close(null);

        Content = new StackPanel
        {
            Margin = new(20),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = "Where does this interface go?", FontWeight = FontWeight.SemiBold },
                choices,
                named,
                newProject,
                ownFile,
                problem,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, save } },
            },
        };
        Opened += (_, _) => name.Focus();
    }

    /// <summary>Where it goes.</summary>
    public InterfacePlace Place
    {
        get => places.FirstOrDefault(pair => pair.Value.IsChecked == true).Key;
        set => places[value].IsChecked = true;
    }

    /// <summary>The interface's name, which names its file in a project.</summary>
    public string InterfaceName
    {
        get => name.Text ?? "";
        set => name.Text = value;
    }

    /// <summary>A new project's name.</summary>
    public string ProjectName
    {
        get => projectName.Text ?? "";
        set => projectName.Text = value;
    }

    /// <summary>Where a new project is made.</summary>
    public string Location
    {
        get => location.Text ?? "";
        set => location.Text = value;
    }

    /// <summary>The file of its own it's saved as.</summary>
    public string FilePath
    {
        get => file.Text ?? "";
        set => file.Text = value;
    }

    /// <summary>Why it can't be saved there, or <see langword="null"/>.</summary>
    public string? Problem => problem.IsVisible ? problem.Text : null;

    /// <summary>
    /// Settles where it goes, making a new project when that's the choice, and closes with the file to write; a
    /// problem is shown, and the dialog stays.
    /// </summary>
    /// <returns>The file to write, which doesn't exist yet, or <see langword="null"/>.</returns>
    public string? Choose()
    {
        try
        {
            var target = Target();
            Close(target);
            return target;
        }
        catch (Exception exception) when (exception is ArgumentException or ProjectException or IOException or UnauthorizedAccessException)
        {
            problem.Text = exception.Message;
            problem.IsVisible = true;
            return null;
        }
    }

    private string Target()
    {
        if (Place == InterfacePlace.File)
        {
            var chosen = FilePath.Trim();
            if (chosen.Length == 0 || !Path.IsPathFullyQualified(chosen))
                throw new ArgumentException("Choose where to save it, as a full path.");
            if (!chosen.EndsWith(".uix", StringComparison.OrdinalIgnoreCase))
                chosen += ".uix";
            return File.Exists(chosen) ? throw new ArgumentException($"There's already {Path.GetFileName(chosen)} there.") : chosen;
        }

        if (FolderNames.Problem(InterfaceName, "interface") is { } bad)
            throw new ArgumentException(bad);

        var fileName = $"{InterfaceName.Trim()}.uix";
        if (Place == InterfacePlace.NewProject)
        {
            // Checked before the project's made, so a taken name leaves nothing behind.
            var made = RoundhouseProject.Create(ProjectName, Location);
            return Path.Combine(made.InterfacesFolder, fileName);
        }

        var open = project ?? throw new ArgumentException("There's no project open.");
        var folder = Place == InterfacePlace.ProjectApp ? Path.Combine(open.AppFolder, "Resources") : open.InterfacesFolder;
        var target = Path.Combine(folder, fileName);
        return File.Exists(target) ? throw new ArgumentException($"There's already {fileName} there.") : target;
    }

    private void Offer(Panel panel, InterfacePlace place, string text, bool enabled = true)
    {
        RadioButton choice = new() { Content = text, GroupName = "place", IsEnabled = enabled };
        places[place] = choice;
        panel.Children.Add(choice);
    }

    private static Grid Fields(params (string Label, Control Field, Control? Extra)[] rows)
    {
        Grid grid = new() { ColumnDefinitions = new("100,*,Auto"), RowSpacing = 8, ColumnSpacing = 8 };
        for (var row = 0; row < rows.Length; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var (label, field, extra) = rows[row];
            TextBlock text = new() { Text = label, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(text, row);
            Grid.SetRow(field, row);
            Grid.SetColumn(field, 1);
            grid.Children.Add(text);
            grid.Children.Add(field);
            if (extra is not null)
            {
                Grid.SetRow(extra, row);
                Grid.SetColumn(extra, 2);
                grid.Children.Add(extra);
            }
        }

        return grid;
    }
}
