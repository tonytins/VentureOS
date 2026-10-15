namespace Venture.OS.Shell.Commands;

/// <summary>
/// Shows the primary network adapter and its IPv4 configuration.
/// </summary>
internal sealed class NetCommand : IShellCommand
{
    public string Name => "net";
    public string Summary => "Show the network adapter and its address.";
    public string Usage => "net";
    public int MinArguments => 0;
    public int MaxArguments => 0;

    public void Execute(ShellContext context, IReadOnlyList<string> arguments)
    {
        var output = context.Output;

        // NetworkManager.Primary is a handle that may name no device, so check for one first.
        if (!HostInfo.IsLinkedToNetwork)
        {
            output.WriteLine("No network device.");
            return;
        }

        output.WriteLine($"Device:   {NetworkManager.Name}");
        output.WriteLine($"MAC:      {NetworkManager.MacAddress}");
        output.WriteLine($"Link up:  {NetworkManager.LinkUp}");
        output.WriteLine($"Ready:    {NetworkManager.Ready}");

        var config = NetworkManager.Primary.IPConfig;
        if (config is null)
        {
            output.WriteLine("IPv4:     not configured");
            return;
        }

        output.WriteLine($"Address:  {config.Address}");
        output.WriteLine($"Subnet:   {config.SubnetMask}");
        output.WriteLine($"Gateway:  {config.DefaultGateway}");
    }
}
