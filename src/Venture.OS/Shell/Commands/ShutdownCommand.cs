namespace Venture.OS.Shell.Commands;

/// <summary>
/// Powers the machine off.
/// </summary>
internal sealed class ShutdownCommand : IShellCommand
{
    public string Name => "shutdown";
    public string Summary => "Power the machine off.";
    public string Usage => "shutdown";
    public int MinArguments => 0;
    public int MaxArguments => 0;

    public void Execute(ShellContext context, IReadOnlyList<string> arguments) => context.Host.Shutdown();
}
