namespace Venture.OS.Shell.Commands;

/// <summary>
/// Stops the shell and halts the CPU, leaving the machine on.
/// </summary>
internal sealed class HaltCommand : IShellCommand
{
    public string Name => "halt";
    public string Summary => "Halt the system, leaving the machine on.";
    public string Usage => "halt";
    public int MinArguments => 0;
    public int MaxArguments => 0;

    public void Execute(ShellContext context, IReadOnlyList<string> arguments) => context.Host.Halt();
}
