using Venture.OS.Scripting;

namespace Venture.OS.Tests.Scripting;

/// <summary>
/// A network that knows example.com and answers pings from the gateway, 10.0.2.2, keeping what it was asked.
/// </summary>
internal sealed class FakeNetworkTools : INetworkTools
{
    public List<(string Call, string Target, TimeSpan Timeout)> Calls { get; } = [];

    public ResolveResult Resolve(string name, TimeSpan timeout)
    {
        Calls.Add(("resolve", name, timeout));
        return name switch
        {
            "example.com" => new ResolveResult("93.184.216.34", null),
            "10.0.2.2" => new ResolveResult("10.0.2.2", null),
            _ => new ResolveResult(null, $"no answer for {name}"),
        };
    }

    public PingResult Ping(string address, TimeSpan timeout)
    {
        Calls.Add(("ping", address, timeout));
        return address == "10.0.2.2"
            ? new PingResult(TimeSpan.FromMilliseconds(12), null)
            : new PingResult(null, $"no reply within {timeout.TotalSeconds:0} s");
    }
}
