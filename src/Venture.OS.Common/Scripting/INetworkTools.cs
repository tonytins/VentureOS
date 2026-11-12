namespace Venture.OS.Scripting;

/// <summary>
/// What scripts can ask of the network, through <c>venture.resolve</c> and <c>venture.ping</c>: the kernel
/// implements it with Cosmos's DNS and ICMP clients, and tests with a fake. Every call waits no longer than its
/// timeout, and reports a failure as its result rather than throwing.
/// </summary>
public interface INetworkTools
{
    /// <summary>
    /// The address a host name has, asked of the DNS server DHCP gave; an address passes through as it is.
    /// </summary>
    ResolveResult Resolve(string name, TimeSpan timeout);

    /// <summary>Sends one echo request to an IPv4 address and waits for its reply.</summary>
    PingResult Ping(string address, TimeSpan timeout);
}

/// <summary>An address, or why there's none.</summary>
public readonly record struct ResolveResult(string? Address, string? Problem);

/// <summary>How long a reply took, or why none came.</summary>
public readonly record struct PingResult(TimeSpan? Time, string? Problem);
