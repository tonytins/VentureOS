namespace Venture.OS.Shell.Commands;

/// <summary>
/// Lists the commands, or shows how to use one.
/// </summary>
internal sealed class HelpCommand : IShellCommand
{
    public string Name => "help";
    public string Summary => "List the commands, or show how to use one.";
    public string Usage => "help [command]";
    public int MinArguments => 0;
    public int MaxArguments => 1;

    public void Execute(ShellContext context, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 1)
        {
            if (!context.Commands.TryGet(arguments[0], out var command))
            {
                context.Output.WriteLine($"\"{arguments[0]}\" is not a command.");
                return;
            }

            context.Output.WriteLine(command.Summary);
            context.Output.WriteLine($"Usage: {command.Usage}");
            return;
        }

        var commands = context.Commands.Commands;
        var width = 0;
        foreach (var command in commands)
            width = Math.Max(width, command.Name.Length);

        context.Output.WriteLine("Available commands:");
        foreach (var command in commands)
            context.Output.WriteLine($"    {command.Name.PadRight(width)}  {command.Summary}");

        context.Output.WriteLine("Type \"help <command>\" for how to use one.");
    }
}
