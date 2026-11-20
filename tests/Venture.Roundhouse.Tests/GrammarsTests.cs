using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using Venture.Roundhouse.Highlighting;

namespace Venture.Roundhouse.Tests;

/// <summary>
/// CST's grammar, which Roundhouse adds to TextMateSharp's: what it calls each part of a table, so the editor colors
/// keys, values, placeholders, references, and comments apart.
/// </summary>
public sealed class GrammarsTests
{
    private static readonly Grammars Grammars = new(new RegistryOptions(ThemeName.LightPlus));

    [Theory]
    [InlineData("_1_strings.cst", Grammars.CstScope)]
    [InlineData("_1_STRINGS.CST", Grammars.CstScope)]
    [InlineData("main.lua", "source.lua")]
    [InlineData("Info.xml", "text.xml")]
    [InlineData("notes", null)]
    public void PicksAGrammarByExtension(string file, string? scope) => Assert.Equal(scope, Grammars.ScopeFor(file));

    [Fact]
    public void NamesEachPartOfATable()
    {
        var grammar = new Registry(Grammars).LoadGrammar(Grammars.CstScope)!;

        Assert.Contains("comment.line.cst", Scopes(grammar, "// a comment", "// a comment"));
        Assert.Contains("support.type.property-name.cst", Scopes(grammar, "ok button ^Accept^", "ok button"));
        Assert.Contains("string.quoted.other.cst", Scopes(grammar, "ok button ^Accept^", "Accept"));
        Assert.Contains("constant.character.format.placeholder.cst", Scopes(grammar, "2 ^%02d items^", "%02d"));
        Assert.Contains("keyword.control.reference.cst",
            Scopes(grammar, "16 ^\u0002<cst><cst_id>9</cst_id><string_id>1</string_id></cst>^", "<cst>"));
    }

    [Fact]
    public void AValueRunsAcrossLines()
    {
        var grammar = new Registry(Grammars).LoadGrammar(Grammars.CstScope)!;
        var first = grammar.TokenizeLine("4 ^A value that runs", null, TimeSpan.FromSeconds(5));
        var second = grammar.TokenizeLine("across lines^", first.RuleStack, TimeSpan.FromSeconds(5));

        Assert.Contains("string.quoted.other.cst", second.Tokens[0].Scopes);
    }

    // The scopes on the token where `part` starts in `line`.
    private static List<string> Scopes(TextMateSharp.Grammars.IGrammar grammar, string line, string part)
    {
        var at = line.IndexOf(part, StringComparison.Ordinal);
        var tokens = grammar.TokenizeLine(line, null, TimeSpan.FromSeconds(5)).Tokens;
        return tokens.Last(token => token.StartIndex <= at).Scopes;
    }
}
