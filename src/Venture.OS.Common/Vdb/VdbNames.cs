namespace Venture.OS.Vdb;

/// <summary>The checks on names that cross a vdb connection: apps' folder names, paths inside a bundle, and sizes.</summary>
public static class VdbNames
{
    /// <summary>Why a text can't name an app's folder, such as <c>Dice.app</c>, or <see langword="null"/> when it can.</summary>
    public static string? ProblemWithApp(string app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!app.EndsWith(".app", StringComparison.OrdinalIgnoreCase) || app.Length == ".app".Length)
            return $"\"{app}\" isn't an app's folder, such as Dice.app";
        if (app.Length > VdbProtocol.MaximumPathLength)
            return $"an app's name is at most {VdbProtocol.MaximumPathLength} characters";
        if (app.StartsWith('.'))
            return "an app's name can't start with a dot";
        if (app.Any(character => char.IsControl(character) || character is '/' or '\\' or ':'))
            return $"\"{app}\" has a character an app's name can't have";
        return null;
    }

    /// <summary>
    /// Why a text can't be a path inside a bundle, such as <c>Contents/Lua/main.lua</c>, or <see langword="null"/>
    /// when it can: relative, with <c>/</c> between parts, and no part empty, <c>.</c>, or <c>..</c>, so it can only
    /// name something inside the bundle.
    /// </summary>
    public static string? ProblemWithPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Length == 0)
            return "a file needs a path";
        if (path.Length > VdbProtocol.MaximumPathLength)
            return $"a path is at most {VdbProtocol.MaximumPathLength} characters";
        if (path.StartsWith('/'))
            return $"\"{path}\" starts at the root; a path is inside the bundle";
        if (path.Any(character => char.IsControl(character) || character is '\\' or ':'))
            return $"\"{path}\" has a character a path can't have; parts are separated by /";
        foreach (var part in path.Split('/'))
        {
            if (part is "" or "." or "..")
                return $"\"{path}\" has an empty, . or .. part; a path names a file inside the bundle";
        }

        return null;
    }

    /// <summary>Reads a size: a whole number of bytes, from 0 to <paramref name="largest"/>, in digits alone.</summary>
    public static bool TryReadSize(string text, long largest, out long size)
    {
        ArgumentNullException.ThrowIfNull(text);
        size = 0;
        if (text.Length is 0 or > 12 || !text.All(char.IsAsciiDigit))
            return false;

        size = long.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        return size <= largest;
    }
}
