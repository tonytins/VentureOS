namespace Venture.OS.Shell.Commands;

/// <summary>
/// Shows the version, uptime, and memory.
/// </summary>
internal sealed class InfoCommand : IShellCommand
{
    private const ulong BytesPerMebibyte = 1024 * 1024;

    public string Name => "info";
    public string Summary => "Show the version, uptime, and memory.";
    public string Usage => "info";
    public int MinArguments => 0;
    public int MaxArguments => 0;

    public void Execute(ShellContext context, IReadOnlyList<string> arguments)
    {
        var output = context.Output;

        output.WriteLine(HostInfo.DisplayVersion);
        output.WriteLine($"Commit:  {ThisAssembly.Git.Commit}");
        output.WriteLine($"Cosmos:  {Sys.Kernel.VersionString}");
        output.WriteLine($"Uptime:  {FormatUptime()}");
        output.WriteLine($"Memory:  {FormatMemory()}");
    }

    /// <summary>
    /// Time since the CPU started counting, read from the Stopwatch (the timestamp counter on x64).
    /// </summary>
    private static string FormatUptime()
    {
        var frequency = Stopwatch.Frequency;
        if (frequency <= 0)
            return "unknown";

        var seconds = Stopwatch.GetTimestamp() / frequency;
        if (seconds < 0)
            return "unknown";

        var uptime = TimeSpan.FromSeconds(seconds);
        return uptime.Days > 0
            ? $"{uptime.Days}d {uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}"
            : $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
    }

    private static string FormatMemory()
    {
        var pageSize = MemoryDiagnostics.PageSizeBytes;
        var total = MemoryDiagnostics.TotalPages;
        var free = MemoryDiagnostics.FreePages;

        // Guard against a page count that would overflow when converted to bytes.
        if (pageSize == 0 || total > ulong.MaxValue / pageSize || free > total)
            return "unknown";

        var totalMebibytes = total * pageSize / BytesPerMebibyte;
        var freeMebibytes = free * pageSize / BytesPerMebibyte;
        return $"{freeMebibytes} MiB free of {totalMebibytes} MiB";
    }
}
