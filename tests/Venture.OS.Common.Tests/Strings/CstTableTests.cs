using Venture.OS.Strings;

namespace Venture.OS.Tests.Strings;

/// <summary>
/// The transitional CST reader: the part of the CST specification (cstdotnet's draft) whose meaning is settled, and
/// a report, never a guess, for the rest, so every table it reads means the same to a full reader.
/// </summary>
public sealed class CstTableTests
{
    [Fact]
    public void ReadsKeysOnTheValuesLine()
    {
        var table = CstTable.Read("""
            1 ^A value after its key^
                2 ^An indented key^
            ok button ^A key with spaces^
            kErrNoFood ^A word for a key^
            """);

        Assert.Equal("A value after its key", table.Values["1"]);
        Assert.Equal("An indented key", table.Values["2"]);
        Assert.Equal("A key with spaces", table.Values["ok button"]);
        Assert.Equal("A word for a key", table.Values["kErrNoFood"]);
        Assert.Empty(table.Problems);
    }

    [Fact]
    public void KeepsAValuesLineBreaksAndSpacingExactly()
    {
        var table = CstTable.Read("4 ^A value that runs\r\n  across lines ^\r\n5 ^^\n");

        Assert.Equal("A value that runs\n  across lines ", table.Values["4"]);
        Assert.Equal("", table.Values["5"]);
    }

    [Fact]
    public void AcceptsEveryLineEnding()
    {
        var table = CstTable.Read("1 ^one\rtwo^\u20282 ^three^");

        Assert.Equal("one\ntwo", table.Values["1"]);
        Assert.Equal("three", table.Values["2"]);
    }

    [Fact]
    public void IgnoresCommentsAndTextOutsideValues()
    {
        var table = CstTable.Read("""
            // A comment. So is a line starting with #, and a /* */ block.
            # A header
            /* A block
            1 ^not a value^
            that ends here */
            A note for translators, which no value follows on its line.
            2 ^A value^ with a note after it
            3 ^A value
              // a comment line inside a value, which isn't part of it
            that goes on^
            """);

        Assert.Equal(["2", "3"], table.Values.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("A value", table.Values["2"]);
        Assert.Equal("A value\nthat goes on", table.Values["3"]);
        Assert.Empty(table.Problems);
    }

    [Theory]
    [InlineData("^A value on its own^")]
    [InlineData("3\n^A key above its value^")]
    [InlineData("kErrNoFood\n^A name above its value^")]
    public void ReportsAndSkipsAValueWithoutAKeyOnItsLine(string text)
    {
        var table = CstTable.Read(text + "\n9 ^after^");

        Assert.Equal(["9"], table.Values.Keys);
        Assert.Contains("key", Assert.Single(table.Problems), StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsAndSkipsASecondValueOnALine()
    {
        var table = CstTable.Read("1 ^one^ 2 ^two^\n3 ^three^");

        Assert.Equal("one", table.Values["1"]);
        Assert.False(table.Values.ContainsKey("2"));
        Assert.Equal("three", table.Values["3"]);
        Assert.Contains("Line 1", Assert.Single(table.Problems), StringComparison.Ordinal);
    }

    [Fact]
    public void KeepsTheFirstOfARepeatedKey()
    {
        var table = CstTable.Read("2 ^first^\n2 ^second^");

        Assert.Equal("first", table.Values["2"]);
        Assert.Contains("\"2\"", Assert.Single(table.Problems), StringComparison.Ordinal);
    }

    [Fact]
    public void ALineWithAWholeEntryEndsAValueNeverClosed()
    {
        // As the specification says, one stray caret mustn't misread every later entry.
        var table = CstTable.Read("1 ^unfinished\n2 ^two^\n3 ^three^");

        Assert.False(table.Values.ContainsKey("1"));
        Assert.Equal("two", table.Values["2"]);
        Assert.Equal("three", table.Values["3"]);
        Assert.Contains("Line 1", Assert.Single(table.Problems), StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsAValueStillOpenAtTheEnd()
    {
        var table = CstTable.Read("1 ^one^\n2 ^never closed");

        Assert.Equal(["1"], table.Values.Keys);
        Assert.Contains("Line 2", Assert.Single(table.Problems), StringComparison.Ordinal);
    }

    [Fact]
    public void ReturnsValuesUntouched()
    {
        var table = CstTable.Read("1 ^Sell %s for $%d, 100%%^");

        Assert.Equal("Sell %s for $%d, 100%%", table.Values["1"]);
    }
}
