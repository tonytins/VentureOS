using System.Text;
using Venture.OS.Bundles;
using Venture.OS.Interface.Xml;

namespace Venture.Roundhouse.Bundles;

/// <summary>
/// File ▸ New App: a window app made from the template beside Roundhouse (<c>Templates/WindowApp</c>), the guide's
/// window example as a start: an <c>Info.xml</c>, a <c>main.lua</c> that opens <c>main.uix</c>, and a window with a
/// label and a button that counts its presses. The app is checked by VentureOS's own reader once it's written.
/// </summary>
public static class AppTemplate
{
    /// <summary>The longest name an app is given here, as a folder's name and a title.</summary>
    public const int LongestName = FolderNames.Longest;

    /// <summary>The template's folder, beside Roundhouse.</summary>
    public static string Folder => Path.Combine(AppContext.BaseDirectory, "Templates", "WindowApp");

    /// <summary>
    /// An identifier for a new app, reverse-DNS from its name, such as <c>dev.example.counter</c>, to start from and
    /// change: <c>dev.example</c> is for trying things, and an app shared with others should use its maker's own.
    /// </summary>
    public static string IdentifierFor(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        StringBuilder last = new();
        foreach (var character in name.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
                last.Append(character);
        }

        return last.Length == 0 || !char.IsAsciiLetter(last[0]) ? $"dev.example.app{last}" : $"dev.example.{last}";
    }

    /// <summary>Why a name can't be a new app's, or <see langword="null"/> when it can.</summary>
    public static string? ProblemWithName(string name) => FolderNames.Problem(name, "app");

    /// <summary>
    /// Makes <c>Name.app</c> in <paramref name="folder"/> from the template, and checks it as VentureOS reads apps.
    /// </summary>
    /// <returns>The new app's folder.</returns>
    /// <exception cref="ArgumentException">The name can't be an app's, there's already an app of that name there, or VentureOS refuses the result, such as for its identifier; nothing is left behind.</exception>
    /// <exception cref="IOException">The app couldn't be written; what was written is removed.</exception>
    public static string Create(string name, string identifier, string folder)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(identifier);
        ArgumentException.ThrowIfNullOrEmpty(folder);
        if (ProblemWithName(name) is { } problem)
            throw new ArgumentException(problem, nameof(name));

        name = name.Trim();
        var app = Path.Combine(Path.GetFullPath(folder), name + Bundle.Extension(BundleKind.App));
        if (Directory.Exists(app) || File.Exists(app))
            throw new ArgumentException($"There's already {Path.GetFileName(app)} in {folder}.", nameof(name));
        if (!Directory.Exists(Folder))
            throw new IOException($"The app template isn't beside Roundhouse, at {Folder}.");

        try
        {
            foreach (var source in Directory.EnumerateFiles(Folder, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(app, Path.GetRelativePath(Folder, source));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);

                // In XML, the name is written as a value is; in Lua, it's only in a comment.
                var xml = source.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || source.EndsWith(".uix", StringComparison.OrdinalIgnoreCase);
                var text = File.ReadAllText(source)
                    .Replace("$NAME$", xml ? FlatXml.EncodeValue(name, '"') : name, StringComparison.Ordinal)
                    .Replace("$IDENTIFIER$", xml ? FlatXml.EncodeValue(identifier, '"') : identifier, StringComparison.Ordinal);
                File.WriteAllText(target, text);
            }

            Bundle.Read(app.Replace('\\', '/'), BundleKind.App);
            return app;
        }
        catch (BundleException exception)
        {
            Directory.Delete(app, recursive: true);
            throw new ArgumentException(exception.Message, nameof(identifier), exception);
        }
        catch
        {
            if (Directory.Exists(app))
                Directory.Delete(app, recursive: true);
            throw;
        }
    }
}
