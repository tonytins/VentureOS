using Venture.OS.Scripting;

namespace Venture.OS.Tests.Scripting;

/// <summary>
/// Fixed system details for Lua tests, with every value settable.
/// </summary>
internal sealed class FakeSystemInfo : ISystemInfo
{
    public string Name { get; init; } = "VentureOS";
    public string Version { get; init; } = "1.2.3";
    public uint BuildNumber { get; init; } = 456;
    public string Commit { get; init; } = "abc1234";
    public string CosmosVersion { get; init; } = "3.0.94";
    public TimeSpan? Uptime { get; init; } = TimeSpan.FromSeconds(3725);
    public MemoryInfo? Memory { get; init; } = new(100UL * 1024 * 1024, 512UL * 1024 * 1024);

    public NetworkInfo? Network { get; init; } =
        new("e1000e", "52:54:00:12:34:56", true, false, "10.0.2.15", "255.255.255.0", "10.0.2.2");

    /// <summary>When set, reading <see cref="Uptime"/> throws it, as a failing device might.</summary>
    public Exception? UptimeFailure { get; init; }

    TimeSpan? ISystemInfo.Uptime => UptimeFailure is null ? Uptime : throw UptimeFailure;
}
