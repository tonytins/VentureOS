using System.Diagnostics.CodeAnalysis;

namespace Venture.OS.Shell;

/// <summary>
/// The commands the shell knows, by name: the built-in commands registered here, then any from added sources.
/// </summary>
public sealed class CommandRegistry
{
    private readonly Dictionary<string, IShellCommand> commands = [];
    private readonly List<ICommandSource> sources = [];

    /// <summary>
    /// Every command, sorted by name: the registered ones, then each source's, skipping names already listed.
    /// </summary>
    public IReadOnlyList<IShellCommand> Commands
    {
        get
        {
            var all = new Dictionary<string, IShellCommand>(commands);
            foreach (var source in sources)
            {
                foreach (var command in source.Commands)
                    all.TryAdd(command.Name, command);
            }

            var sorted = new List<IShellCommand>(all.Values);
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
    /// Whether a built-in command, one registered here rather than found through a source, has this name.
    /// </summary>
    public bool IsBuiltIn(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return commands.ContainsKey(name.ToLowerInvariant());
    }

    /// <summary>
    /// Adds a place to look for commands that aren't registered, such as Lua scripts. Sources are asked in the
    /// order they were added, and only for names no registered command has.
    /// </summary>
    public void AddSource(ICommandSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        sources.Add(source);
    }

    /// <summary>
    /// Finds a command by name, ignoring case: a registered command first, then the sources in order.
    /// </summary>
    public bool TryGet(string name, [NotNullWhen(true)] out IShellCommand? command)
    {
        ArgumentNullException.ThrowIfNull(name);

        var lowercase = name.ToLowerInvariant();
        if (commands.TryGetValue(lowercase, out command))
            return true;

        if (!IsValidName(lowercase))
            return false;

        foreach (var source in sources)
        {
            if (source.TryGet(lowercase, out command))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Whether <paramref name="name"/> is a valid command name: lowercase letters, digits, and hyphens. Script
    /// commands use the same rule, so a name can never climb out of their directory.
    /// </summary>
    internal static bool IsValidName(string? name)
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
