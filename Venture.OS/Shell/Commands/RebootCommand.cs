namespace Venture.OS.Shell.Commands;

/// <summary>
/// Restarts the machine.
/// </summary>
internal sealed class RebootCommand : IShellCommand
{
    public string Name => "reboot";
    public string Summary => "Restart the machine.";
    public string Usage => "reboot";
    public int MinArguments => 0;
    public int MaxArguments => 0;

    public void Execute(ShellContext context, IReadOnlyList<string> arguments) => context.Host.Reboot();
}
