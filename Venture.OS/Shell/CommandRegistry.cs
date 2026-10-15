using System.Diagnostics.CodeAnalysis;

namespace Venture.OS.Shell;

/// <summary>
/// The commands the shell knows, by name.
/// </summary>
internal sealed class CommandRegistry
{
    private readonly Dictionary<string, IShellCommand> commands = [];

    /// <summary>Every registered command, sorted by name.</summary>
    public IReadOnlyList<IShellCommand> Commands
    {
        get
        {
            var sorted = new List<IShellCommand>(commands.Values);
            sorted.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
            return sorted;
        }
    }

    /// <summary>
    /// Adds <paramref name="command"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The name is invalid, the argument range is invalid, or another command already uses the name.
    /// </exception>
    public void Register(IShellCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!IsValidName(command.Name))
            throw new ArgumentException(
                $"\"{command.Name}\" isn't a valid command name: use lowercase letters, digits, and hyphens.",
                nameof(command));

        if (command.MinArguments < 0 || command.MaxArguments < command.MinArguments)
            throw new ArgumentException(
                $"\"{command.Name}\" has an invalid argument range ({command.MinArguments} to {command.MaxArguments}).",
                nameof(command));

        if (!commands.TryAdd(command.Name, command))
            throw new ArgumentException($"A command named \"{command.Name}\" is already registered.", nameof(command));
    }

    /// <summary>
    /// Finds a command by name, ignoring case.
    /// </summary>
    public bool TryGet(string name, [NotNullWhen(true)] out IShellCommand? command)
    {
        ArgumentNullException.ThrowIfNull(name);
        return commands.TryGetValue(name.ToLowerInvariant(), out command);
    }

    private static bool IsValidName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        foreach (var character in name)
        {
            if (character is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
                return false;
        }

        return true;
    }
}
