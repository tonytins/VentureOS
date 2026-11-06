namespace Venture.OS.Scripting;

/// <summary>
/// Reads the system's details from Cosmos for the Lua <c>venture</c> table.
/// </summary>
/// <remarks>
/// Every property reads the current value, so scripts see changes such as a new DHCP address. A value that can't be
/// read reliably is <see langword="null"/>.
/// </remarks>
internal sealed class KernelSystemInfo : ISystemInfo
{
    public string Name => HostInfo.Name;
    public string Version => HostInfo.Version;
    public uint BuildNumber => HostInfo.BuildNumber;
    public string Commit => ThisAssembly.Git.Commit;
    public string CosmosVersion => Sys.Kernel.VersionString;

    /// <summary>
    /// Time since the CPU started counting, read from the Stopwatch (the timestamp counter on x64).
    /// </summary>
    public TimeSpan? Uptime
    {
        get
        {
            var frequency = Stopwatch.Frequency;
            if (frequency <= 0)
                return null;

            var seconds = Stopwatch.GetTimestamp() / frequency;
            return seconds < 0 ? null : TimeSpan.FromSeconds(seconds);
        }
    }

    public MemoryInfo? Memory
    {
        get
        {
            var pageSize = MemoryDiagnostics.PageSizeBytes;
            var total = MemoryDiagnostics.TotalPages;
            var free = MemoryDiagnostics.FreePages;

            // Guard against a page count that would overflow when converted to bytes.
            if (pageSize == 0 || total > ulong.MaxValue / pageSize || free > total)
                return null;

            return new MemoryInfo(free * pageSize, total * pageSize);
        }
    }

    public NetworkInfo? Network
    {
        get
        {
            // NetworkManager.Primary is a handle that may name no device, so check for one first.
            if (NetworkManager.DeviceCount == 0)
                return null;

            var config = NetworkManager.Primary.IPConfig;
            return new NetworkInfo(
                NetworkManager.Name ?? "unknown",
                NetworkManager.MacAddress?.ToString() ?? "unknown",
                NetworkManager.LinkUp,
                NetworkManager.Ready,
                config?.Address.ToString(),
                config?.SubnetMask.ToString(),
                config?.DefaultGateway.ToString());
        }
    }
}
