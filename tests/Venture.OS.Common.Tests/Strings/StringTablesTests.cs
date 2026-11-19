using Venture.OS.Interface;
using Venture.OS.Interface.Strings;

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
        File.WriteAllText(Path.Combine(root, "_1_settings.CST"), "1 ^one^\n1 ^again^\n");
        StringTables tables = new(root);

        Assert.Equal("one", tables.Find(new UIStringResource("one", "1", 1)));
        var problem = Assert.Single(tables.Problems);
        Assert.StartsWith("_1_settings.CST, line 2:", problem, StringComparison.Ordinal);
        Assert.Contains("\"1\"", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadsKeysTheTransitionalReaderSkipped()
    {
        // A key above its value, and a numbered key with a name, as The Sims Online's and SSC's tables have them.
        File.WriteAllText(Path.Combine(root, "_1_settings.cst"), "3\n^A key above its value^\n4.label ^A numbered key^\n");
        StringTables tables = new(root);

        Assert.Equal("A key above its value", tables.Find(new UIStringResource("three", "1", 3)));
        Assert.Equal("A numbered key", tables.Find(new UIStringResource("four", "1", 4)));
        Assert.Empty(tables.Problems);
    }

    [Fact]
    public void ReadsValuesAcrossLinesAndSkipsComments()
    {
        File.WriteAllText(Path.Combine(root, "_1_settings.cst"), """
            // A comment, as is a line starting with # and a /* */ block.
            /* 1 ^not a value^ */
            2 ^A value that runs
              across lines^
            3 ^Sell %s for $%d^
            """);
        StringTables tables = new(root);

        Assert.Null(tables.Find(new UIStringResource("one", "1", 1)));
        Assert.Equal("A value that runs\n  across lines", tables.Find(new UIStringResource("two", "1", 2)));
        Assert.Equal("Sell %s for $%d", tables.Find(new UIStringResource("three", "1", 3)));
        Assert.Empty(tables.Problems);
    }

    [Fact]
    public void ReportsAndSkipsAValueNeverClosed()
    {
        File.WriteAllText(Path.Combine(root, "_1_settings.cst"), "1 ^unfinished\n2 ^two^\n");
        StringTables tables = new(root);

        Assert.Null(tables.Find(new UIStringResource("one", "1", 1)));
        Assert.Equal("two", tables.Find(new UIStringResource("two", "1", 2)));
        Assert.Equal("_1_settings.cst, line 1: a value is never closed, so it's skipped.", Assert.Single(tables.Problems));
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
    public void FindsSharedTablesAfterTheDocumentsOwn()
    {
        var shared = Shared();
        File.WriteAllText(Path.Combine(root, "_1_settings.cst"), "1 ^Mine^\n");
        File.WriteAllText(Path.Combine(shared, "_100_common.cst"), "1 ^OK^\n2 ^^\n");
        StringTables tables = new(root, shared);

        Assert.Equal("Mine", tables.Find(new UIStringResource("mine", "1", 1)));
        Assert.Equal("OK", tables.Find(new UIStringResource("ok", "100", 1)));
        Assert.Null(tables.Find(new UIStringResource("none", "100", 9)));
        Assert.Null(new StringTables(root).Find(new UIStringResource("ok", "100", 1)));
        Assert.Empty(tables.Problems);
    }

    [Fact]
    public void AnEmptySharedValueIsText()
    {
        // As in The Sims Online: empty is what the table says, not a reason to look in another layer.
        var shared = Shared();
        File.WriteAllText(Path.Combine(shared, "_100_common.cst"), "2 ^^\n");

        Assert.Equal("", new StringTables(root, shared).Find(new UIStringResource("blank", "100", 2)));
    }

    [Fact]
    public void ReportsAndSkipsAnAppTableInTheSharedRange()
    {
        // Otherwise an app's table 100 would hide the system's.
        var shared = Shared();
        File.WriteAllText(Path.Combine(root, "_100_mine.cst"), "1 ^Mine^\n");
        File.WriteAllText(Path.Combine(shared, "_100_common.cst"), "1 ^OK^\n");
        StringTables tables = new(root, shared);

        Assert.Equal("OK", tables.Find(new UIStringResource("ok", "100", 1)));
        Assert.Contains("_100_mine.cst is numbered 100 or more", Assert.Single(tables.Problems), StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsAndSkipsASharedTableInTheAppsRange()
    {
        // Otherwise a key an app's table lacks would turn up text from a shared table it never named.
        var shared = Shared();
        File.WriteAllText(Path.Combine(root, "_1_settings.cst"), "1 ^Mine^\n");
        File.WriteAllText(Path.Combine(shared, "_1_common.cst"), "1 ^Theirs^\n2 ^Theirs too^\n");
        StringTables tables = new(root, shared);

        Assert.Null(tables.Find(new UIStringResource("two", "1", 2)));
        Assert.Contains("_1_common.cst is a shared table numbered below 100", Assert.Single(tables.Problems), StringComparison.Ordinal);
    }

    [Fact]
    public void TheShippedSharedTablesReadCleanly()
    {
        var shipped = Path.Combine(AppContext.BaseDirectory, "system", "strings");
        StringTables tables = new(root, shipped);

        Assert.Equal("OK", tables.Find(new UIStringResource("ok", "100", 1)));
        Assert.Equal("Cancel", tables.Find(new UIStringResource("cancel", "100", 2)));
        Assert.Equal("Don't Save", tables.Find(new UIStringResource("dontSave", "100", 4)));
        Assert.Empty(tables.Problems);
    }

    private string Shared()
    {
        var shared = Path.Combine(root, "shared");
        Directory.CreateDirectory(shared);
        return shared;
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
