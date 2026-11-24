using CSTNet;
using Venture.OS.Interface.Strings;

namespace Venture.OS.Desktop;

/// <summary>
/// The words the desktop shows a person, such as its menu items and window names, read from the system's shared
/// table <see cref="Table"/>, <c>_101_desktop.cst</c>, so the wording lives in one place beside the words apps
/// share, and changes without a rebuild of the code.
/// </summary>
/// <remarks>
/// Keys are names, such as <c>closeWindow</c>, since only code asks for them. Text that isn't there shows as
/// <c>101:key</c>, as a document's missing string does, and is reported once, so a missing word is visible
/// rather than silent. A table that's missing altogether is reported once, not once a word.
/// </remarks>
public sealed class DesktopText
{
    /// <summary>The number of the desktop's table among the shared ones.</summary>
    public const int Table = 101;

    private readonly StringTables? tables;
    private readonly Action<string>? report;
    private readonly HashSet<string> reported = [];
    private int problemsSeen;
    private bool? hasTable;

    /// <param name="stringsFolder">
    /// The system's shared strings folder as the host sees it, such as <c>/mnt/system/strings</c>, or
    /// <see langword="null"/> for a desktop without one, whose every word shows as its key.
    /// </param>
    /// <param name="report">Where a missing table, a missing word, or a table's problem is reported, once each.</param>
    public DesktopText(string? stringsFolder, Action<string>? report = null)
    {
        tables = stringsFolder is null ? null : StringTables.Shared(stringsFolder);
        this.report = report;
    }

    /// <summary>
    /// The text under <paramref name="key"/>, its <c>%s</c> placeholders filled with <paramref name="arguments"/> in
    /// order, or <c>101:key</c> when there's none.
    /// </summary>
    public string Get(string key, params string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(arguments);

        var text = tables?.Find(Table, key);
        ReportProblems();
        if (text is null)
        {
            ReportMissing(key);
            return $"{Table}:{key}";
        }

        return arguments.Length == 0 ? text : CST.Format(text, arguments);
    }

    // A table's own problems, such as a value never closed, as they turn up.
    private void ReportProblems()
    {
        if (tables is null)
            return;

        for (; problemsSeen < tables.Problems.Count; problemsSeen++)
            Report(tables.Problems[problemsSeen]);
    }

    private void ReportMissing(string key)
    {
        hasTable ??= tables?.HasTable(Table) ?? false;
        if (hasTable == false)
            Report($"The desktop's text, _{Table}_desktop.cst, isn't in the system's strings.");
        else
            Report($"_{Table}_desktop.cst has no \"{key}\".");
    }

    private void Report(string problem)
    {
        if (reported.Add(problem))
            report?.Invoke(problem);
    }
}
