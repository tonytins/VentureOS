using TextMateSharp.Grammars;
using TextMateSharp.Internal.Grammars.Reader;
using TextMateSharp.Internal.Types;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace Venture.Roundhouse.Highlighting;

/// <summary>
/// The grammars Roundhouse highlights with: TextMateSharp's own, such as Lua's and XML's, and the ones VentureOS's
/// formats need that it doesn't have, starting with CST's.
/// </summary>
/// <remarks>
/// TextMateSharp finds its grammars by scope through <see cref="IRegistryOptions"/>, so this answers for its own
/// scopes from grammars built into Roundhouse and passes every other request to <see cref="RegistryOptions"/>.
/// </remarks>
public sealed class Grammars(RegistryOptions builtIn) : IRegistryOptions
{
    /// <summary>The scope of CST tables (<c>.cst</c>), which <see cref="ScopeFor"/> gives them.</summary>
    public const string CstScope = "source.cst";

    private IRawGrammar? cst;

    /// <summary>TextMateSharp's own grammars and themes, for what this doesn't add.</summary>
    public RegistryOptions BuiltIn { get; } = builtIn ?? throw new ArgumentNullException(nameof(builtIn));

    /// <summary>The scope to highlight a file with, by its extension, or <see langword="null"/> for none.</summary>
    public string? ScopeFor(string file)
    {
        var extension = Path.GetExtension(file);
        if (string.Equals(extension, ".cst", StringComparison.OrdinalIgnoreCase))
            return CstScope;

        return BuiltIn.GetLanguageByExtension(extension) is { } language ? BuiltIn.GetScopeByLanguageId(language.Id) : null;
    }

    /// <inheritdoc/>
    public IRawGrammar GetGrammar(string scopeName) =>
        scopeName == CstScope ? cst ??= Load("cst.tmLanguage.json") : BuiltIn.GetGrammar(scopeName);

    /// <inheritdoc/>
    public IRawTheme GetTheme(string scopeName) => BuiltIn.GetTheme(scopeName);

    /// <inheritdoc/>
    public ICollection<string> GetInjections(string scopeName) => BuiltIn.GetInjections(scopeName);

    /// <inheritdoc/>
    public IRawTheme GetDefaultTheme() => BuiltIn.GetDefaultTheme();

    // A grammar built into Roundhouse. A missing one is a build mistake, so it throws rather than highlighting nothing.
    private static IRawGrammar Load(string name)
    {
        var resource = $"Venture.Roundhouse.Highlighting.{name}";
        using var stream = typeof(Grammars).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Roundhouse was built without its {name} grammar ({resource}).");
        using StreamReader reader = new(stream);
        return GrammarReader.ReadGrammarSync(reader);
    }
}
