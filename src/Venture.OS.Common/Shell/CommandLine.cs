using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Venture.OS.Shell;

/// <summary>
/// Splits a line of shell input into words.
/// </summary>
/// <remarks>
/// <para>Words are separated by spaces or tabs. Quotes group words:</para>
/// <list type="bullet">
/// <item><description><c>'single quotes'</c> keep everything inside them literally.</description></item>
/// <item><description><c>"double quotes"</c> allow <c>\"</c> and <c>\\</c> escapes inside them.</description></item>
/// <item><description>Outside quotes, a backslash makes the next character literal, such as <c>\ </c> for a space.</description></item>
/// </list>
/// <para>Quoted and unquoted parts next to each other form one word, so <c>a"b c"</c> is <c>ab c</c>, and
/// <c>""</c> is an empty word. This type doesn't depend on the kernel, so it can be tested on its own.</para>
/// </remarks>
public static class CommandLine
{
    /// <summary>
    /// Splits <paramref name="input"/> into words.
    /// </summary>
    /// <param name="input">The line the user typed.</param>
    /// <param name="words">The words, in order. Empty when the line is blank.</param>
    /// <param name="error">Why the line couldn't be split, when the method returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if the line was split; <see langword="false"/> if a quote or escape was left open.</returns>
    public static bool TryParse(string input, out List<string> words, [NotNullWhen(false)] out string? error)
    {
        ArgumentNullException.ThrowIfNull(input);

        words = [];
        error = null;

        StringBuilder current = new();
        var inWord = false;
        var index = 0;

        while (index < input.Length)
        {
            var character = input[index];

            if (character is ' ' or '\t')
            {
                if (inWord)
                {
                    words.Add(current.ToString());
                    current.Clear();
                    inWord = false;
                }

                index++;
                continue;
            }

            inWord = true;

            switch (character)
            {
                case '\'':
                    var closingSingle = input.IndexOf('\'', index + 1);
                    if (closingSingle < 0)
                    {
                        error = "Missing closing single quote (').";
                        return false;
                    }

                    current.Append(input, index + 1, closingSingle - index - 1);
                    index = closingSingle + 1;
                    break;

                case '"':
                    if (!TryReadDoubleQuoted(input, ref index, current))
                    {
                        error = "Missing closing double quote (\").";
                        return false;
                    }

                    break;

                case '\\':
                    if (index + 1 >= input.Length)
                    {
                        error = "Nothing follows the backslash (\\) at the end of the line.";
                        return false;
                    }

                    current.Append(input[index + 1]);
                    index += 2;
                    break;

                default:
                    current.Append(character);
                    index++;
                    break;
            }
        }

        if (inWord)
            words.Add(current.ToString());

        return true;
    }

    /// <summary>
    /// Splits off the first word, read as <see cref="TryParse"/> reads words, and the rest of the line exactly as it
    /// was typed, without the space after the word or at its end: for a command that takes text rather than words
    /// (<see cref="ITextCommand"/>).
    /// </summary>
    /// <param name="input">The line the user typed.</param>
    /// <param name="first">The first word, or empty when the line is blank.</param>
    /// <param name="rest">What follows it, as written.</param>
    /// <param name="error">Why the first word couldn't be read, when the method returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if the first word was read; <see langword="false"/> if its quote or escape was left open.</returns>
    public static bool TrySplitFirst(string input, out string first, out string rest, [NotNullWhen(false)] out string? error)
    {
        ArgumentNullException.ThrowIfNull(input);
        first = "";
        rest = "";

        var start = 0;
        while (start < input.Length && input[start] is ' ' or '\t')
            start++;

        // The word ends at the first space or tab outside quotes, where TryParse would end it too.
        var end = start;
        while (end < input.Length && input[end] is not (' ' or '\t'))
        {
            switch (input[end])
            {
                case '\'':
                    var closing = input.IndexOf('\'', end + 1);
                    end = closing < 0 ? input.Length : closing + 1;
                    break;
                case '"':
                    var position = end + 1;
                    while (position < input.Length && input[position] != '"')
                        position += input[position] == '\\' && position + 1 < input.Length ? 2 : 1;
                    end = Math.Min(input.Length, position + 1);
                    break;
                case '\\':
                    end = Math.Min(input.Length, end + 2);
                    break;
                default:
                    end++;
                    break;
            }
        }

        if (!TryParse(input[start..end], out var words, out error))
            return false;

        first = words.Count > 0 ? words[0] : "";
        rest = input[end..].Trim(' ', '\t');
        return true;
    }

    /// <summary>
    /// Reads a double-quoted part starting at the opening quote, and moves <paramref name="index"/> past the closing one.
    /// </summary>
    /// <returns><see langword="false"/> if the line ends before the closing quote.</returns>
    private static bool TryReadDoubleQuoted(string input, ref int index, StringBuilder current)
    {
        var position = index + 1;

        while (position < input.Length)
        {
            var character = input[position];

            if (character == '"')
            {
                index = position + 1;
                return true;
            }

            // Inside double quotes, only \" and \\ are escapes; any other backslash is kept as written.
            if (character == '\\' && position + 1 < input.Length && input[position + 1] is '"' or '\\')
            {
                current.Append(input[position + 1]);
                position += 2;
                continue;
            }

            current.Append(character);
            position++;
        }

        return false;
    }
}
