using System.Diagnostics.CodeAnalysis;
using System.Text;
using Venture.OS.Interface.Xml;

namespace Venture.OS.Bundles;

/// <summary>
/// The apps the person chose to open at startup, after macOS's login items: they open with the desktop, after the
/// kept apps, and are left ended if they quit or crash. Kept in <c>startup.xml</c> beside the look's settings, by
/// identifier, so a renamed or moved app is still found. See <c>docs/design/2026-11-25-status-items-design.md</c>.
/// </summary>
/// <remarks>
/// <para>Only the person puts an app here, in Settings or by answering an app that asks; an app can't add itself.</para>
/// <para>The file is <c>&lt;StartupItems version="1"&gt;</c> holding <c>&lt;App identifier="..."/&gt;</c> elements.
/// Elements and attributes this reader doesn't know are skipped, so a later version can say more.</para>
/// </remarks>
public sealed class StartupItems
{
    private const int SupportedVersion = 1;

    private readonly Func<string, Bundle?> find;

    /// <param name="path">Where the list is kept, or <see langword="null"/> for a list that's never saved.</param>
    /// <param name="find">Finds an installed app by name or identifier, or gives <see langword="null"/>.</param>
    public StartupItems(string? path, Func<string, Bundle?> find)
    {
        ArgumentNullException.ThrowIfNull(find);
        Path = path;
        this.find = find;
    }

    /// <summary>Where the list is kept.</summary>
    public string? Path { get; }

    /// <summary>Whether the desktop opened them already since VentureOS started, so a second visit doesn't again.</summary>
    public bool HaveOpened { get; set; }

    /// <summary>The identifiers listed, in order, or none and why when the file can't be read; no file is an empty list.</summary>
    public (IReadOnlyList<string> Identifiers, string? Problem) Load()
    {
        if (Path is not { } path || !File.Exists(path))
            return ([], null);

        try
        {
            var document = FlatXml.Load(path);
            if (document.Root.Name != "StartupItems")
                return ([], $"{path} isn't a list of startup items: it starts with <{document.Root.Name}>.");
            if (document.Root["version"] is { } version && (!int.TryParse(version, out var number) || number > SupportedVersion))
                return ([], $"{path} is version {version}, and this VentureOS reads up to {SupportedVersion}.");

            List<string> identifiers = [];
            foreach (var element in document.Children)
            {
                if (element.Name == "App" && element["identifier"] is { Length: > 0 } identifier && !identifiers.Contains(identifier))
                    identifiers.Add(identifier);
            }

            return (identifiers, null);
        }
        catch (Exception exception) when (exception is FlatXmlException or IOException or UnauthorizedAccessException)
        {
            return ([], $"couldn't read {path}: {exception.Message}");
        }
    }

    /// <summary>Whether an app, by identifier, is on the list.</summary>
    public bool Contains(string identifier) => Load().Identifiers.Contains(identifier);

    /// <summary>The installed app with this name or identifier, or <see langword="null"/>.</summary>
    public Bundle? Find(string app) => find(app);

    /// <summary>Puts an app on the list or takes it off, by name or identifier, and saves it.</summary>
    /// <returns>Whether it was saved; when it wasn't, <paramref name="problem"/> says why, and the list is as it was.</returns>
    public bool TrySet(string app, bool on, [NotNullWhen(false)] out string? problem)
    {
        ArgumentNullException.ThrowIfNull(app);
        var (identifiers, loadProblem) = Load();
        if (loadProblem is not null)
        {
            problem = loadProblem;
            return false;
        }

        // An app that's gone can still be taken off, by its identifier.
        var identifier = find(app)?.Identifier ?? (!on && identifiers.Contains(app) ? app : null);
        if (identifier is null)
        {
            problem = $"there's no app called {app}";
            return false;
        }

        List<string> changed = [.. identifiers.Where(item => item != identifier)];
        if (on)
            changed.Add(identifier);
        return TrySave(changed, out problem);
    }

    // An attribute's text, written so it reads back as itself.
    private static string Escape(string text) =>
        text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal);

    private bool TrySave(List<string> identifiers, [NotNullWhen(false)] out string? problem)
    {
        problem = null;
        if (Path is not { } path)
            return true;

        StringBuilder text = new();
        text.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        text.Append("<!-- The apps that open with the desktop, which Settings and the apps that ask change. -->\n");
        text.Append($"<StartupItems version=\"{SupportedVersion}\">\n");
        foreach (var identifier in identifiers)
            text.Append($"  <App identifier=\"{Escape(identifier)}\"/>\n");
        text.Append("</StartupItems>\n");

        try
        {
            if (System.IO.Path.GetDirectoryName(path) is { Length: > 0 } folder)
                Directory.CreateDirectory(folder);
            File.WriteAllText(path, text.ToString());
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            problem = $"couldn't save {path}: {exception.Message}";
            return false;
        }
    }
}
