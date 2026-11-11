namespace Venture.OS.Shell;

/// <summary>
/// Turns a path the user typed into a full one, against the shell's current folder.
/// </summary>
/// <remarks>
/// Paths use <c>/</c> between folders, as Cosmos's file system does. <c>.</c> is the folder itself and <c>..</c> its
/// parent, and <c>..</c> at <c>/</c> stays at <c>/</c>. Empty parts, as in <c>a//b</c>, are skipped. This only works out
/// a name; whether anything is there is for the caller to check.
/// </remarks>
public static class ShellPaths
{
    /// <summary>
    /// The full path that <paramref name="path"/> names, starting from <paramref name="currentDirectory"/> unless it
    /// starts with <c>/</c>, such as <c>/mnt/notes</c> for <c>notes</c> in <c>/mnt</c>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="currentDirectory"/> isn't a full path.</exception>
    public static string Resolve(string currentDirectory, string path)
    {
        ArgumentNullException.ThrowIfNull(currentDirectory);
        ArgumentNullException.ThrowIfNull(path);
        if (!IsFull(currentDirectory))
            throw new ArgumentException($"\"{currentDirectory}\" isn't a full path.", nameof(currentDirectory));

        List<string> parts = [];
        var start = path.StartsWith('/') ? path : $"{currentDirectory}/{path}";
        foreach (var part in start.Split('/'))
        {
            switch (part)
            {
                case "" or ".":
                    break;
                case "..":
                    if (parts.Count > 0)
                        parts.RemoveAt(parts.Count - 1);
                    break;
                default:
                    parts.Add(part);
                    break;
            }
        }

        return "/" + string.Join('/', parts);
    }

    /// <summary>Whether <paramref name="path"/> is a full path, one that starts with <c>/</c>.</summary>
    public static bool IsFull(string? path) => path is not null && path.StartsWith('/');

    /// <summary>
    /// Whether <paramref name="path"/> is <paramref name="folder"/> or inside it, comparing as FAT does, ignoring
    /// case. Both must be full, resolved paths.
    /// </summary>
    public static bool IsWithin(string path, string folder)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(folder);

        var root = folder.TrimEnd('/');
        return path.Equals(root, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase)
            || root.Length == 0;
    }
}
