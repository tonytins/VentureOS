namespace Venture.OS.Desktop;

/// <summary>A wallpaper the desktop can show: its name, as the look keeps it, and its picture.</summary>
/// <param name="Name">The picture's file name without <c>.png</c>, such as <c>Earthset</c>, which the look keeps.</param>
/// <param name="Path">The full path of the picture, which a picker scales down itself.</param>
public sealed record Wallpaper(string Name, string Path);

/// <summary>
/// The wallpapers in a folder, each a PNG named after it, such as <c>Earthset.png</c>: one picture, shown in light and
/// dark alike, with no thumbnail of its own, since a picker scales it down.
/// </summary>
public static class WallpaperCatalog
{
    /// <summary>A wallpaper's file extension.</summary>
    public const string Extension = ".png";

    /// <summary>Every wallpaper in a folder, sorted by name; files that aren't PNGs, or have names the look can't keep, are passed over.</summary>
    public static IReadOnlyList<Wallpaper> Read(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        List<Wallpaper> found = [];
        if (!Directory.Exists(folder))
            return found;

        foreach (var path in Directory.GetFiles(folder))
        {
            var file = System.IO.Path.GetFileName(path);
            if (!file.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
                continue;

            var name = file[..^Extension.Length];
            if (DesktopLook.IsWallpaperName(name) && name != DesktopLook.Sky)
                found.Add(new Wallpaper(name, path));
        }

        found.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));
        return found;
    }

    /// <summary>
    /// The wallpaper with a name, as the look keeps it, or <see langword="null"/> when there's none, which
    /// <paramref name="problems"/> hears.
    /// </summary>
    public static Wallpaper? Find(string folder, string name, List<string>? problems = null)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(name);
        if (!DesktopLook.IsWallpaperName(name) || name == DesktopLook.Sky)
            return null;

        var path = $"{folder.TrimEnd('/')}/{name}{Extension}";
        if (File.Exists(path))
            return new Wallpaper(name, path);

        problems?.Add($"there's no wallpaper named {name} in {folder}");
        return null;
    }
}
