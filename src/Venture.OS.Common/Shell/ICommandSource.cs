using System.Diagnostics.CodeAnalysis;

namespace Venture.OS.Shell;

/// <summary>
/// Somewhere besides the built-in commands that the shell can find commands, such as Lua scripts on disk.
/// </summary>
/// <remarks>
/// <see cref="CommandRegistry"/> asks its sources only for names it doesn't know itself, so a source can't replace a
/// built-in command.
/// </remarks>
public interface ICommandSource
{
    /// <summary>Every command the source has right now.</summary>
    IReadOnlyList<IShellCommand> Commands { get; }

    /// <summary>
    /// Finds a command by name. The name has already passed <see cref="CommandRegistry"/>'s name rule and is
    /// lowercase.
    /// </summary>
    bool TryGet(string name, [NotNullWhen(true)] out IShellCommand? command);
}
