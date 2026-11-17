namespace Venture.OS.Shell.Commands;

/// <summary>
/// Clears the screen.
/// </summary>
internal sealed class ClearCommand : IShellCommand
{
    public string Name => "clear";
    public string Summary => "Clear the screen.";
    public string Usage => "clear";
    public int MinArguments => 0;
    public int MaxArguments => 0;

    public void Execute(ShellContext context, IReadOnlyList<string> arguments) => context.Output.Clear();
}
