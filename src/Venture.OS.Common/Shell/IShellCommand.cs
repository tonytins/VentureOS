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
