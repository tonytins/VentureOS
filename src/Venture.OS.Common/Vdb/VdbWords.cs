using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Venture.OS.Vdb;

/// <summary>A word in a vdb/1 line: bare, or quoted, which a reader can tell apart, as <c>act</c>'s row needs to.</summary>
public readonly record struct VdbWord(string Text, bool IsQuoted);

/// <summary>
/// The words of the <c>ui</c> feature's lines, which carry names and text that may hold spaces or line breaks: a
/// word is bare, or quoted in <c>"</c> with <c>\\</c>, <c>\"</c>, <c>\n</c>, <c>\r</c>, and <c>\t</c> escaped, and
/// answers are made of <c>key=value</c> words, with a bare key a flag that's on.
/// </summary>
/// <remarks>
/// A reader skips keys it doesn't know, so later versions can say more in the same answer without a new feature.
/// </remarks>
public static class VdbWords
{
    /// <summary>
    /// The most characters of text one quoted value carries. At most three bytes of UTF-8 each, escapes included, so
    /// the four values in a window's line still fit in one.
    /// </summary>
    public const int MaximumTextLength = 600;

    /// <summary>
    /// <paramref name="text"/> quoted, with characters a line can't hold escaped and other control characters made
    /// spaces, cut to <see cref="MaximumTextLength"/>.
    /// </summary>
    /// <param name="cut">Set when it was cut, so the answer can say so.</param>
    public static string Quote(string text, ref bool cut)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > MaximumTextLength)
        {
            cut = true;
            text = text[..MaximumTextLength];

            // Half a pair isn't a character, so it doesn't go on its own.
            if (char.IsHighSurrogate(text[^1]))
                text = text[..^1];
        }

        StringBuilder quoted = new(text.Length + 2);
        quoted.Append('"');
        foreach (var character in text)
        {
            quoted.Append(character switch
            {
                '\\' => @"\\",
                '"' => "\\\"",
                '\n' => @"\n",
                '\r' => @"\r",
                '\t' => @"\t",
                _ when char.IsControl(character) => " ",
                _ => character.ToString(),
            });
        }

        return quoted.Append('"').ToString();
    }

    /// <summary>Quotes <paramref name="text"/>, ignoring whether it was cut.</summary>
    public static string Quote(string text)
    {
        var cut = false;
        return Quote(text, ref cut);
    }

    /// <summary>A <c>key=value</c> word with the value quoted, or nothing when there's no value.</summary>
    public static string Pair(string key, string? value, ref bool cut) => value is null ? "" : $" {key}={Quote(value, ref cut)}";

    /// <summary>A <c>key=value</c> word with a number for the value, or nothing when there's no value.</summary>
    public static string Pair(string key, double? value) =>
        value is { } number ? $" {key}={number.ToString("0.####", CultureInfo.InvariantCulture)}" : "";

    /// <summary>A flag, on when it's there, or nothing.</summary>
    public static string Flag(string key, bool on) => on ? $" {key}" : "";

    /// <summary>Splits a command's arguments into words, separated by spaces, unquoting quoted ones.</summary>
    public static bool TrySplit(string text, [NotNullWhen(true)] out List<VdbWord>? words, [NotNullWhen(false)] out string? problem)
    {
        ArgumentNullException.ThrowIfNull(text);
        words = [];
        problem = null;
        var index = 0;
        while (index < text.Length)
        {
            if (text[index] == ' ')
            {
                index++;
                continue;
            }

            if (text[index] != '"')
            {
                var end = text.IndexOf(' ', index);
                if (end < 0)
                    end = text.Length;
                var word = text[index..end];
                if (word.Contains('"', StringComparison.Ordinal))
                {
                    problem = $"a quote can only start a word, not sit inside {Shorten(word)}";
                    break;
                }

                words.Add(new VdbWord(word, IsQuoted: false));
                index = end;
                continue;
            }

            if (!TryUnquote(text, ref index, out var unquoted, out problem))
                break;
            if (index < text.Length && text[index] != ' ')
            {
                problem = "a quoted word ends at a space or the end of the line";
                break;
            }

            words.Add(new VdbWord(unquoted, IsQuoted: true));
        }

        if (problem is null)
            return true;
        words = null;
        return false;
    }

    // Reads the quoted word at index, leaving index just past its closing quote.
    private static bool TryUnquote(string text, ref int index, [NotNullWhen(true)] out string? word, [NotNullWhen(false)] out string? problem)
    {
        StringBuilder unquoted = new();
        word = null;
        for (index++; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '"')
            {
                index++;
                word = unquoted.ToString();
                problem = null;
                return true;
            }

            if (character != '\\')
            {
                unquoted.Append(character);
                continue;
            }

            index++;
            var escaped = index < text.Length ? text[index] : '\0';
            switch (escaped)
            {
                case '\\' or '"':
                    unquoted.Append(escaped);
                    break;
                case 'n':
                    unquoted.Append('\n');
                    break;
                case 'r':
                    unquoted.Append('\r');
                    break;
                case 't':
                    unquoted.Append('\t');
                    break;
                default:
                    problem = escaped == '\0'
                        ? "a quoted word ends in a lone \\"
                        : $"\\{escaped} isn't an escape; a quoted word escapes \\\\, \\\", \\n, \\r, and \\t";
                    return false;
            }
        }

        problem = "a quoted word has no closing quote";
        return false;
    }

    private static string Shorten(string text) => text.Length <= 40 ? text : text[..40] + "…";
}
