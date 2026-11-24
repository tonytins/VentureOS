using Venture.OS.Bundles;
using Venture.OS.Interface.Xml;

namespace Venture.Airo.Projects;

/// <summary>A project Roundhouse can't open or make, and why, said for the person using it.</summary>
public sealed class ProjectException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// A Roundhouse Project: a <c>.rproj</c> folder where an app and its interfaces live while they're made, so an
/// <c>.app</c>, which VentureOS takes for something that runs, only appears where something runs it.
/// </summary>
/// <remarks>
/// <code>
/// Dice.rproj/
///   Project.xml   &lt;Project version="1"&gt;&lt;Info name="Dice"/&gt;&lt;/Project&gt;
///   App/          optional: the app, laid out as a bundle's Contents/, which <see cref="Assemble"/> makes into Dice.app
///   Interfaces/   optional: loose .uix files, such as system components to wire later
/// </code>
/// <para>Only Roundhouse and airo read a project; VentureOS gets the <c>.app</c>. <c>Project.xml</c> follows the format rules
/// VentureOS's own files do: a newer <c>version</c> is refused, since it may mean something this can't keep, and an
/// attribute this doesn't know is ignored and, since nothing here rewrites the file, kept.</para>
/// </remarks>
public sealed class RoundhouseProject
{
    /// <summary>A project folder's extension.</summary>
    public const string Extension = ".rproj";

    /// <summary>The project's own file, at the folder's top.</summary>
    public const string FileName = "Project.xml";

    /// <summary>The newest <c>Project.xml</c> format this reads.</summary>
    public const int SupportedVersion = 1;

    private RoundhouseProject(string root, string name)
    {
        Root = root;
        Name = name;
    }

    /// <summary>The project's folder, such as <c>~/Documents/Dice.rproj</c>.</summary>
    public string Root { get; }

    /// <summary>The project's name, from <c>Project.xml</c>.</summary>
    public string Name { get; }

    /// <summary>Where the app's files are, laid out as a bundle's <c>Contents/</c>, whether or not there's an app yet.</summary>
    public string AppFolder => Path.Combine(Root, "App");

    /// <summary>Where the loose interfaces are, whether or not there are any yet.</summary>
    public string InterfacesFolder => Path.Combine(Root, "Interfaces");

    /// <summary>Whether the project has its app: one at most, and it's optional, so a project can be only interfaces.</summary>
    public bool HasApp => Directory.Exists(AppFolder);

    /// <summary>Whether <paramref name="path"/> is a project's folder by its name.</summary>
    public static bool IsProject(string path) =>
        Path.TrimEndingDirectorySeparator(path).EndsWith(Extension, StringComparison.OrdinalIgnoreCase);

