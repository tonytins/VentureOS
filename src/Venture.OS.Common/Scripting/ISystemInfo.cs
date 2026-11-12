namespace Venture.OS.Scripting;

/// <summary>
/// What the kernel tells Lua scripts about the system, through the <c>venture</c> table.
/// </summary>
/// <remarks>
/// The kernel implements this with Cosmos; tests use a fake. Values the kernel can't read reliably are
/// <see langword="null"/>, and scripts show them as unknown.
/// </remarks>
public interface ISystemInfo
{
    /// <summary>The OS name, such as <c>VentureOS</c>.</summary>
    string Name { get; }

    /// <summary>The version, such as <c>0.2.7</c>.</summary>
    string Version { get; }

    /// <summary>The build number derived from the commit.</summary>
    uint BuildNumber { get; }

    /// <summary>The commit the kernel was built from.</summary>
    string Commit { get; }

    /// <summary>The Cosmos version the kernel runs on.</summary>
    string CosmosVersion { get; }

    /// <summary>The time since the CPU started counting, or <see langword="null"/> if unknown.</summary>
    TimeSpan? Uptime { get; }

    /// <summary>The free and total memory, or <see langword="null"/> if unknown.</summary>
    MemoryInfo? Memory { get; }

    /// <summary>The primary network adapter, or <see langword="null"/> when there's none.</summary>
    NetworkInfo? Network { get; }

    /// <summary>
    /// The system's own folder, such as <c>/mnt/system</c>, which <c>venture.files</c> won't change, or
    /// <see langword="null"/> for none.
    /// </summary>
    /// <remarks>It's rebuilt from the kernel at every boot, so changes there wouldn't last anyway.</remarks>
    string? SystemDirectory { get; }

    /// <summary>
    /// The free and total space where <paramref name="path"/> is stored, or <see langword="null"/> if unknown.
    /// </summary>
    /// <param name="path">A full path.</param>
    SpaceInfo? Space(string path);

    /// <summary>
    /// The desktop's look, which <c>venture.appearance</c> and <c>venture.setAppearance</c> read and change, or
    /// <see langword="null"/> on a system without one.
    /// </summary>
    IAppearance? Appearance { get; }
}

/// <summary>Free and total space on a file system, in bytes.</summary>
public sealed record SpaceInfo(ulong FreeBytes, ulong TotalBytes);

/// <summary>Free and total memory, in bytes.</summary>
public sealed record MemoryInfo(ulong FreeBytes, ulong TotalBytes);

/// <summary>
/// The primary network adapter. The address, subnet mask, and gateway are <see langword="null"/> until IPv4 is
/// configured.
/// </summary>
public sealed record NetworkInfo(
    string Device,
    string MacAddress,
    bool LinkUp,
    bool Ready,
    string? Address,
    string? SubnetMask,
    string? Gateway);
