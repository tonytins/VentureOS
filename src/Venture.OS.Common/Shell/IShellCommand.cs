namespace Venture.OS.Shell;

/// <summary>
/// A command the shell can run.
/// </summary>
public interface IShellCommand
{
    /// <summary>The name the user types: lowercase letters, digits, and hyphens.</summary>
    string Name { get; }

    /// <summary>One line describing what the command does, shown by <c>help</c>.</summary>
    string Summary { get; }

    /// <summary>How to call the command, such as <c>help [command]</c>.</summary>
    string Usage { get; }

    /// <summary>The fewest arguments the command accepts.</summary>
    int MinArguments { get; }

    /// <summary>The most arguments the command accepts.</summary>
    int MaxArguments { get; }

    /// <summary>
    /// Runs the command. The shell has already checked the argument count against
    /// <see cref="MinArguments"/> and <see cref="MaxArguments"/>.
    /// </summary>
    void Execute(ShellContext context, IReadOnlyList<string> arguments);
}

/// <summary>
/// A command whose argument is code or text rather than words, such as <c>lua</c>: it gets the rest of the line as
/// written, quotes and backslashes included, as its one argument, or none when nothing follows its name.
/// </summary>
/// <remarks>
/// The shell's quoting is for words, so <c>lua print("x")</c> split into words would reach Lua as <c>print(x)</c>.
/// Text that starts with a quote is read the old way, as one quoted word, so <c>lua "print(1)"</c> still works: no
/// Lua statement starts with a quote, so nothing written for Lua is lost by it.
/// </remarks>
public interface ITextCommand : IShellCommand;
