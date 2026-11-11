using Venture.OS.Scripting;

namespace Venture.OS.Tests.Scripting;

public sealed class ScriptHeaderTests
{
    [Fact]
    public void ReadsTheSummaryUsageAndNeeds()
    {
        var header = Parse("-- Show things.", "-- Usage: show [what]", "-- Needs: system, network", "print()");

        Assert.Equal("Show things.", header.Summary);
        Assert.Equal("show [what]", header.Usage);
        Assert.Equal(ScriptScopes.System | ScriptScopes.Network, header.Needs);
        Assert.Null(header.Error);
    }

    [Theory]
    [InlineData("-- Needs: network", ScriptScopes.Network)]
    [InlineData("-- needs: NETWORK", ScriptScopes.Network)]
    [InlineData("-- Needs: system network", ScriptScopes.System | ScriptScopes.Network)]
    [InlineData("-- Needs:  system ,, network ", ScriptScopes.System | ScriptScopes.Network)]
    [InlineData("-- Needs: system, system", ScriptScopes.System)]
    [InlineData("-- Needs:", ScriptScopes.None)]
    public void AcceptsCommasSpacesAndAnyCase(string line, ScriptScopes expected)
    {
        var header = Parse(line);

        Assert.Equal(expected, header.Needs);
        Assert.Null(header.Error);
    }

    [Fact]
    public void AddsUpSeveralNeedsLines()
    {
        Assert.Equal(ScriptScopes.All, Parse("-- Needs: system", "-- Needs: network, shell files ui").Needs);
    }

    [Fact]
    public void NeedsNothingWithoutANeedsLine()
    {
        var header = Parse("-- Just a summary.", "print()");

        Assert.Equal(ScriptScopes.None, header.Needs);
        Assert.Null(header.Usage);
    }

    [Fact]
    public void IgnoresNeedsLinesAfterTheHeader()
    {
        Assert.Equal(ScriptScopes.None, Parse("-- Summary.", "print()", "-- Needs: network").Needs);
    }

    [Fact]
    public void ReportsAnUnknownScope()
    {
        var header = Parse("-- Needs: network, teleporter");

        Assert.NotNull(header.Error);
        Assert.Contains("teleporter", header.Error);
        Assert.Contains("isn't a known scope", header.Error);
    }

    [Fact]
    public void ReadsTheUiScope()
    {
        var header = Parse("-- Needs: ui files");

        Assert.Null(header.Error);
        Assert.Equal(ScriptScopes.Ui | ScriptScopes.Files, header.Needs);
    }

    [Fact]
    public void SkipsBlankCommentLinesWhenFindingTheSummary()
    {
        Assert.Equal("The summary.", Parse("--", "-- The summary.").Summary);
    }

    [Fact]
    public void DescribesScopes()
    {
        Assert.Equal("none", ScriptScopeNames.Describe(ScriptScopes.None));
        Assert.Equal("network", ScriptScopeNames.Describe(ScriptScopes.Network));
        Assert.Equal("system, network, shell, files, ui", ScriptScopeNames.Describe(ScriptScopes.All));
    }

    [Fact]
    public void RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => ScriptHeader.Parse(null!));
        Assert.Throws<ArgumentNullException>(() => ScriptScopeNames.TryParse(null!, out _, out _));
    }

    private static ScriptHeader Parse(params string[] lines) => ScriptHeader.Parse(lines);
}
