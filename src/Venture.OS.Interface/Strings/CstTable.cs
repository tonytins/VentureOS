using System.Text;

namespace Venture.OS.Interface.Strings;

/// <summary>
/// One table of Caret-Separated Text (CST), the key-value format The Sims Online kept its interface text in:
/// <c>key ^value^</c>, read by a transitional reader until CST.NET is reworked to the new specification.
/// </summary>
/// <remarks>
/// <para>It reads the part of the specification (cstdotnet's <c>cst-spec.md</c>) whose meaning is settled: a key
/// on the same line as its value's opening caret, values that run across lines and keep their spacing, empty values,
/// <c>//</c>, <c>#</c>, and <c>/* */</c> comments, any line ending, a repeated key (the first wins), and the
/// specification's recovery from a value never closed.</para>
/// <para>What it doesn't read, it reports and skips rather than guessing: a value with no key on its own line (a
/// key or name above it, or none), and a second value on one line. So every value it does read is the one a full
/// reader finds under the same key, and a table it reads cleanly means the same to both. Positions only reach values
/// without keys, which it skips, so it offers none.</para>
/// <para>Reading never fails: a bad entry is reported in <see cref="Problems"/> and the rest is kept.</para>
/// </remarks>
public sealed class CstTable
{
    private CstTable(IReadOnlyDictionary<string, string> values, IReadOnlyList<string> problems)
    {
        Values = values;
        Problems = problems;
    }

    /// <summary>The values by key, exactly as written: placeholders and all.</summary>
    public IReadOnlyDictionary<string, string> Values { get; }

    /// <summary>Entries that were skipped, and why, each starting with its line.</summary>
    public IReadOnlyList<string> Problems { get; }

    /// <summary>Reads a table's text.</summary>
    public static CstTable Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        List<string> problems = [];

        // CRLF first, or each one becomes two line breaks.
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Replace('\u2028', '\n')
            .Split('\n');

        // An open value: its key (null when it has none, so it's skipped when it closes), where it began, and its text.
        string? openKey = null;
        var openLine = 0;
        StringBuilder? open = null;
        var inComment = false;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var number = index + 1;

            // Comments come out before anything else, even inside a value.
            var start = line.TrimStart();
            if (inComment)
            {
                inComment = !line.Contains("*/", StringComparison.Ordinal);
                continue;
            }

            if (start.StartsWith("//", StringComparison.Ordinal) || start.StartsWith('#'))
                continue;
            if (start.StartsWith("/*", StringComparison.Ordinal))
            {
                inComment = !start[2..].Contains("*/", StringComparison.Ordinal);
                continue;
            }

            if (open is not null)
            {
                var carets = line.Count(character => character == '^');
                if (carets == 2 && line.TrimEnd().EndsWith('^'))
                {
                    // A whole entry on one line: the open value was never closed.
                    problems.Add($"Line {openLine}: a value is never closed, so it's skipped.");
                    open = null;
                }
                else if (carets > 0)
                {
                    var close = line.IndexOf('^');
                    open.Append(line, 0, close);
                    Add(openKey, open.ToString(), openLine);
                    open = null;
                    OnlyOne(line[(close + 1)..], number);
                    continue;
                }
                else
                {
                    open.Append(line).Append('\n');
                    continue;
                }
            }

            var caret = line.IndexOf('^');
            if (caret < 0)
                continue;

            var key = line[..caret].Trim();
            if (key.Length == 0)
            {
                problems.Add($"Line {number}: a value needs its key before it on the same line, so it's skipped; "
                    + "this reader doesn't read keys or names above values, or values without either.");
            }

            var rest = line[(caret + 1)..];
            var end = rest.IndexOf('^');
            if (end < 0)
            {
                (openKey, openLine, open) = (key.Length == 0 ? null : key, number, new StringBuilder(rest).Append('\n'));
                continue;
            }

            Add(key.Length == 0 ? null : key, rest[..end], number);
            OnlyOne(rest[(end + 1)..], number);
        }

        if (open is not null)
            problems.Add($"Line {openLine}: a value is never closed, so it's skipped.");

        return new CstTable(values, problems);

        void Add(string? key, string value, int line)
        {
            if (key is null)
                return;
            if (!values.TryAdd(key, value))
                problems.Add($"Line {line}: the key \"{key}\" is used again, so this value is skipped and the first kept.");
        }

        // Text after a closing caret is a note, but another value there is beyond this reader.
        void OnlyOne(string after, int line)
        {
            if (after.Contains('^', StringComparison.Ordinal))
                problems.Add($"Line {line}: there's another value after the first on this line, so it's skipped.");
        }
    }
}
