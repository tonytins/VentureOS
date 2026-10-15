using Venture.OS.Shell;

namespace Venture.OS.Tests.Shell;

public sealed class CommandLineTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" \t  ")]
    [InlineData("help", "help")]
    [InlineData("help info", "help", "info")]
    [InlineData("  help   info  ", "help", "info")]
    [InlineData("help\tinfo", "help", "info")]
    [InlineData("HELP", "HELP")]
    public void SplitsOnSpacesAndTabs(string input, params string[] expected) => AssertWords(input, expected);

    [Theory]
    [InlineData("'a b'", "a b")]
    [InlineData(@"'a \ "" b'", @"a \ "" b")]
    [InlineData("''", "")]
    [InlineData("x '' y", "x", "", "y")]
    [InlineData("'it''s'", "its")]
    public void SingleQuotesKeepEverythingLiterally(string input, params string[] expected) => AssertWords(input, expected);

    [Theory]
    [InlineData(@"""a b""", "a b")]
    [InlineData(@"""a \"" b""", @"a "" b")]
    [InlineData(@"""a \\ b""", @"a \ b")]
    [InlineData(@"""a \n b""", @"a \n b")]
    [InlineData(@"""""", "")]
    [InlineData(@"help ""net""", "help", "net")]
    public void DoubleQuotesAllowOnlyQuoteAndBackslashEscapes(string input, params string[] expected) =>
        AssertWords(input, expected);

    [Theory]
    [InlineData(@"a\ b", "a b")]
    [InlineData(@"\'quoted\'", "'quoted'")]
    [InlineData(@"\\", @"\")]
    public void BackslashOutsideQuotesMakesTheNextCharacterLiteral(string input, params string[] expected) =>
        AssertWords(input, expected);

    [Theory]
    [InlineData(@"a""b c""", "ab c")]
    [InlineData(@"'a'b""c""", "abc")]
    [InlineData(@"pre'mid'post rest", "premidpost", "rest")]
    public void QuotedAndUnquotedPartsNextToEachOtherFormOneWord(string input, params string[] expected) =>
        AssertWords(input, expected);

    [Theory]
    [InlineData("'open", "single quote")]
    [InlineData(@"""open", "double quote")]
    [InlineData(@"""a \""", "double quote")]
    [InlineData(@"trailing\", "backslash")]
    [InlineData(@"help ""net", "double quote")]
    public void ReportsUnfinishedQuotesAndEscapes(string input, string expectedInError)
    {
        var parsed = CommandLine.TryParse(input, out _, out var error);

        Assert.False(parsed);
        Assert.NotNull(error);
        Assert.Contains(expectedInError, error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsNullInput() =>
        Assert.Throws<ArgumentNullException>(() => CommandLine.TryParse(null!, out _, out _));

    private static void AssertWords(string input, string[] expected)
    {
        var parsed = CommandLine.TryParse(input, out var words, out var error);

        Assert.True(parsed, error);
        Assert.Null(error);
        Assert.Equal(expected, words);
    }
}
