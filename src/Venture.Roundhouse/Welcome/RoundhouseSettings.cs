using System.Text;
using Venture.OS.Interface.Xml;
using Venture.Roundhouse.Projects;

namespace Venture.Roundhouse.Welcome;

/// <summary>What a recent item is, which the welcome window tags it with.</summary>
public enum RecentKind
{
    /// <summary>A Roundhouse Project, <c>.rproj</c>.</summary>
    Project,

    /// <summary>An <c>.app</c> edited in place, such as one of VentureOS's own.</summary>
    App,

    /// <summary>A loose interface, a <c>.uix</c> outside any project or app.</summary>
    Interface,
}

/// <summary>Something opened or saved recently, by its full path.</summary>
public sealed record RecentItem(string Path, RecentKind Kind);

/// <summary>
/// What Roundhouse remembers between launches: the recent items and whether the welcome window shows at startup.
/// </summary>
/// <remarks>
/// <para>Kept as flat XML in <see cref="DefaultPath"/>:
/// <c>&lt;Roundhouse version="1"&gt;&lt;Welcome showAtStartup="true"/&gt;&lt;Recent path="…" kind="project"/&gt;…&lt;/Roundhouse&gt;</c>.
/// A newer Roundhouse may add to it, so an element or attribute this doesn't know is kept as written and saved back.
/// A file this can't read, or a newer <c>version</c>, gives the defaults and a problem to report, never a crash, and a
/// newer file isn't saved over, so this Roundhouse can't throw away what a newer one wrote.</para>
/// </remarks>
public sealed class RoundhouseSettings
{
    /// <summary>The newest settings format this reads.</summary>
    public const int SupportedVersion = 1;

    /// <summary>How many recent items are kept, newest first.</summary>
    public const int MostRecent = 10;

    private readonly List<RecentItem> recent = [];

    // What a newer Roundhouse wrote that this doesn't know: attributes by element, and whole elements, as written.
    private readonly List<KeyValuePair<string, string>> otherWelcome = [];
    private readonly Dictionary<string, List<KeyValuePair<string, string>>> otherRecent = new(StringComparer.Ordinal);
    private readonly List<string> otherElements = [];

    /// <summary>Where the settings live: Roundhouse's folder among the user's application data, such as <c>~/.config/Roundhouse</c> on Linux.</summary>
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Roundhouse", "settings.xml");

    /// <summary>Whether the welcome window shows when Roundhouse starts with nothing named.</summary>
    public bool ShowAtStartup { get; set; } = true;

    /// <summary>The recent items, newest first.</summary>
    public IReadOnlyList<RecentItem> Recent => recent;

    /// <summary>Whether <see cref="Save"/> may write; not for a file from a newer Roundhouse, which it would cut down.</summary>
    public bool CanSave { get; private init; } = true;

    /// <summary>What a path is as a recent item, by its name, or <see langword="null"/> for something that isn't one.</summary>
    public static RecentKind? KindOf(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var trimmed = System.IO.Path.TrimEndingDirectorySeparator(path);
        if (RoundhouseProject.IsProject(trimmed))
            return RecentKind.Project;
        if (trimmed.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            return RecentKind.App;
        if (trimmed.EndsWith(".uix", StringComparison.OrdinalIgnoreCase))
            return RecentKind.Interface;
        return null;
    }

    /// <summary>Puts a project, app, or interface at the top of the recent items, once, keeping <see cref="MostRecent"/>.</summary>
    /// <returns>Whether it was something to remember.</returns>
    public bool Remember(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var full = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path));
        if (KindOf(full) is not { } kind)
            return false;

