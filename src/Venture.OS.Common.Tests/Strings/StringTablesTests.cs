using Venture.OS.Interface;
using Venture.OS.Strings;

namespace Venture.OS.Tests.Strings;

/// <summary>
/// Finding a <c>&lt;String&gt;</c>'s text in the tables beside a document, named <c>_&lt;id&gt;_&lt;name&gt;.cst</c>,
/// as Hudini's hosts do: the table is the number at the end of its name, and the index is the key.
/// </summary>
public sealed class StringTablesTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-cst-{Guid.NewGuid():N}");

    public StringTablesTests() => Directory.CreateDirectory(root);

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public void FindsTextByTableNumberAndIndex()
    {
        File.WriteAllText(Path.Combine(root, "_1_settings.cst"), "1 ^Sky Blue^\n2 ^Blueberry^\n");
        StringTables tables = new(root);

        Assert.Equal("Sky Blue", tables.Find(new UIStringResource("sky", "1", 1)));
        Assert.Equal("Blueberry", tables.Find(new UIStringResource("berry", "UIText:1", 2)));
        Assert.Empty(tables.Problems);
    }

    [Fact]
    public void MissingTextIsMissing()
    {
        File.WriteAllText(Path.Combine(root, "_1_settings.cst"), "1 ^Sky Blue^\n");
        StringTables tables = new(root);

        Assert.Null(tables.Find(new UIStringResource("gone", "1", 9)));
        Assert.Null(tables.Find(new UIStringResource("none", "7", 1)));
        Assert.Empty(tables.Problems);
    }

    [Fact]
    public void ReportsATablesProblemsWithItsFileName()
    {
        File.WriteAllText(Path.Combine(root, "_1_settings.CST"), "1 ^one^\n^keyless^\n");
        StringTables tables = new(root);

        Assert.Equal("one", tables.Find(new UIStringResource("one", "1", 1)));
        Assert.StartsWith("_1_settings.CST, line 2:", Assert.Single(tables.Problems), StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsASecondTableWithTheSameNumberAndUsesTheFirst()
    {
        File.WriteAllText(Path.Combine(root, "_1_a.cst"), "1 ^from a^\n");
        File.WriteAllText(Path.Combine(root, "_1_b.cst"), "1 ^from b^\n");
        StringTables tables = new(root);

        Assert.Equal("from a", tables.Find(new UIStringResource("one", "1", 1)));
        Assert.Contains("_1_b.cst", Assert.Single(tables.Problems), StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsATableNameWithoutANumber()
    {
        StringTables tables = new(root);

        Assert.Null(tables.Find(new UIStringResource("bad", "settings", 1)));
        Assert.Contains("\"settings\"", Assert.Single(tables.Problems), StringComparison.Ordinal);
    }

    [Fact]
    public void IgnoresOtherFilesAndAMissingFolder()
    {
        File.WriteAllText(Path.Combine(root, "notes.cst"), "1 ^not a table^\n");
        File.WriteAllText(Path.Combine(root, "_x_bad.cst"), "1 ^not a table^\n");

        Assert.Null(new StringTables(root).Find(new UIStringResource("one", "1", 1)));
        Assert.Null(new StringTables(Path.Combine(root, "nowhere")).Find(new UIStringResource("one", "1", 1)));
    }
}
