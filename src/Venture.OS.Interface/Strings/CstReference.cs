using System.Globalization;

namespace Venture.OS.Interface.Strings;

/// <summary>
/// A value that holds a reference to another table's text in place of text of its own, as 39 entries in The Sims
/// Online's New &amp; Improved table 208 do:
/// <c>U+0002&lt;cst&gt;&lt;cst_id&gt;276&lt;/cst_id&gt;&lt;string_id&gt;2&lt;/string_id&gt;&lt;string arg="1"&gt;%s&lt;/string&gt;&lt;/cst&gt;</c>.
/// </summary>
/// <remarks>
/// The CST specification leaves references to the wrapper, not the reader, so CSTNet's <c>CSTTable</c> returns them as
/// written and <see cref="StringTables"/> follows them. This reads one until CSTNet's <c>CSTLayers</c> does.
/// </remarks>
/// <param name="Table">The table the text is in.</param>
/// <param name="Key">Its key in that table.</param>
/// <param name="Arguments">What fills the text's placeholders, in order; the spec ignores their <c>arg</c> numbers.</param>
internal sealed record CstReference(int Table, string Key, IReadOnlyList<string> Arguments)
{
    private const string Start = "\u0002<cst>";
    private const string End = "</cst>";

    /// <summary>Whether a value is written as a reference, well-formed or not.</summary>
    public static bool Is(string value) => value.StartsWith(Start, StringComparison.Ordinal);

    /// <summary>Reads a reference, or gives <see langword="null"/> when it's malformed.</summary>
    public static CstReference? Read(string value)
    {
        if (!Is(value) || !value.EndsWith(End, StringComparison.Ordinal))
            return null;

        var body = value[Start.Length..^End.Length];
        if (Enclosed(body, "cst_id") is not { } tableText
            || !int.TryParse(tableText, NumberStyles.None, CultureInfo.InvariantCulture, out var table)
            || Enclosed(body, "string_id") is not { Length: > 0 } key)
        {
            return null;
        }

        // Arguments are <string arg="n">text</string>, in order.
        List<string> arguments = [];
        var position = 0;
        while (true)
        {
            var open = body.IndexOf("<string ", position, StringComparison.Ordinal);
            if (open < 0)
                break;
            var openEnd = body.IndexOf('>', open);
            var close = openEnd < 0 ? -1 : body.IndexOf("</string>", openEnd, StringComparison.Ordinal);
            if (close < 0)
                return null;
            arguments.Add(body[(openEnd + 1)..close]);
            position = close + "</string>".Length;
        }

        return new CstReference(table, key, arguments);
    }

    // The text between the first <tag> and the </tag> after it, or null when either is missing.
    private static string? Enclosed(string text, string tag)
    {
        var open = $"<{tag}>";
        var start = text.IndexOf(open, StringComparison.Ordinal);
        if (start < 0)
            return null;
        start += open.Length;
        var end = text.IndexOf($"</{tag}>", start, StringComparison.Ordinal);
        return end < 0 ? null : text[start..end];
    }
}
