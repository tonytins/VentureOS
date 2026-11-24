namespace Venture.Airo.Projects;

/// <summary>
/// What a new app or project may be called: its name becomes its folder's, such as <c>Dice.app</c> or
/// <c>Dice.rproj</c>, so it has to be one on Linux, macOS, and Windows alike.
/// </summary>
public static class FolderNames
{
    /// <summary>The longest name given here, as a folder's name and a title.</summary>
    public const int Longest = 40;

    /// <summary>Why <paramref name="name"/> can't be a new <paramref name="what"/>'s, such as an app's, or <see langword="null"/> when it can.</summary>
    public static string? Problem(string name, string what)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentException.ThrowIfNullOrEmpty(what);
        var trimmed = name.Trim();
        var article = "aeiou".Contains(what[0], StringComparison.Ordinal) ? "An" : "A";
        if (trimmed.Length == 0)
            return $"{article} {what} needs a name.";
        if (trimmed.Length > Longest)
            return $"A name is at most {Longest} characters.";
        if (trimmed.StartsWith('.'))
            return "A name can't start with a dot.";
        if (trimmed.Any(character => char.IsControl(character) || character is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|'))
            return $"A name can't have / \\ : * ? \" < > | in it, since it names the {what}'s folder.";
        return null;
    }
}