        Forget(full);
        recent.Insert(0, new RecentItem(full, kind));
        if (recent.Count > MostRecent)
            recent.RemoveRange(MostRecent, recent.Count - MostRecent);
        return true;
    }

    /// <summary>Takes an item off the recent list, such as one that's gone.</summary>
    public void Forget(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var full = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path));
        recent.RemoveAll(item => item.Path == full);
    }

    /// <summary>Reads the settings at <paramref name="path"/>; a missing file is the defaults, with nothing to report.</summary>
    /// <returns>The settings, and what was wrong with the file, or <see langword="null"/>.</returns>
    public static (RoundhouseSettings Settings, string? Problem) Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!File.Exists(path))
            return (new RoundhouseSettings(), null);

        FlatXmlDocument document;
        string text;
        try
        {
            text = File.ReadAllText(path);
            document = FlatXml.Parse(text);
        }
        catch (FlatXmlException exception)
        {
            return (new RoundhouseSettings(), $"{path}, line {exception.Line}: {exception.Reason} Using the defaults.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (new RoundhouseSettings(), $"{path} couldn't be read: {exception.Message} Using the defaults.");
        }

        var root = document.Root;
        if (root.Name != "Roundhouse")
            return (new RoundhouseSettings(), $"{path} starts with <{root.Name}>, not <Roundhouse>, so it isn't Roundhouse's settings. Using the defaults.");
        if (root["version"] is { } version && (!int.TryParse(version, out var number) || number > SupportedVersion || number < 1))
        {
            return (new RoundhouseSettings { CanSave = false },
                $"{path} is settings version {version}, and this Roundhouse reads up to {SupportedVersion}. Using the defaults, and leaving the file as it is.");
        }

        RoundhouseSettings settings = new();
        List<string> skipped = [];
        foreach (var child in document.Children)
        {
            switch (child.Name)
            {
                case "Welcome":
                    foreach (var (name, value) in child.Attributes)
                    {
                        if (name == "showAtStartup" && bool.TryParse(value, out var show))
                            settings.ShowAtStartup = show;
                        else if (name != "showAtStartup")
                            settings.otherWelcome.Add(new(name, value));
                        else
                            skipped.Add($"line {child.Line}: showAtStartup=\"{value}\" isn't true or false");
                    }

                    break;
                case "Recent":
                    if (child["path"] is not { Length: > 0 } recentPath || KindOf(recentPath) is not { } kind)
                    {
                        skipped.Add($"line {child.Line}: a <Recent> without a project, app, or interface's path");
                        break;
                    }

                    if (settings.recent.Count < MostRecent && settings.recent.TrueForAll(item => item.Path != recentPath))
                    {
                        settings.recent.Add(new RecentItem(recentPath, kind));
                        List<KeyValuePair<string, string>> other = [];
                        foreach (var attribute in child.Attributes)
                        {
                            if (attribute.Key is not ("path" or "kind"))
                                other.Add(attribute);
                        }

                        if (other.Count > 0)
                            settings.otherRecent[recentPath] = other;
                    }

                    break;
                default:
                    // A newer Roundhouse's, kept as written.
                    if (child.Source is { } source)
                        settings.otherElements.Add(source.Span.Of(text));
                    break;
            }
        }

        return (settings, skipped.Count == 0 ? null : $"{path}: skipped {string.Join("; ", skipped)}.");
    }

    /// <summary>
    /// Writes the settings to <paramref name="path"/>, making its folder, through a file beside it, so a failure
    /// partway leaves the old settings whole.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="CanSave"/> is false.</exception>
    /// <exception cref="IOException">They couldn't be written.</exception>
    /// <exception cref="UnauthorizedAccessException">They couldn't be written.</exception>
    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!CanSave)
            throw new InvalidOperationException("These settings came from a newer Roundhouse's file, which saving would cut down.");

        StringBuilder text = new();
        text.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        text.Append("<!-- Roundhouse's settings: the welcome window's recent items, and whether it shows at startup. -->\n");
        text.Append($"<Roundhouse version=\"{SupportedVersion}\">\n");
        text.Append($"  <Welcome showAtStartup=\"{(ShowAtStartup ? "true" : "false")}\"{Attributes(otherWelcome)}/>\n");
        foreach (var item in recent)
        {
            var other = otherRecent.TryGetValue(item.Path, out var kept) ? Attributes(kept) : "";
            text.Append($"  <Recent path=\"{FlatXml.EncodeValue(item.Path, '"')}\" kind=\"{Name(item.Kind)}\"{other}/>\n");
        }

        foreach (var element in otherElements)
            text.Append("  ").Append(element).Append('\n');
        text.Append("</Roundhouse>\n");

        var folder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!;
        Directory.CreateDirectory(folder);
        var temporary = System.IO.Path.Combine(folder, $".{System.IO.Path.GetFileName(path)}.{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(temporary, text.ToString());
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static string Name(RecentKind kind) => kind switch
    {
        RecentKind.Project => "project",
        RecentKind.App => "app",
        _ => "interface",
    };

    private static string Attributes(List<KeyValuePair<string, string>> attributes)
    {
        StringBuilder text = new();
        foreach (var (name, value) in attributes)
            text.Append($" {name}=\"{FlatXml.EncodeValue(value, '"')}\"");
        return text.ToString();
    }
}
