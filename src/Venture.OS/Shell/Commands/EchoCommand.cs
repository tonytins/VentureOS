namespace Venture.OS.Shell.Commands;

/// <summary>
/// Prints its arguments on one line, separated by single spaces.
/// </summary>
/// <remarks>
/// The shell has already split and unquoted the arguments, so <c>echo "a  b"</c> keeps both spaces while
/// <c>echo a  b</c> prints <c>a b</c>. With no arguments it prints an empty line.
/// </remarks>
internal sealed class EchoCommand : IShellCommand
{
    public string Name => "echo";
    public string Summary => "Print the arguments, separated by spaces.";
    public string Usage => "echo [text...]";
    public int MinArguments => 0;
    public int MaxArguments => int.MaxValue;

    public void Execute(ShellContext context, IReadOnlyList<string> arguments) =>
        context.Output.WriteLine(string.Join(" ", arguments));
}
