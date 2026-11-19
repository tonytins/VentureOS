using System.Text;

namespace Venture.OS.Vdb;

/// <summary>One file of an app to send, by its path inside the bundle, such as <c>Contents/Lua/main.lua</c>.</summary>
public sealed record VdbAppFile(string Path, byte[] Contents);

/// <summary>
/// An app ready to send over vdb: its folder's name and its files, checked against the limits VentureOS applies, so an
/// app that can't be sent is refused before connecting, with the reason. VentureOS checks it again, and as an app.
/// </summary>
public sealed class VdbApp
{
    private VdbApp(string name, IReadOnlyList<VdbAppFile> files)
    {
        Name = name;
        Files = files;
    }

    /// <summary>The folder's name, such as <c>Dice.app</c>.</summary>
    public string Name { get; }

    /// <summary>Its files, sorted by path, so the same app is always sent the same way.</summary>
    public IReadOnlyList<VdbAppFile> Files { get; }

    /// <summary>Makes an app from its parts.</summary>
    /// <exception cref="ArgumentException">It can't be sent as it is; the message says why.</exception>
    public static VdbApp Create(string name, IEnumerable<VdbAppFile> files)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(files);
        if (VdbNames.ProblemWithApp(name) is { } problem)
            throw new ArgumentException(problem, nameof(name));

        var sorted = files.OrderBy(file => file.Path, StringComparer.Ordinal).ToList();
        if (sorted.Count > VdbProtocol.MaximumFiles)
            throw new ArgumentException($"An app sent over vdb has at most {VdbProtocol.MaximumFiles} files.", nameof(files));

        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var file in sorted)
        {
            if (VdbNames.ProblemWithPath(file.Path) is { } pathProblem)
                throw new ArgumentException(pathProblem, nameof(files));
            if (!paths.Add(file.Path))
                throw new ArgumentException($"{file.Path} is in the app twice, which VentureOS's files can't tell apart.", nameof(files));
            if (file.Contents.Length > VdbProtocol.MaximumFileSize)
                throw new ArgumentException($"{file.Path} is larger than the {VdbProtocol.MaximumFileSize / (1024 * 1024)} MiB a file sent over vdb may be.", nameof(files));
            total += file.Contents.Length;
        }

        if (total > VdbProtocol.MaximumBundleSize)
            throw new ArgumentException($"An app sent over vdb is at most {VdbProtocol.MaximumBundleSize / (1024 * 1024)} MiB.", nameof(files));
        return new VdbApp(name, sorted);
    }

    /// <summary>
    /// Reads an app's folder: every regular file under it, with any of <paramref name="edits"/>, by full path, in
    /// place of what's on disk, as an editor's unsaved files. A symbolic link is refused, since it could name
    /// something outside the app.
    /// </summary>
    /// <exception cref="ArgumentException">It can't be sent as it is.</exception>
    /// <exception cref="IOException">A file couldn't be read.</exception>
    public static VdbApp Read(string folder, IReadOnlyDictionary<string, string>? edits = null)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Directory.Exists(root))
            throw new ArgumentException($"There's no folder at {folder}.", nameof(folder));

        List<VdbAppFile> files = [];
        foreach (var entry in new DirectoryInfo(root).EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
        {
            var path = Path.GetRelativePath(root, entry.FullName).Replace(Path.DirectorySeparatorChar, '/');
            if (entry.LinkTarget is not null)
                throw new ArgumentException($"{path} is a symbolic link, which vdb doesn't send.", nameof(folder));
            if (entry is not FileInfo)
                continue;

            var contents = edits is not null && edits.TryGetValue(entry.FullName, out var edited)
                ? Encoding.UTF8.GetBytes(edited)
                : File.ReadAllBytes(entry.FullName);
            files.Add(new VdbAppFile(path, contents));
        }

        return Create(Path.GetFileName(root), files);
    }
}
