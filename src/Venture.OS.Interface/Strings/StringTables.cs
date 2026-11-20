using System.Globalization;
using CSTNet;

namespace Venture.OS.Interface.Strings;

/// <summary>
/// The CST tables a document's <c>&lt;String&gt;</c> elements find their text in, as Hudini's hosts read The Sims
/// Online's: the document's own, beside it, laid over the tables every app shares.
/// </summary>
/// <remarks>
/// <para>A table is a file named <c>_&lt;id&gt;_&lt;name&gt;.cst</c>; only the number counts. A
/// <c>&lt;String table="1" index="3"/&gt;</c> (or <c>table="UIText:1"</c>, the number at the end) finds the value
/// with the key <c>3</c> in table 1. Each table is read once, the first time it's asked for.</para>
/// <para>The tables are layers, looked through in order, as the CST specification's layered tables are: the
/// document's folder first, then the shared folder, such as <c>/mnt/system/strings</c>. Numbers are split between
/// them so a key missing from an app's table never turns up text from a shared table it didn't ask for: an app's
/// tables are numbered below <see cref="FirstSharedTable"/> and the shared ones from it up, and a table in the wrong
/// range is reported and skipped. An empty value is text, as in The Sims Online, not a reason to look further.</para>
/// <para>A value can instead refer to another table's text, as some of The Sims Online's do, which this follows one
/// step, filling the text's placeholders with the reference's arguments. The other table may be in either layer.</para>
/// <para>CSTNet reads each table to the CST specification, so a key above its value, or a numbered key such as
/// <c>3.label</c>, finds its value too. Text that isn't there is missing, so the view can show and list it. A table's
/// problems, and a table name without a number, are kept in <see cref="Problems"/> for the app to be told.</para>
/// </remarks>
public sealed class StringTables
{
    /// <summary>The first table number kept for the shared tables; an app's own tables are numbered below it.</summary>
    public const int FirstSharedTable = 100;

    private readonly List<string> problems = [];
    private readonly Layer[] layers;

    /// <summary>Finds tables in <paramref name="folder"/>, then in <paramref name="sharedFolder"/> when there's one.</summary>
    /// <param name="read">
    /// Reads a table's text by its path, such as an editor's unsaved text in place of what's saved; by default, the
    /// file. It may throw <see cref="IOException"/>, <see cref="UnauthorizedAccessException"/>, or
    /// <see cref="InvalidDataException"/>, which are reported as the table not being read.
    /// </param>
    public StringTables(string folder, string? sharedFolder = null, Func<string, string>? read = null)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var reader = read ?? File.ReadAllText;
        layers = sharedFolder is null
            ? [new Layer(folder, shared: false, problems, reader)]
            : [new Layer(folder, shared: false, problems, reader), new Layer(sharedFolder, shared: true, problems, reader)];
    }

    /// <summary>
    /// The number a table's file name gives it, such as 1 for <c>_1_settings.cst</c>, or <see langword="null"/> for a
    /// name that isn't a table's, which no document finds.
    /// </summary>
    public static int? TableNumber(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        return Number(fileName);
    }

    /// <summary>
    /// Reads table <paramref name="table"/> from the document's own folder and follows every reference in it, so an
    /// editor can show everything wrong with the table at once: what the reader skipped, a number in the shared range,
    /// and references it can't follow.
    /// </summary>
    /// <returns>The problems found, each once.</returns>
    public IReadOnlyList<string> Check(int table)
    {
        var known = problems.Count;
        if (layers[0].Table(table) is { } found)
        {
            foreach (var entry in found.Entries)
            {
                if (entry.Key is { } key && entry.Value is { } value && CstReference.Is(value))
                    _ = Follow(table, key, value);
            }
        }

        List<string> lately = [];
        for (var index = known; index < problems.Count; index++)
            lately.Add(problems[index]);
        return lately;
    }

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

        return Find(id, resource.Index.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The text under <paramref name="key"/> in table <paramref name="table"/>, a reference followed, or
    /// <see langword="null"/> when there's none. Code asks this way, by key, since a table's keys needn't be numbers.
    /// </summary>
    public string? Find(int table, string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (Lookup(table, key) is not { } value)
            return null;

        return CstReference.Is(value) ? Follow(table, key, value) : value;
    }

    // A value as written, from the first layer that has it.
    private string? Lookup(int id, string key)
    {
        foreach (var layer in layers)
        {
            if (layer.Table(id) is { } table && table.TryGetValue(key, out var value))
                return value;
        }

        return null;
    }

    // The text a reference names, with its arguments filling the text's placeholders. As the CST specification says,
    // it goes one step, so a cycle can't hang it; anything it can't follow is reported and the text is missing.
    private string? Follow(int id, string key, string value)
    {
        var where = $"Table {id}, key {key}";
        if (CstReference.Read(value) is not { } reference)
        {
            Report($"{where}: its reference to another table's text is malformed, so it's missing.");
            return null;
        }

        var target = $"table {reference.Table}, key {reference.Key}";
        if (Lookup(reference.Table, reference.Key) is not { } text)
        {
            Report($"{where} refers to {target}, which isn't there, so it's missing.");
            return null;
        }

        if (CstReference.Is(text))
        {
            Report($"{where} refers to {target}, which is a reference itself; references go one step, so it's missing.");
            return null;
        }

        object?[] arguments = [.. reference.Arguments];
        return arguments.Length == 0 ? text : CST.Format(text, arguments);
    }

    // Each problem once, however often a string that has it is asked for.
    private void Report(string problem)
    {
        if (!problems.Contains(problem))
            problems.Add(problem);
    }

    // One folder of tables, read as they're asked for. shared says which range of numbers it may hold.
    private sealed class Layer(string folder, bool shared, List<string> problems, Func<string, string> read)
    {
        private readonly Dictionary<int, CSTTable?> tables = [];
        private Dictionary<int, string>? files;

        public CSTTable? Table(int id)
        {
            if (tables.TryGetValue(id, out var table))
                return table;

            table = null;
            if (Files().TryGetValue(id, out var path))
            {
                try
                {
                    table = CSTTable.Parse(read(path));
                    foreach (var problem in table.Problems)
                        problems.Add($"{Path.GetFileName(path)}, {Describe(problem)}");
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
                {
                    problems.Add($"Couldn't read {Path.GetFileName(path)}: {exception.Message}");
                }
            }

            tables[id] = table;
            return table;
        }

        // The tables in the folder by number, worked out once. A second file with a number already taken, or a number
        // outside the layer's range, is reported.
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
                var name = Path.GetFileName(path);
                if (Number(name) is not { } id)
                    continue;
                if (id >= FirstSharedTable != shared)
                {
                    problems.Add(shared
                        ? $"{name} is a shared table numbered below {FirstSharedTable}, which are apps' own, so it's skipped."
                        : $"{name} is numbered {FirstSharedTable} or more, which are kept for the shared tables, so it's skipped.");
                    continue;
                }

                if (!files.TryAdd(id, path))
                    problems.Add($"{name} is a second table {id}, so it's skipped for {Path.GetFileName(files[id])}.");
            }

            return files;
        }
    }

    // CSTNet reports a problem's kind, line, and key; these say what the reader did about it.
    private static string Describe(CSTProblem problem) => problem.Kind switch
    {
        CSTProblemKind.UnclosedValue => $"line {problem.Line}: a value is never closed, so it's skipped.",
        CSTProblemKind.RepeatedKey =>
            $"line {problem.Line}: the key \"{problem.Key}\" is used again, so this value is skipped and the first kept.",
        _ => $"line {problem.Line}: {problem.Kind}.",
    };

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
