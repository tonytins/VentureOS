using Venture.OS.Shell;

namespace Venture.OS.Scripting;

/// <summary>
/// The folder that stands for VentureOS's <c>/</c> when VentureOS runs inside another system, as it does in
/// Roundhouse: scripts' paths, such as <c>/mnt/user/notes.txt</c>, are found under it, and nothing a script names
/// leads outside it, whether by <c>..</c> or by a symbolic link.
/// </summary>
/// <remarks>
/// <para>The kernel has no root (<see cref="ISystemInfo.Root"/> is <see langword="null"/>): its disk is VentureOS's
/// whole world, so <see cref="Host(FileRoot?, string)"/> and <see cref="Venture(FileRoot?, string)"/> hand paths back
/// unchanged and nothing about it changes.</para>
/// <para>Inside VentureOS's own code, paths the system sets up (the apps' folders, the settings) are the host's, and
/// only paths that cross to or from scripts are converted: what a script names on its way in, and what it's told on
/// the way out.</para>
/// <para>A link is checked when a path is converted, so one made between that and the file being opened isn't caught;
/// scripts can't make links, so only the host could.</para>
/// </remarks>
public sealed class FileRoot
{
    // Enough for any real chain of links; more is a loop.
    private const int MaximumLinks = 40;

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly string realFolder;

    /// <summary>Makes <paramref name="hostFolder"/>, which must exist, VentureOS's <c>/</c>.</summary>
    /// <exception cref="DirectoryNotFoundException">There's no such folder.</exception>
    public FileRoot(string hostFolder)
    {
        ArgumentException.ThrowIfNullOrEmpty(hostFolder);
        if (!Directory.Exists(hostFolder))
            throw new DirectoryNotFoundException($"{hostFolder} isn't a folder, so it can't be VentureOS's root.");

        HostFolder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(hostFolder));
        realFolder = RealPath(HostFolder);
    }

    /// <summary>The folder on the host that stands for VentureOS's <c>/</c>.</summary>
    public string HostFolder { get; }

    /// <summary>A script's path on the host, or the path itself when there's no root, as in the kernel.</summary>
    /// <inheritdoc cref="ToHost(string)" path="/exception"/>
    public static string Host(FileRoot? root, string path) => root is null ? path : root.ToHost(path);

    /// <summary>A host path as a script sees it, or the path itself when there's no root.</summary>
    /// <inheritdoc cref="ToVenture(string)" path="/exception"/>
    public static string Venture(FileRoot? root, string path) => root is null ? path : root.ToVenture(path);

    /// <summary>
    /// Where a full VentureOS path is on the host. <c>..</c> stops at <c>/</c>, as it does in VentureOS.
    /// </summary>
    /// <exception cref="ArgumentException">The path isn't a full VentureOS path, one starting with <c>/</c>.</exception>
    /// <exception cref="UnauthorizedAccessException">A symbolic link on the way leads outside VentureOS's files.</exception>
    public string ToHost(string venturePath)
    {
        ArgumentNullException.ThrowIfNull(venturePath);
        if (!ShellPaths.IsFull(venturePath))
            throw new ArgumentException($"\"{venturePath}\" isn't a full VentureOS path.", nameof(venturePath));

        var resolved = ShellPaths.Resolve("/", venturePath);
        var host = resolved == "/"
            ? HostFolder
            : Path.Join(HostFolder, resolved[1..].Replace('/', Path.DirectorySeparatorChar));

        if (!IsInside(RealPath(host), realFolder))
            throw new UnauthorizedAccessException($"{resolved} leads outside VentureOS's files.");
        return host;
    }

    /// <summary>How a script sees a path on the host, such as <c>/mnt/user/apps/Notes.app</c>.</summary>
    /// <exception cref="ArgumentException">The path isn't inside VentureOS's files.</exception>
    public string ToVenture(string hostPath)
    {
        ArgumentNullException.ThrowIfNull(hostPath);
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(hostPath));
        if (!IsInside(full, HostFolder))
            throw new ArgumentException($"{hostPath} isn't inside VentureOS's files.", nameof(hostPath));

        var relative = Path.GetRelativePath(HostFolder, full);
        return relative == "." ? "/" : "/" + relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static bool IsInside(string path, string folder) =>
        path.Equals(folder, PathComparison)
        || path.StartsWith(folder.EndsWith(Path.DirectorySeparatorChar) ? folder : folder + Path.DirectorySeparatorChar, PathComparison);

    /// <summary>
    /// A path with every symbolic link along it followed, its targets' own folders included, as far as the path
    /// exists; the rest is kept as written.
    /// </summary>
    private static string RealPath(string path) => RealPath(path, 0);

    private static string RealPath(string path, int links)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full) ?? "/";
        var current = root;
        foreach (var part in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            var next = Path.Join(current, part);
            FileSystemInfo info = Directory.Exists(next) ? new DirectoryInfo(next) : new FileInfo(next);
            if (info.Exists && info.LinkTarget is not null)
            {
                if (++links > MaximumLinks)
                    throw new UnauthorizedAccessException($"{path} goes round a loop of links.");

                // A link's target is followed in turn, so links in the folders it's in are followed too.
                var target = info.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? next;
                next = RealPath(target, links);
            }

            current = next;
        }

        return Path.TrimEndingDirectorySeparator(current);
    }
}
