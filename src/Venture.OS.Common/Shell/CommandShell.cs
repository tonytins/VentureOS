namespace Venture.OS.Shell;

/// <summary>
/// Runs one line of user input: splits it, finds the command, checks its arguments, and runs it.
/// </summary>
/// <remarks>
/// Mistakes the user can fix (bad quoting, an unknown command, the wrong number of arguments) are reported here.
/// Exceptions from a command propagate to the kernel's shell loop, which reports them.
/// </remarks>
public sealed class CommandShell(ShellContext context)
{
    private readonly ShellContext context = context ?? throw new ArgumentNullException(nameof(context));

    public void Execute(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!CommandLine.TryParse(input, out var words, out var error))
        {
            context.Output.WriteLine(error);
            return;
        }

        if (words.Count == 0)
            return;

        var name = words[0];
        if (!context.Commands.TryGet(name, out var command))
        {
            context.Output.WriteLine($"\"{name}\" is not a command. Type \"help\" for a list of commands.");
            return;
        }

        var arguments = words.GetRange(1, words.Count - 1);
        if (arguments.Count < command.MinArguments || arguments.Count > command.MaxArguments)
        {
            context.Output.WriteLine($"Usage: {command.Usage}");
            return;
        }

        command.Execute(context, arguments);
    }
}
