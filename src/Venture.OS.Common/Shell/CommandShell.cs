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

        // The name comes first, so a command that takes text, such as lua, gets the rest as written, never split
        // into words; quotes in Lua code mean something to Lua.
        if (!CommandLine.TrySplitFirst(input, out var name, out var rest, out var error))
        {
            context.Output.WriteLine(error);
            return;
        }

        if (name.Length == 0 && rest.Length == 0)
            return;

        if (!context.Commands.TryGet(name, out var command))
        {
            context.Output.WriteLine($"\"{name}\" isn't a command. \"help\" lists them.");
            return;
        }

        List<string> arguments;
        if (command is ITextCommand)
        {
            arguments = rest.Length == 0 ? [] : [TextArgument(rest)];
        }
        else
        {
            if (!CommandLine.TryParse(rest, out var words, out error))
            {
                context.Output.WriteLine(error);
                return;
            }

            arguments = words;
        }

        if (arguments.Count < command.MinArguments || arguments.Count > command.MaxArguments)
        {
            context.Output.WriteLine($"Usage: {command.Usage}");
            return;
        }

        command.Execute(context, arguments);
    }

    /// <summary>
    /// A text command's argument: the text as written, or, when it starts with a quote and is one quoted word, as
    /// that word reads, so <c>lua "print(1)"</c> keeps working.
    /// </summary>
    private static string TextArgument(string text) =>
        text[0] is '"' or '\'' && CommandLine.TryParse(text, out var words, out _) && words.Count == 1 ? words[0] : text;
}
