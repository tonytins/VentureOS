namespace Venture.OS.Network;

/// <summary>
/// Checks a host name or address someone typed, such as for <c>resolve</c> or <c>ping</c>, before the kernel sends
/// anything for it, so a typo is reported plainly rather than as a DNS server's silence.
/// </summary>
public static class HostNames
{
    private const int MaximumName = 253;
    private const int MaximumLabel = 63;

    /// <summary>
    /// Why the text can't be a host name or an address, or <see langword="null"/> when it can. Text of digits and
    /// dots, or with a colon, is taken as an address, which the kernel reads in full.
    /// </summary>
    public static string? Problem(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "a host name can't be empty";
        if (LooksLikeAddress(name))
            return null;

        var trimmed = name.EndsWith('.') ? name[..^1] : name;
        if (trimmed.Length > MaximumName)
            return Refused(name, $"a name is at most {MaximumName} characters");

        foreach (var label in trimmed.Split('.'))
        {
            if (label.Length == 0)
                return Refused(name, "it has an empty part between dots");
            if (label.Length > MaximumLabel)
                return Refused(name, $"each part between dots is at most {MaximumLabel} characters");
            foreach (var character in label)
            {
                if (!char.IsAsciiLetterOrDigit(character) && character != '-')
                    return Refused(name, "use letters, digits, and hyphens between dots");
            }

            if (label[0] == '-' || label[^1] == '-')
                return Refused(name, "a part can't start or end with a hyphen");
        }

        return null;
    }

    private static string Refused(string name, string why) => $"\"{name}\" isn't a host name or an address: {why}";

    private static bool LooksLikeAddress(string text)
    {
        if (text.Contains(':', StringComparison.Ordinal))
        {
            foreach (var character in text)
            {
                if (!char.IsAsciiHexDigit(character) && character is not (':' or '.'))
                    return false;
            }

            return true;
        }

        foreach (var character in text)
        {
            if (!char.IsAsciiDigit(character) && character != '.')
                return false;
        }

        return true;
    }
}
