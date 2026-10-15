namespace Venture.OS.Shell.Commands;

internal sealed class EchoCommand : IShellCommand
{
    public string Name => "echo";
    public string Summary => "Echoes the input arguments.";
    public string Usage => "echo [arguments]";
    public int MinArguments => 0;
    public int MaxArguments => int.MaxValue;

    public void Execute(ShellContext context, IReadOnlyList<string> arguments)
    {
        var output = string.Join(" ", arguments);
        context.Output.WriteLine(output);
    }
}