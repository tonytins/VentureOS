namespace Venture.OS.Scripting;

/// <summary>
/// What a Lua script says about itself in the comment lines at its top.
/// </summary>
/// <remarks>
/// <para>Only the leading <c>--</c> comment block counts; reading stops at the first other line. Within it:</para>
/// <list type="bullet">
/// <item><description>The first plain comment is the summary.</description></item>
/// <item><description><c>-- Usage:</c> gives the usage line.</description></item>
/// <item><description><c>-- Needs:</c> lists the scopes the script uses, separated by commas or spaces, such as
/// <c>-- Needs: system, network</c>. Several <c>Needs</c> lines add up.</description></item>
/// </list>
/// <para>A script that needs a scope it can't have doesn't run. Never treating an unknown need as understood means
/// a script written for a newer VentureOS fails clearly on an older one instead of half working.</para>
/// </remarks>
public sealed record ScriptHeader(string? Summary, string? Usage, ScriptScopes Needs, string? Error)
{
    private const string UsagePrefix = "Usage:";
    private const string NeedsPrefix = "Needs:";

    /// <summary>Reads the header from a script's lines.</summary>
    public static ScriptHeader Parse(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        string? summary = null;
        string? usage = null;
        var needs = ScriptScopes.None;
        string? error = null;

        foreach (var line in lines)
        {
            if (!line.StartsWith("--", StringComparison.Ordinal))
                break;

            var text = line[2..].Trim();
            if (text.StartsWith(UsagePrefix, StringComparison.OrdinalIgnoreCase))
            {
                usage = text[UsagePrefix.Length..].Trim();
            }
            else if (text.StartsWith(NeedsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var name in text[NeedsPrefix.Length..].Split([',', ' ', '\t'],
                             StringSplitOptions.RemoveEmptyEntries))
                {
                    if (ScriptScopeNames.TryParse(name, out var scope, out var nameError))
                        needs |= scope;
                    else
                        error ??= nameError;
                }
            }
            else if (summary is null && text.Length > 0)
            {
                summary = text;
            }
        }

        return new ScriptHeader(summary, usage, needs, error);
    }

    /// <summary>Reads the header of the script at <paramref name="path"/>.</summary>
    /// <exception cref="IOException">The file couldn't be read.</exception>
    public static ScriptHeader Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Parse(File.ReadLines(path));
    }
}
