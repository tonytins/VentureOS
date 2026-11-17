using Venture.OS.Bundles;

namespace Venture.OS.Desktop;

/// <summary>A wallpaper the desktop can show: its name, as the look keeps it, and its bundle.</summary>
/// <param name="Name">The bundle's folder name without <c>.wallpaper</c>, such as <c>Earthset</c>, which the look keeps.</param>
/// <param name="Bundle">The bundle, with its picture and any light and dark ones.</param>
public sealed record Wallpaper(string Name, Bundle Bundle)
{
    /// <summary>A small picture for a picker: the bundle's icon when it has one, and otherwise its picture.</summary>
    public string Thumbnail => Bundle.Icon ?? Bundle.Image!;
}

/// <summary>
/// The wallpapers in a folder, each a <c>Name.wallpaper</c> bundle (<see cref="BundleKind.Wallpaper"/>), by the name
/// the look keeps: the folder's, so renaming a bundle's display name doesn't lose the choice.
/// </summary>
public static class WallpaperCatalog
{
    /// <summary>Every wallpaper in a folder, sorted by name; ones that can't be read are left out, and say why.</summary>
    /// <param name="problems">Where to say why a bundle was left out, or <see langword="null"/>.</param>
    public static IReadOnlyList<Wallpaper> Read(string folder, List<string>? problems = null)
    {
        ArgumentNullException.ThrowIfNull(folder);
        List<Wallpaper> found = [];
        if (!Directory.Exists(folder))
            return found;

        var extension = Bundle.Extension(BundleKind.Wallpaper);
        foreach (var path in Directory.GetDirectories(folder))
        {
            var name = Path.GetFileName(path);
            if (!name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                continue;

            if (Find(folder, name[..^extension.Length], problems) is { } wallpaper)
                found.Add(wallpaper);
        }

        found.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));
        return found;
    }

    /// <summary>
    /// The wallpaper with a name, as the look keeps it, or <see langword="null"/> when there's none or it can't be
    /// read, which <paramref name="problems"/> hears why.
    /// </summary>
    public static Wallpaper? Find(string folder, string name, List<string>? problems = null)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(name);
        if (!DesktopLook.IsWallpaperName(name) || name == DesktopLook.Sky)
            return null;

        var path = $"{folder.TrimEnd('/')}/{name}{Bundle.Extension(BundleKind.Wallpaper)}";
        if (!Directory.Exists(path))
        {
            problems?.Add($"there's no wallpaper named {name} in {folder}");
            return null;
        }

        try
        {
            return new Wallpaper(name, Bundle.Read(path, BundleKind.Wallpaper));
        }
        catch (BundleException exception)
        {
            problems?.Add(exception.Message);
            return null;
        }
    }
}
