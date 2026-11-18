namespace Venture.Roundhouse.Simulator;

/// <summary>
/// The disk a simulated VentureOS runs from: a fresh copy of <c>rootfs/</c> in a temporary folder, laid out as the
/// kernel lays out its RAM disk, with the system's files in <c>mnt/system</c> and the user's in <c>mnt/user</c>.
/// Nothing an app writes outlasts the run: disposing of it deletes the copy.
/// </summary>
public sealed class SimulatedDisk : IDisposable
{
    /// <summary>Where VentureOS mounts its RAM disk, as the kernel does.</summary>
    public const string MountPoint = "/mnt";

    /// <summary>The system's folder, as VentureOS names it.</summary>
    public const string SystemFolder = MountPoint + "/system";

    /// <summary>The user's folder, as VentureOS names it.</summary>
    public const string UserFolder = MountPoint + "/user";

    private SimulatedDisk(string root) => Root = root;

    /// <summary>The temporary folder on this computer that stands for VentureOS's <c>/</c>.</summary>
    public string Root { get; }

    /// <summary>
    /// Copies <paramref name="rootfs"/>, which holds <c>system</c> and <c>user</c>, into a new temporary folder.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException">There's no <c>system</c> or <c>user</c> folder in it.</exception>
    /// <exception cref="IOException">The copy couldn't be made; what was made is removed.</exception>
    public static SimulatedDisk Create(string rootfs)
    {
        ArgumentNullException.ThrowIfNull(rootfs);
        foreach (var part in (string[])["system", "user"])
        {
            if (!Directory.Exists(Path.Combine(rootfs, part)))
                throw new DirectoryNotFoundException($"{rootfs} has no {part} folder, so VentureOS can't run from it.");
        }

        RemoveLeftovers();
        SimulatedDisk disk = new(Path.Combine(Path.GetTempPath(), $"{Prefix}{Environment.ProcessId}-{Guid.NewGuid():N}"));
        try
        {
            Copy(Path.Combine(rootfs, "system"), disk.HostPath(SystemFolder));
            Copy(Path.Combine(rootfs, "user"), disk.HostPath(UserFolder));
            return disk;
        }
        catch
        {
            disk.Dispose();
            throw;
        }
    }

    /// <summary>What every run's folder in the temporary folder starts with, before the process's ID.</summary>
    public const string Prefix = "roundhouse-run-";

    /// <summary>
    /// Deletes runs' copies left behind by a Roundhouse that has ended without cleaning up, such as one that crashed
    /// or was killed: those whose process isn't running. A running Roundhouse's are left alone.
    /// </summary>
    /// <returns>How many it deleted.</returns>
    public static int RemoveLeftovers()
    {
        var removed = 0;
        foreach (var folder in Directory.EnumerateDirectories(Path.GetTempPath(), Prefix + "*"))
        {
            var rest = Path.GetFileName(folder)[Prefix.Length..];
            var dash = rest.IndexOf('-', StringComparison.Ordinal);
            if (dash <= 0 || !int.TryParse(rest[..dash], out var process) || IsRunning(process))
                continue;

            try
            {
                Directory.Delete(folder, recursive: true);
                removed++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Another user's, or one in use; it's left for that process or the system to clear.
            }
        }

        return removed;
    }

    private static bool IsRunning(int process)
    {
        try
        {
            using var found = System.Diagnostics.Process.GetProcessById(process);
            return !found.HasExited;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// The <c>rootfs/</c> Roundhouse runs VentureOS from: beside it when packaged, or the repository's while it's
    /// built from source; <see langword="null"/> when there's neither.
    /// </summary>
    public static string? FindRootfs()
    {
        var beside = Path.Combine(AppContext.BaseDirectory, "rootfs");
        if (Directory.Exists(Path.Combine(beside, "system")))
            return beside;

        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "GitInfo.txt")) && Directory.Exists(Path.Combine(folder.FullName, "rootfs", "system")))
                return Path.Combine(folder.FullName, "rootfs");
        }

        return null;
    }

    /// <summary>
    /// Where one of the disk's folders, named as VentureOS names it (<c>/mnt/user</c>), is on this computer. It's for
    /// the folders Roundhouse sets up itself; scripts' own paths go through the file root.
    /// </summary>
    /// <exception cref="ArgumentException">The path isn't an absolute VentureOS path, or climbs out with <c>..</c>.</exception>
    public string HostPath(string venturePath)
    {
        ArgumentNullException.ThrowIfNull(venturePath);
        if (!venturePath.StartsWith('/') || venturePath.Split('/').Contains(".."))
            throw new ArgumentException($"\"{venturePath}\" isn't an absolute VentureOS path.", nameof(venturePath));
        return Path.Combine(Root, venturePath.TrimStart('/'));
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
            Directory.Delete(Root, recursive: true);
    }

    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }
}
