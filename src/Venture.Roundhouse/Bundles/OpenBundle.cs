using Venture.Airo.Projects;
using System.Text;

namespace Venture.Roundhouse.Bundles;

/// <summary>A file or folder in a bundle, as the navigator lists it: folders first, then files, each by name.</summary>
/// <param name="Name">Its name, such as <c>main.lua</c>.</param>
/// <param name="Path">Where it is on this computer.</param>
/// <param name="Children">What's inside a folder; none for a file.</param>
public sealed record BundleItem(string Name, string Path, bool IsFolder, IReadOnlyList<BundleItem> Children);

/// <summary>
/// A bundle or project open in Roundhouse, such as <c>Calculator.app</c> or <c>Dice.rproj</c>: its files, and the
/// text of each one edited but not saved, so moving between files keeps every edit until it's saved, and Run can use
/// them all.
/// </summary>
public sealed class OpenBundle
{
    /// <summary>The kinds of folder Roundhouse opens, by their extension: VentureOS's bundles, and its own projects.</summary>
    public static IReadOnlyList<string> Extensions { get; } = [".app", ".wallpaper", RoundhouseProject.Extension];

    // Strict UTF-8, as UIDocument reads, so a file that isn't text isn't changed by being read and written back.
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly Dictionary<string, string> unsaved = new(StringComparer.Ordinal);

    /// <summary>Opens the bundle at <paramref name="root"/>.</summary>
    /// <exception cref="DirectoryNotFoundException">There's no such folder.</exception>
    public OpenBundle(string root)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (!Directory.Exists(Root))
            throw new DirectoryNotFoundException($"{root} isn't a folder.");
    }

    /// <summary>The bundle's folder.</summary>
    public string Root { get; }

    /// <summary>The bundle's name, such as <c>Calculator.app</c>.</summary>
    public string Name => Path.GetFileName(Root);

    /// <summary>Whether it's a Roundhouse Project rather than one of VentureOS's bundles.</summary>
    public bool IsProject => RoundhouseProject.IsProject(Root);

    /// <summary>
    /// The <c>Info.xml</c> its <c>Info.xml</c> form edits: a bundle's <c>Contents/Info.xml</c>, or a project's
    /// <c>App/Info.xml</c>, laid out the same.
    /// </summary>
    public string InfoFile => Path.Combine(Root, IsProject ? "App" : "Contents", "Info.xml");

    /// <summary>The files that have text edited but not saved, by their paths, in no particular order.</summary>
    public IReadOnlyCollection<string> Unsaved => unsaved.Keys;

    /// <summary>The bundle a path is, or is inside, or <see langword="null"/>: the nearest folder named like one.</summary>
    public static string? Find(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        for (var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)); !string.IsNullOrEmpty(folder); folder = Path.GetDirectoryName(folder))
        {
            if (Directory.Exists(folder) && Extensions.Any(extension => folder.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
                return folder;
        }

        return null;
    }

    /// <summary>
    /// The file to show first when it opens on its own: its app's first interface (in a bundle's
    /// <c>Contents/Resources</c>, or a project's <c>App/Resources</c>), then a project's first loose interface, then the
    /// app's <c>Info.xml</c>; or <see langword="null"/> when there's none.
    /// </summary>
    public string? FirstFile()
    {
        var resources = Path.Combine(Path.GetDirectoryName(InfoFile)!, "Resources");
        if (FirstInterface(resources) is { } document)
            return document;
        if (IsProject && FirstInterface(Path.Combine(Root, "Interfaces")) is { } loose)
            return loose;

        return File.Exists(InfoFile) ? InfoFile : null;

        static string? FirstInterface(string folder) =>
            Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*.uix").Order(StringComparer.OrdinalIgnoreCase).FirstOrDefault() : null;
    }

    /// <summary>The bundle's files and folders, read from the disk now, with the bundle itself at the top.</summary>
    public BundleItem Tree() => new(Name, Root, IsFolder: true, Children(Root));

    /// <summary>Whether a path is inside the bundle.</summary>
    public bool Contains(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Path.GetFullPath(path).StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    /// <summary>Whether a file in the bundle has text edited but not saved.</summary>
    public bool IsUnsaved(string file) => unsaved.ContainsKey(Path.GetFullPath(file));

    /// <summary>A file's text: as edited, if it was, or as saved.</summary>
    /// <exception cref="ArgumentException">The file isn't in the bundle.</exception>
    /// <exception cref="IOException">It couldn't be read.</exception>
    /// <exception cref="InvalidDataException">It isn't UTF-8 text.</exception>
    public string Text(string file)
    {
        var full = Inside(file);
        if (unsaved.TryGetValue(full, out var text))
            return text;

        try
        {
            return Utf8.GetString(File.ReadAllBytes(full));
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException($"{Path.GetFileName(full)} isn't UTF-8 text.", exception);
        }
    }

    /// <summary>
    /// Keeps a file's edited text until it's saved; text the same as what's saved leaves the file as saved.
    /// </summary>
    /// <exception cref="ArgumentException">The file isn't in the bundle.</exception>
    public void Edit(string file, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var full = Inside(file);
        if (File.Exists(full) && Saved(full) == text)
            unsaved.Remove(full);
        else
            unsaved[full] = text;
    }

    /// <summary>Writes a file's edited text, if it has some, and forgets it.</summary>
    /// <returns>Whether there was something to write.</returns>
    /// <exception cref="IOException">It couldn't be written; the edit is kept.</exception>
    public bool Save(string file)
    {
        var full = Inside(file);
        if (!unsaved.TryGetValue(full, out var text))
            return false;

        File.WriteAllBytes(full, Utf8.GetBytes(text));
        unsaved.Remove(full);
        return true;
    }

    private string? Saved(string full)
    {
        try
        {
            return Utf8.GetString(File.ReadAllBytes(full));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            return null;
        }
    }

    private string Inside(string file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var full = Path.GetFullPath(file);
        return full.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? full
            : throw new ArgumentException($"{file} isn't in {Name}.", nameof(file));
    }

    private static List<BundleItem> Children(string folder)
    {
        List<BundleItem> items = [];
        foreach (var child in Directory.EnumerateDirectories(folder).Order(StringComparer.OrdinalIgnoreCase))
        {
            // A linked folder is listed but not entered, so a link back up the tree can't go round for ever.
            var linked = new DirectoryInfo(child).LinkTarget is not null;
            items.Add(new BundleItem(Path.GetFileName(child), child, IsFolder: true, linked ? [] : Children(child)));
        }

        foreach (var child in Directory.EnumerateFiles(folder).Order(StringComparer.OrdinalIgnoreCase))
            items.Add(new BundleItem(Path.GetFileName(child), child, IsFolder: false, []));
        return items;
    }
}
