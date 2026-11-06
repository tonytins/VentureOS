namespace Venture.OS.Scripting;

/// <summary>
/// Writes the files built into the kernel, such as the Lua system scripts, to a directory on disk.
/// </summary>
public static class SystemDirectory
{
    /// <summary>
    /// Writes every file in <paramref name="files"/> under <paramref name="root"/>, creating folders as needed and
    /// replacing files that are already there.
    /// </summary>
    /// <remarks>
    /// Every path is checked before anything is written, so a bad path leaves the disk untouched. An I/O failure
    /// partway through can still leave some files written.
    /// </remarks>
    /// <param name="root">The directory to install into, such as <c>/mnt/system</c>.</param>
    /// <param name="files">The files to write.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="root"/> is empty, or a file's path is invalid or appears twice.
    /// </exception>
    /// <exception cref="IOException">A folder or file couldn't be written.</exception>
    public static void Install(string root, IReadOnlyList<EmbeddedFile> files)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        ArgumentNullException.ThrowIfNull(files);

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            ArgumentNullException.ThrowIfNull(file, nameof(files));
            ArgumentNullException.ThrowIfNull(file.Contents, nameof(files));

            if (!IsValidPath(file.Path))
                throw new ArgumentException($"\"{file.Path}\" isn't a valid path for a system file.", nameof(files));

            // FAT ignores case, so two paths that differ only in case would overwrite each other.
            if (!seen.Add(file.Path))
                throw new ArgumentException($"\"{file.Path}\" appears more than once.", nameof(files));
        }

        var trimmedRoot = root.TrimEnd('/');
        Directory.CreateDirectory(trimmedRoot);

        foreach (var file in files)
        {
            var path = $"{trimmedRoot}/{file.Path}";

            var folderEnd = path.LastIndexOf('/');
            if (folderEnd > trimmedRoot.Length)
                Directory.CreateDirectory(path[..folderEnd]);

            File.WriteAllBytes(path, file.Contents);
        }
    }

    /// <summary>
    /// A path is valid when it's relative, uses <c>/</c> between folders, and every part is a plain name, so it
    /// can't climb out of the directory or name the directory itself.
    /// </summary>
    internal static bool IsValidPath(string? path)
    {
        if (string.IsNullOrEmpty(path) || path.Contains('\\'))
            return false;

        foreach (var part in path.Split('/'))
        {
            if (part is "" or "." or "..")
                return false;
        }

        return true;
    }
}
