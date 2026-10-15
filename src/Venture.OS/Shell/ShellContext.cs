namespace Venture.OS.Shell;

/// <summary>
/// What a running command can reach: its output, the kernel, and the other commands.
/// </summary>
internal sealed class ShellContext(IShellOutput output, IShellHost host, CommandRegistry commands)
{
    public IShellOutput Output { get; } = output ?? throw new ArgumentNullException(nameof(output));

    public IShellHost Host { get; } = host ?? throw new ArgumentNullException(nameof(host));

    public CommandRegistry Commands { get; } = commands ?? throw new ArgumentNullException(nameof(commands));
}
