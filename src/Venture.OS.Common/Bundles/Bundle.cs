using Venture.OS.Scripting;
using Venture.OS.Xml;

namespace Venture.OS.Bundles;

/// <summary>The kinds of bundle: a folder that acts as one thing, after macOS's bundles.</summary>
public enum BundleKind
{
    /// <summary>An application, <c>Name.app</c>, with <c>&lt;App&gt;</c> as its root.</summary>
    App,

    /// <summary>A look for the desktop, <c>Name.theme</c>, with <c>&lt;Theme&gt;</c>. Not opened yet.</summary>
    Theme,

    /// <summary>Shared Lua modules with resources, <c>Name.library</c>, with <c>&lt;Library&gt;</c>. Not loaded yet.</summary>
    Library,
}

/// <summary>An attribute a reader didn't know and skipped, which may be a typo; see <see cref="Bundle.IgnoredAttributes"/>.</summary>
public sealed record IgnoredAttribute(string Element, string Attribute, int Line);

/// <summary>Why a bundle couldn't be read.</summary>
public sealed class BundleException(string message) : Exception(message);

/// <summary>
/// A bundle read from its <c>Contents/Info.xml</c>: what it is, who it is, and where its parts are.
/// </summary>
/// <remarks>
/// <code>
/// Notes.app/
/// └── Contents/
///     ├── Info.xml      &lt;App version="1"&gt;&lt;Info identifier=... name=... /&gt;&lt;/App&gt;
///     ├── Lua/          code; main.lua runs when an app opens
///     └── Resources/    images, .uix interfaces, and so on
/// </code>
/// <para><c>Info.xml</c> follows UIScript's compatibility rules: a <c>version</c> newer than 1 or a
/// <c>requires</c> feature it doesn't know refuses the bundle, as does an element it doesn't know; an attribute it
/// doesn't know is skipped and listed in <see cref="IgnoredAttributes"/>. A bundle reads completely or throws.</para>
/// <para><c>&lt;Info&gt;</c> takes <c>identifier</c> (required, reverse-DNS such as <c>dev.tonytins.notes</c>),
/// <c>name</c> (required), <c>version</c>, <c>needs</c> (scopes, as in <c>-- Needs:</c>), <c>main</c> (an app's
/// first script, by default <c>Contents/Lua/main.lua</c>), and <c>icon</c>. Paths are relative to the bundle and
/// must stay inside it.</para>
/// </remarks>
public sealed class Bundle
{
    /// <summary>The newest <c>Info.xml</c> format version this reader understands.</summary>
    public const int SupportedVersion = 1;

    /// <summary>Where an app's first script is, when <c>main</c> doesn't say.</summary>
    public const string DefaultMain = "Contents/Lua/main.lua";

    private const string InfoPath = "Contents/Info.xml";

    private static readonly string[] InfoAttributes = ["identifier", "name", "version", "needs", "main", "icon"];

    private Bundle(
        BundleKind kind,
        string path,
        string identifier,
        string name,
        string? version,
        ScriptScopes needs,
        string? main,
        string? icon,
        IReadOnlyList<IgnoredAttribute> ignoredAttributes)
    {
        Kind = kind;
        Path = path;
        Identifier = identifier;
        Name = name;
        Version = version;
        Needs = needs;
        Main = main;
        Icon = icon;
        IgnoredAttributes = ignoredAttributes;
    }

    /// <summary>What kind of bundle it is.</summary>
    public BundleKind Kind { get; }

    /// <summary>The bundle folder, such as <c>/mnt/system/apps/Notes.app</c>.</summary>
    public string Path { get; }

    /// <summary>Its reverse-DNS identity, such as <c>dev.tonytins.notes</c>.</summary>
    public string Identifier { get; }

    /// <summary>Its name as people see it, such as <c>Notes</c>.</summary>
    public string Name { get; }

    /// <summary>Its own version, such as <c>1.0</c>, or <see langword="null"/>.</summary>
    public string? Version { get; }

    /// <summary>The scopes it declares, for all its code.</summary>
    public ScriptScopes Needs { get; }

    /// <summary>The full path of an app's first script; <see langword="null"/> for other kinds.</summary>
    public string? Main { get; }

    /// <summary>The full path of its icon, or <see langword="null"/>.</summary>
    public string? Icon { get; }

    /// <summary>Attributes the reader didn't know and skipped. Worth logging while developing, to catch typos.</summary>
    public IReadOnlyList<IgnoredAttribute> IgnoredAttributes { get; }

    // Spelled out rather than through Enum.GetValues or ToString, which lean on runtime type metadata that
    // Cosmos's runtime may not have.
    private static readonly BundleKind[] Kinds = [BundleKind.App, BundleKind.Theme, BundleKind.Library];

