using System.Globalization;
using Venture.OS.Interface;

namespace Venture.OS.Strings;

/// <summary>
/// The CST tables in one folder, such as beside an app's <c>main.uix</c>, which supply the text a document's
/// <c>&lt;String&gt;</c> elements name, as Hudini's hosts read The Sims Online's.
/// </summary>
/// <remarks>
/// <para>A table is a file named <c>_&lt;id&gt;_&lt;name&gt;.cst</c>; only the number counts. A
/// <c>&lt;String table="1" index="3"/&gt;</c> (or <c>table="UIText:1"</c>, the number at the end) finds the value
/// with the key <c>3</c> in table 1. Each table is read once, the first time it's asked for.</para>
/// <para>Text that isn't there is missing, so the view can show and list it. A table's problems, and a table name
/// without a number, are kept in <see cref="Problems"/> for the app to be told.</para>
/// </remarks>
public sealed class StringTables(string folder)
{
    private readonly string folder = folder ?? throw new ArgumentNullException(nameof(folder));
    private readonly Dictionary<int, CstTable?> tables = [];
    private readonly List<string> problems = [];
    private Dictionary<int, string>? files;

    /// <summary>What went wrong reading the tables, each naming its file and line where it has one.</summary>
    public IReadOnlyList<string> Problems => problems;

    /// <summary>The text a <c>&lt;String&gt;</c> names, or <see langword="null"/> when there's none.</summary>
    public string? Find(UIStringResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var name = resource.Table;
        var number = name[(name.LastIndexOf(':') + 1)..];
        if (!int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            problems.Add($"The table \"{name}\" needs a number, such as \"1\" for _1_strings.cst.");
            return null;
        }

        return Table(id)?.Values.GetValueOrDefault(resource.Index.ToString(CultureInfo.InvariantCulture));
    }

    private CstTable? Table(int id)
    {
        if (tables.TryGetValue(id, out var table))
            return table;

        table = null;
        if (Files().TryGetValue(id, out var path))
        {
            try
            {
                table = CstTable.Read(File.ReadAllText(path));
                foreach (var problem in table.Problems)
                    problems.Add($"{Path.GetFileName(path)}, {char.ToLowerInvariant(problem[0])}{problem[1..]}");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                problems.Add($"Couldn't read {Path.GetFileName(path)}: {exception.Message}");
            }
        }

        tables[id] = table;
        return table;
    }

    // The tables in the folder by number, worked out once. A second file with a number already taken is reported.
    private Dictionary<int, string> Files()
    {
        if (files is not null)
            return files;

        files = [];
        if (!Directory.Exists(folder))
            return files;

        // GetFiles and Array.Sort, as elsewhere in the kernel, rather than LINQ, which Cosmos hasn't been shown to run.
        var paths = Directory.GetFiles(folder);
        Array.Sort(paths, StringComparer.Ordinal);
        foreach (var path in paths)
        {
            if (Number(Path.GetFileName(path)) is not { } id)
                continue;
            if (!files.TryAdd(id, path))
                problems.Add($"{Path.GetFileName(path)} is a second table {id}, so it's skipped for {Path.GetFileName(files[id])}.");
        }

        return files;
    }

    // The number in a name like _1_settings.cst, or null for any other file.
    private static int? Number(string name)
    {
        if (!name.StartsWith('_') || !name.EndsWith(".cst", StringComparison.OrdinalIgnoreCase))
            return null;

        var end = name.IndexOf('_', 1);
        return end > 1 && int.TryParse(name.AsSpan(1, end - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? id
            : null;
    }
}