    /// <summary>The project a path is, or is inside, or <see langword="null"/>: the nearest folder named like one.</summary>
    public static string? Find(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        for (var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)); !string.IsNullOrEmpty(folder); folder = Path.GetDirectoryName(folder))
        {
            if (IsProject(folder))
                return folder;
        }

        return null;
    }

    /// <summary>Opens the project at <paramref name="root"/>.</summary>
    /// <exception cref="ProjectException">It isn't a project this Roundhouse reads; the message says why.</exception>
    public static RoundhouseProject Open(string root)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var label = Path.GetFileName(root);
        if (!Directory.Exists(root))
            throw new ProjectException($"There's no project at {root}.");

        var file = Path.Combine(root, FileName);
        if (!File.Exists(file))
            throw new ProjectException($"{label} has no {FileName}, so it isn't a project.");

        FlatXmlDocument document;
        try
        {
            document = FlatXml.Load(file);
        }
        catch (FlatXmlException exception)
        {
            throw new ProjectException($"{label}'s {FileName}, line {exception.Line}: {exception.Reason}", exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ProjectException($"{label}'s {FileName} couldn't be read: {exception.Message}", exception);
        }

        return new RoundhouseProject(root, ReadName(document, label));
    }

    /// <summary>
    /// Makes a project from an <c>.app</c>, beside it and named after it: its <c>Contents/</c> copied to the project's
    /// <c>App/</c>, so a stray app becomes work in progress. The <c>.app</c> is left as it was.
    /// </summary>
    /// <exception cref="ProjectException">It isn't an app with <c>Contents/</c>, or there's already a project of its name there.</exception>
    /// <exception cref="IOException">It couldn't be written; what was written is removed.</exception>
    public static RoundhouseProject FromApp(string app)
    {
        ArgumentException.ThrowIfNullOrEmpty(app);
        app = Path.TrimEndingDirectorySeparator(Path.GetFullPath(app));
        var contents = Path.Combine(app, "Contents");
        if (!app.EndsWith(Bundle.Extension(BundleKind.App), StringComparison.OrdinalIgnoreCase) || !Directory.Exists(contents))
            throw new ProjectException($"{Path.GetFileName(app)} isn't an app with Contents/, so there's nothing to make a project from.");

        var project = Create(Path.GetFileNameWithoutExtension(app), Path.GetDirectoryName(app)!);
        try
        {
            foreach (var source in Directory.EnumerateFiles(contents, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(project.AppFolder, Path.GetRelativePath(contents, source));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target);
            }

            return project;
        }
        catch
        {
            Directory.Delete(project.Root, recursive: true);
            throw;
        }
    }

    /// <summary>Makes an empty project, <c>Name.rproj</c> with only its <c>Project.xml</c>, in <paramref name="folder"/>.</summary>
    /// <exception cref="ProjectException">The name can't be a project's, or there's already something of that name there.</exception>
    /// <exception cref="IOException">It couldn't be written; what was written is removed.</exception>
    public static RoundhouseProject Create(string name, string folder)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentException.ThrowIfNullOrEmpty(folder);
        if (FolderNames.Problem(name, "project") is { } problem)
            throw new ProjectException(problem);

        name = name.Trim();
        var root = Path.Combine(Path.GetFullPath(folder), name + Extension);
        if (Directory.Exists(root) || File.Exists(root))
            throw new ProjectException($"There's already {Path.GetFileName(root)} in {folder}.");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, FileName), $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!-- A Roundhouse Project: Roundhouse makes its App/ into {FlatXml.EncodeValue(name, '"')}.app to run or export it. -->
                <Project version="{SupportedVersion}">
                  <Info name="{FlatXml.EncodeValue(name, '"')}"/>
                </Project>

                """);
            return Open(root);
        }
        catch
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
            throw;
        }
    }

    /// <summary>
    /// Makes the project's app into <c>Name.app</c> in <paramref name="folder"/>: <c>App/</c> copied to its
    /// <c>Contents/</c>, with <paramref name="edits"/> (text by full path, such as unsaved files) in place of what's
    /// saved, then checked as VentureOS reads apps. It's how the app is run and exported, so an <c>.app</c> only
    /// exists where something runs it.
    /// </summary>
    /// <param name="replace">Whether an <c>.app</c> of that name already in <paramref name="folder"/> is replaced.</param>
    /// <returns>The <c>.app</c>'s folder.</returns>
    /// <exception cref="ProjectException">There's no app, its <c>Info.xml</c> has no usable name, there's already an
    /// <c>.app</c> there and <paramref name="replace"/> is false, or VentureOS wouldn't read the result; nothing new is
    /// left behind.</exception>
    /// <exception cref="IOException">It couldn't be written; what was written is removed.</exception>
    public string Assemble(string folder, IReadOnlyDictionary<string, string>? edits = null, bool replace = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        edits ??= new Dictionary<string, string>();
        if (!HasApp)
            throw new ProjectException($"{Path.GetFileName(Root)} has no app yet; add one with New App.");

        var appName = AppName(edits);
        var app = Path.Combine(Path.GetFullPath(folder), appName + Bundle.Extension(BundleKind.App));
        if (Directory.Exists(app) || File.Exists(app))
        {
            if (!replace)
                throw new ProjectException($"There's already {Path.GetFileName(app)} in {folder}.");

            // Built beside it first, so a failure leaves the one that's there untouched.
            var fresh = Assemble(Path.Combine(folder, $".{appName}.{Guid.NewGuid():N}"), edits);
            try
            {
                Directory.Delete(app, recursive: true);
                Directory.Move(fresh, app);
            }
            finally
            {
                var scratch = Path.GetDirectoryName(fresh)!;
                if (Directory.Exists(scratch))
                    Directory.Delete(scratch, recursive: true);
            }

            return app;
        }

        var contents = Path.Combine(app, "Contents");
        try
        {
            foreach (var source in Directory.EnumerateFiles(AppFolder, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(contents, Path.GetRelativePath(AppFolder, source));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target);
            }

            foreach (var (file, text) in edits)
            {
                var relative = Path.GetRelativePath(AppFolder, Path.GetFullPath(file));
                if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
                    continue;

                var target = Path.Combine(contents, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllText(target, text);
            }

            Bundle.Read(app.Replace('\\', '/'), BundleKind.App);
            return app;
        }
        catch (BundleException exception)
        {
            Directory.Delete(app, recursive: true);
            throw new ProjectException(exception.Message, exception);
        }
        catch
        {
            if (Directory.Exists(app))
                Directory.Delete(app, recursive: true);
            throw;
        }
    }

    /// <summary>
    /// The name of the <c>.app</c> <see cref="Assemble"/> makes, such as <c>Dice.app</c>, from <c>App/Info.xml</c> with
    /// <paramref name="edits"/>, so a caller can ask before replacing one.
    /// </summary>
    /// <exception cref="ProjectException">The app's <c>Info.xml</c> has no usable name.</exception>
    public string AppFolderName(IReadOnlyDictionary<string, string>? edits = null) =>
        AppName(edits ?? new Dictionary<string, string>()) + Bundle.Extension(BundleKind.App);

    // The app's name, from its Info.xml as edited, which names the .app it's assembled into.
    private string AppName(IReadOnlyDictionary<string, string> edits)
    {
        var info = Path.Combine(AppFolder, "Info.xml");
        var label = $"{Path.GetFileName(Root)}'s App/Info.xml";
        string text;
        try
        {
            text = edits.TryGetValue(Path.GetFullPath(info), out var edited) ? edited : File.ReadAllText(info);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ProjectException($"{label} couldn't be read: {exception.Message}", exception);
        }

        FlatXmlDocument document;
        try
        {
            document = FlatXml.Parse(text);
        }
        catch (FlatXmlException exception)
        {
            throw new ProjectException($"{label}, line {exception.Line}: {exception.Reason}", exception);
        }

        foreach (var child in document.Children)
        {
            if (child.Name == "Info" && child["name"] is { } name)
            {
                return FolderNames.Problem(name, "app") is { } problem
                    ? throw new ProjectException($"{label}: name=\"{name}\" can't name the app's folder. {problem}")
                    : name.Trim();
            }
        }

        throw new ProjectException($"{label} has no <Info name=\"…\"/>, which names the app.");
    }

    private static string ReadName(FlatXmlDocument document, string label)
    {
        var root = document.Root;
        if (root.Name != "Project")
            throw new ProjectException($"{label}'s {FileName} starts with <{root.Name}>, not <Project>.");

        if (root["version"] is { } text)
        {
            if (!int.TryParse(text, out var version) || version < 1)
                throw new ProjectException($"{label}'s {FileName}: version=\"{text}\" isn't a format version, such as 1.");
            if (version > SupportedVersion)
                throw new ProjectException(
                    $"{label} is a format version {version} project, and this Roundhouse reads up to {SupportedVersion}; it needs a newer Roundhouse.");
        }

        string? name = null;
        foreach (var child in document.Children)
        {
            // An element this doesn't know may change what the project means, so it's refused, as VentureOS refuses
            // one in Info.xml, rather than skipped.
            if (child.Name != "Info")
                throw new ProjectException($"{label}'s {FileName}, line {child.Line}: <{child.Name}> isn't something this Roundhouse knows.");
            if (name is not null)
                throw new ProjectException($"{label}'s {FileName}, line {child.Line}: there's more than one <Info>.");
            name = child["name"];
            if (string.IsNullOrWhiteSpace(name))
                throw new ProjectException($"{label}'s {FileName}, line {child.Line}: <Info> needs a name.");
        }

        return name?.Trim() ?? throw new ProjectException($"{label}'s {FileName} has no <Info>.");
    }
}
