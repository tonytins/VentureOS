namespace Venture.OS.Scripting;

/// <summary>
/// What a Lua script may reach through the <c>venture</c> table, declared with <c>-- Needs:</c> in its header.
/// </summary>
/// <remarks>
/// Every script gets <c>venture.version()</c> without declaring anything. Scopes are broad on purpose: each covers
/// a whole area, so new functions in that area don't need new names.
/// </remarks>
[Flags]
public enum ScriptScopes
{
    /// <summary>Only <c>venture.version()</c>.</summary>
    None = 0,

    /// <summary><c>system</c>: <c>venture.uptime()</c> and <c>venture.memory()</c>.</summary>
    System = 1 << 0,

    /// <summary><c>network</c>: <c>venture.net()</c>.</summary>
    Network = 1 << 1,

    /// <summary><c>shell</c>: <c>venture.command</c>, which adds commands to the shell.</summary>
    Shell = 1 << 2,

    /// <summary><c>files</c>: <c>venture.files</c>, which lists, reads, and changes files and folders.</summary>
    Files = 1 << 3,

    /// <summary>Everything, for code the user types, such as the <c>lua</c> command.</summary>
    All = System | Network | Shell | Files,
}

/// <summary>
/// The names scripts use for <see cref="ScriptScopes"/> in a <c>-- Needs:</c> line.
/// </summary>
public static class ScriptScopeNames
{
    /// <summary>Names that are planned but not offered yet. A script that needs one can't run.</summary>
    /// <remarks><c>ui</c> is for the desktop's <c>venture.ui</c> (Borealis).</remarks>
    public static readonly IReadOnlyList<string> Reserved = ["ui"];

    /// <summary>The name of one scope, such as <c>network</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="scope"/> isn't a single scope.</exception>
    public static string Name(ScriptScopes scope) => scope switch
    {
        ScriptScopes.System => "system",
        ScriptScopes.Network => "network",
        ScriptScopes.Shell => "shell",
        ScriptScopes.Files => "files",
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Not a single scope."),
    };

    /// <summary>
    /// The names of the scopes in <paramref name="scopes"/>, such as <c>system, network</c>, or <c>none</c>.
    /// </summary>
    public static string Describe(ScriptScopes scopes)
    {
        List<string> names = [];
        foreach (var scope in (ReadOnlySpan<ScriptScopes>)[ScriptScopes.System, ScriptScopes.Network, ScriptScopes.Shell, ScriptScopes.Files])
        {
            if ((scopes & scope) != 0)
                names.Add(Name(scope));
        }

        return names.Count == 0 ? "none" : string.Join(", ", names);
    }

    /// <summary>
    /// Finds the scope a name stands for, ignoring case.
    /// </summary>
    /// <param name="name">A name from a <c>-- Needs:</c> line.</param>
    /// <param name="scope">The scope, when the method returns <see langword="true"/>.</param>
    /// <param name="error">Why the name isn't usable, when it returns <see langword="false"/>.</param>
    public static bool TryParse(string name, out ScriptScopes scope, out string? error)
    {
        ArgumentNullException.ThrowIfNull(name);

        scope = name.ToLowerInvariant() switch
        {
            "system" => ScriptScopes.System,
            "network" => ScriptScopes.Network,
            "shell" => ScriptScopes.Shell,
            "files" => ScriptScopes.Files,
            _ => ScriptScopes.None,
        };

        if (scope != ScriptScopes.None)
        {
            error = null;
            return true;
        }

        error = Reserved.Contains(name.ToLowerInvariant())
            ? $"\"{name}\" isn't available yet."
            : $"\"{name}\" isn't a known scope; use system, network, shell, or files.";
        return false;
    }
}