    /// <summary>The root element name for a kind, such as <c>App</c>.</summary>
    public static string RootName(BundleKind kind) => kind switch
    {
        BundleKind.App => "App",
        BundleKind.Theme => "Theme",
        BundleKind.Library => "Library",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>The kind as a word in a sentence, such as <c>an app</c>.</summary>
    private static string Describe(BundleKind kind) => kind switch
    {
        BundleKind.App => "an app",
        BundleKind.Theme => "a theme",
        BundleKind.Library => "a library",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>The folder extension for a kind, such as <c>.app</c>.</summary>
    public static string Extension(BundleKind kind) => kind switch
    {
        BundleKind.App => ".app",
        BundleKind.Theme => ".theme",
        BundleKind.Library => ".library",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>
    /// Reads the bundle at <paramref name="path"/>, which must be the <paramref name="expected"/> kind.
    /// </summary>
    /// <param name="path">The bundle folder, as a full path with <c>/</c> between folders.</param>
    /// <exception cref="BundleException">It isn't a readable bundle of that kind; the message says why.</exception>
    public static Bundle Read(string path, BundleKind expected)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var root = path.TrimEnd('/');
        var label = System.IO.Path.GetFileName(root);
        if (!Directory.Exists(root))
            throw new BundleException($"There's no bundle at {root}.");

        var infoPath = $"{root}/{InfoPath}";
        if (!File.Exists(infoPath))
            throw new BundleException($"{label} has no {InfoPath}, so it isn't a bundle.");

        FlatXmlDocument document;
        try
        {
            document = FlatXml.Load(infoPath);
        }
        catch (FlatXmlException exception)
        {
            throw new BundleException($"{label}'s {InfoPath}, line {exception.Line}: {exception.Reason}");
        }
        catch (IOException exception)
        {
            throw new BundleException($"{label}'s {InfoPath} couldn't be read: {exception.Message}");
        }

        var kind = ReadKind(document.Root, expected, label);
        CheckVersion(document.Root, label);

        List<IgnoredAttribute> ignored = [];
        FlatXmlElement? info = null;
        foreach (var child in document.Children)
        {
            if (child.Name != "Info")
                throw Problem(label, child.Line, $"<{child.Name}> isn't something this VentureOS knows.");
            if (info is not null)
                throw Problem(label, child.Line, "There's more than one <Info>.");
            info = child;
        }

        if (info is null)
            throw new BundleException($"{label}'s {InfoPath} has no <Info>.");

        foreach (var (attribute, _) in info.Attributes)
        {
            if (Array.IndexOf(InfoAttributes, attribute) < 0)
                ignored.Add(new IgnoredAttribute("Info", attribute, info.Line));
        }

        var identifier = Required(info, "identifier", label);
        if (!IsIdentifier(identifier))
            throw Problem(label, info.Line,
                $"\"{identifier}\" isn't a reverse-DNS identifier, such as dev.example.notes: use letters, digits, and hyphens, between dots.");

        var name = Required(info, "name", label);
        var needs = ReadNeeds(info, label);
        var icon = info["icon"] is { } iconPath ? Inside(root, iconPath, "icon", label, info.Line) : null;

        string? main = null;
        if (kind == BundleKind.App)
        {
            main = Inside(root, info["main"] ?? DefaultMain, "main", label, info.Line);
            if (!File.Exists(main))
                throw new BundleException($"{label} has no {main[(root.Length + 1)..]} to run.");
        }

        return new Bundle(kind, root, identifier, name, info["version"], needs, main, icon, ignored);
    }

    private static BundleKind ReadKind(FlatXmlElement root, BundleKind expected, string label)
    {
        foreach (var kind in Kinds)
        {
            if (root.Name != RootName(kind))
                continue;
            if (kind != expected)
                throw new BundleException($"{label} is {Describe(kind)}, not {Describe(expected)}.");
            return kind;
        }

        throw Problem(label, root.Line, $"<{root.Name}> isn't a kind of bundle this VentureOS knows.");
    }

    private static void CheckVersion(FlatXmlElement root, string label)
    {
        if (root["version"] is { } text
            && (!int.TryParse(text, out var version) || version < 1))
            throw Problem(label, root.Line, $"version=\"{text}\" isn't a format version, such as 1.");

        if (root["version"] is { } newer && int.Parse(newer) > SupportedVersion)
            throw Problem(label, root.Line,
                $"It's format version {newer}, and this VentureOS reads up to {SupportedVersion}; it needs a newer VentureOS.");

        // No features are defined yet, so any required one is one this reader doesn't know.
        if (root["requires"] is { } requires
            && requires.Split(' ', StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } features)
            throw Problem(label, root.Line,
                $"It requires \"{features[0]}\", which this VentureOS doesn't support; it needs a newer VentureOS.");
    }

    private static ScriptScopes ReadNeeds(FlatXmlElement info, string label)
    {
        var needs = ScriptScopes.None;
        foreach (var name in (info["needs"] ?? "").Split([' ', ','], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!ScriptScopeNames.TryParse(name, out var scope, out var error))
                throw Problem(label, info.Line, $"needs: {error}");
            needs |= scope;
        }

        return needs;
    }

    private static string Required(FlatXmlElement info, string attribute, string label) =>
        string.IsNullOrWhiteSpace(info[attribute])
            ? throw Problem(label, info.Line, $"<Info> needs {attribute}=\"...\".")
            : info[attribute]!.Trim();

    /// <summary>The full path of a path inside the bundle, refusing one that would leave it.</summary>
    private static string Inside(string root, string relative, string attribute, string label, int line)
    {
        if (!SystemDirectory.IsValidPath(relative))
            throw Problem(label, line,
                $"{attribute}=\"{relative}\" must be a path inside the bundle, such as Contents/Resources/AppIcon.png.");
        return $"{root}/{relative}";
    }

    private static bool IsIdentifier(string identifier)
    {
        var parts = identifier.Split('.');
        if (parts.Length < 2)
            return false;

        foreach (var part in parts)
        {
            if (part.Length == 0 || part[0] == '-' || part[^1] == '-')
                return false;
            foreach (var character in part)
            {
                if (!char.IsAsciiLetterOrDigit(character) && character != '-')
                    return false;
            }
        }

        return true;
    }

    private static BundleException Problem(string label, int line, string message) =>
        new($"{label}'s {InfoPath}, line {line}: {message}");
}
