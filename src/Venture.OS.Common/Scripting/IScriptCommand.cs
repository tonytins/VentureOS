using Venture.OS.Shell;

namespace Venture.OS.Scripting;

/// <summary>
/// A command written in Lua, which also says what it needs from the <c>venture</c> table.
/// </summary>
public interface IScriptCommand : IShellCommand
{
    /// <summary>The scopes its <c>-- Needs:</c> lines declare.</summary>
    ScriptScopes Needs { get; }

    /// <summary>
    /// Why its <c>-- Needs:</c> line keeps it from running, such as an unknown scope, or <see langword="null"/>.
    /// </summary>
    string? HeaderError { get; }
}
