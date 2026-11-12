namespace Venture.OS.Scripting;

/// <summary>
/// Resolving names and pinging for scripts, with Cosmos's <see cref="DnsClient"/> and <see cref="IcmpClient"/>.
/// </summary>
/// <remarks>
/// <para>Each call waits no longer than its timeout: both clients poll in 10 ms steps up to it, so a lost packet or
/// a silent server can't hang the shell. A failure, including anything the network stack throws, comes back as a
/// result with the reason, since an exception here would stop the machine.</para>
/// <para>DHCP registers the DNS server it gives (<see cref="DnsConfig.Add"/>), and the first one registered is
/// asked. Cosmos's echo measures in 10 ms steps, so a fast reply reads as 0 or 10 ms.</para>
/// </remarks>
internal sealed class KernelNetworkTools : INetworkTools
{
    private ushort sequence;

    public ResolveResult Resolve(string name, TimeSpan timeout)
    {
        try
        {
            if (Address.Parse(name) is { } literal)
                return new ResolveResult(literal.ToString(), null);
            if (DnsConfig.Nameservers.Count == 0)
                return new ResolveResult(null, "there's no DNS server: DHCP didn't give one");

            using DnsClient client = new();
            client.Connect(DnsConfig.Nameservers[0]);
            client.SendQuery(name.TrimEnd('.'));
            return client.Receive(Milliseconds(timeout)) is { } address
                ? new ResolveResult(address.ToString(), null)
                : new ResolveResult(null, $"no answer for {name} within {timeout.TotalSeconds:0} s");
        }
        catch (Exception exception)
        {
            return new ResolveResult(null, $"couldn't ask about {name}: {exception.Message}");
        }
    }

    public PingResult Ping(string address, TimeSpan timeout)
    {
        try
        {
            if (Address.Parse(address) is not { } target)
                return new PingResult(null, $"\"{address}\" isn't an address");
            if (!target.IsIpv4)
                return new PingResult(null, "ping reaches IPv4 addresses only");

            using IcmpClient client = new();
            client.Connect(target);
            client.SendEcho(id: 1, sequence: ++sequence);
            Cosmos.Kernel.System.Network.EndPoint source = new(target, 0);
            var elapsed = client.Receive(ref source, Milliseconds(timeout));
            return elapsed < 0
                ? new PingResult(null, $"no reply within {timeout.TotalSeconds:0} s")
                : new PingResult(TimeSpan.FromMilliseconds(elapsed), null);
        }
        catch (Exception exception)
        {
            return new PingResult(null, $"couldn't ping {address}: {exception.Message}");
        }
    }

    // A timeout as the clients take it, kept within what scripts may ask for.
    private static int Milliseconds(TimeSpan timeout) => (int)Math.Clamp(timeout.TotalMilliseconds, 10, 10_000);
}
