using System.Globalization;
using System.Text;

namespace Venture.OS.Interface.Xml;

/// <summary>A stretch of a document's text, by the index of its first character and its length.</summary>
public readonly record struct FlatXmlSpan(int Start, int Length)
{
    /// <summary>The index just past the span's last character.</summary>
    public int End => Start + Length;

    /// <summary>The span's text in <paramref name="text"/>.</summary>
    public string Of(string text) => text.Substring(Start, Length);
}

/// <summary>
/// Where one attribute sits in the text, so it can be edited in place: the whole attribute, from its name through its
/// closing quote, and its value as written, between the quotes, with its entities still encoded.
/// </summary>
public sealed record FlatXmlAttributeSource(string Name, FlatXmlSpan Span, FlatXmlSpan Value, char Quote);

/// <summary>
/// Where an element sits in the text: the whole element, from its <c>&lt;</c> through its closing <c>&gt;</c>, its
/// children included, the end of its name, its attributes, and, when it isn't written as <c>&lt;Name/&gt;</c>, where
/// its closing tag begins.
/// </summary>
public sealed record FlatXmlElementSource(
    FlatXmlSpan Span,
    int NameEnd,
    IReadOnlyList<FlatXmlAttributeSource> Attributes,
    int? CloseStart);

/// <summary>
/// One element of a <see cref="FlatXml"/> document: a name, its attributes in document order, its line, and, in a
/// nested document, the element it's inside.
/// </summary>
public sealed class FlatXmlElement(
    string name,
    IReadOnlyList<KeyValuePair<string, string>> attributes,
    int line,
    FlatXmlElementSource? source = null,
    int parent = FlatXmlDocument.RootIndex,
    int depth = 0)
{
    /// <summary>The element's name, such as <c>Info</c>.</summary>
    public string Name { get; } = name;

    /// <summary>The attributes in the order they were written.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Attributes { get; } = attributes;

    /// <summary>The line the element starts on, from 1.</summary>
    public int Line { get; } = line;

    /// <summary>
    /// Where the element sits in the text it was read from, so an editor can change it in place; <see
    /// langword="null"/> for an element that wasn't read from text.
    /// </summary>
    public FlatXmlElementSource? Source { get; } = source;

    /// <summary>
    /// The index in <see cref="FlatXmlDocument.Children"/> of the element this one is inside, or
    /// <see cref="FlatXmlDocument.RootIndex"/> when it's directly inside the root.
    /// </summary>
    public int Parent { get; } = parent;

    /// <summary>How deep it is: 0 directly inside the root, 1 inside one of those, and so on.</summary>
    public int Depth { get; } = depth;

    /// <summary>The value of an attribute, or <see langword="null"/> when it's absent. Names match exactly.</summary>
    public string? this[string attribute]
    {
        get
        {
            foreach (var (key, value) in Attributes)
            {
                if (key == attribute)
                    return value;
            }

            return null;
        }
    }
}

/// <summary>
/// A <see cref="FlatXml"/> document: one root element and every element inside it, in the order they're written.
/// </summary>
/// <remarks>
/// <see cref="Children"/> is one list even when elements nest: each element comes straight before the elements
/// inside it, so an index always means the element at that place in the text, and a document that doesn't nest
/// numbers its elements as it always did. <see cref="FlatXmlElement.Parent"/> and <see cref="End"/> recover the
/// structure.
/// </remarks>
public sealed class FlatXmlDocument(FlatXmlElement root, IReadOnlyList<FlatXmlElement> children)
{
    /// <summary>The root's index, wherever an element's index or parent is asked for.</summary>
    public const int RootIndex = -1;

    /// <summary>The root element.</summary>
    public FlatXmlElement Root { get; } = root;

    /// <summary>Every element inside the root, in document order, each before the elements inside it.</summary>
    public IReadOnlyList<FlatXmlElement> Children { get; } = children;

    /// <summary>
    /// The index just past the elements inside <paramref name="element"/>, so they're the ones from
    /// <c>element + 1</c> up to it; for <see cref="RootIndex"/>, the count of <see cref="Children"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">There's no such element.</exception>
    public int End(int element)
    {
        if (element == RootIndex)
            return Children.Count;
        if (element < 0 || element >= Children.Count)
            throw new ArgumentOutOfRangeException(nameof(element), element, $"The document has {Children.Count} elements inside its root.");

        var depth = Children[element].Depth;
        var end = element + 1;
        while (end < Children.Count && Children[end].Depth > depth)
            end++;
        return end;
    }

    /// <summary>The indices of the elements directly inside <paramref name="element"/>, or the root's, in order.</summary>
    /// <exception cref="ArgumentOutOfRangeException">There's no such element.</exception>
    public int[] ChildrenOf(int element)
    {
        var end = End(element);
        List<int> direct = [];
        for (var index = element + 1; index < end; index = End(index))
            direct.Add(index);
        return [.. direct];
    }
}

/// <summary>Why a <see cref="FlatXml"/> document couldn't be read, with the line it happened on.</summary>
public sealed class FlatXmlException(string message, int line) : FormatException($"Line {line}: {message}")
{
    /// <summary>The line of the problem, from 1.</summary>
    public int Line { get; } = line;

    /// <summary>The problem, without the line number.</summary>
    public string Reason { get; } = message;
}

/// <summary>
/// Reads the flat XML that UIScript (<c>.uix</c>) and bundle <c>Info.xml</c> files use.
/// </summary>
/// <remarks>
/// <para>Cosmos 3.0.94 can't link <c>System.Xml</c> (it needs <c>RhGetMaxGcGeneration</c>, which Cosmos's runtime
/// lacks), and these formats need only a small part of XML, so this reads exactly that part:</para>
/// <list type="bullet">
/// <item><description>An optional <c>&lt;?xml ...?&gt;</c> declaration first, and comments anywhere.</description></item>
/// <item><description>One root element, holding elements that don't nest further, unless the caller allows
/// nesting, as UIScript does; then they may hold elements too, up to <see cref="MaximumDepth"/> deep. Elements hold
/// no text, only attributes, in single or double quotes.</description></item>
/// <item><description>The five standard entities, such as <c>&amp;amp;</c>, and numeric references such as
/// <c>&amp;#233;</c>.</description></item>
/// </list>
/// <para>Anything else, such as a <c>DOCTYPE</c>, text, nesting the caller didn't allow, or an unknown entity, is
/// refused with the line it's on, rather than read some other way. A document either reads completely or
/// throws.</para>
/// </remarks>
public static class FlatXml
{
    /// <summary>
    /// How deep elements may nest inside the root when nesting is allowed. It bounds the reader, which reads each
    /// level by recursing, so a hostile document can't exhaust the stack; real documents nest a few levels at most.
    /// </summary>
    public const int MaximumDepth = 32;

    /// <summary>Reads a document.</summary>
    /// <param name="text">The document's text.</param>
    /// <param name="allowNesting">
    /// Whether the root's elements may hold elements of their own. Formats that never nest, such as a bundle's
    /// <c>Info.xml</c>, leave it off, so nesting is refused as malformed.
    /// </param>
    /// <exception cref="FlatXmlException">The text isn't well-formed flat XML.</exception>
    public static FlatXmlDocument Parse(string text, bool allowNesting = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new Reader(text, allowNesting).ReadDocument();
    }

    /// <summary>Whether <paramref name="name"/> can be written as an element or attribute name, as this reader reads one.</summary>
    public static bool IsName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0 || !Reader.IsNameStart(name[0]))
            return false;

        foreach (var character in name)
        {
            if (!Reader.IsNameCharacter(character))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Writes <paramref name="value"/> as an attribute value between <paramref name="quote"/>s, so this reader reads
    /// it back exactly: <c>&amp;</c>, <c>&lt;</c>, and the quote become entities, and tabs and line breaks become
    /// character references, which, unlike the characters themselves, aren't read as spaces.
    /// </summary>
    public static string EncodeValue(string value, char quote)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (quote is not ('"' or '\''))
            throw new ArgumentOutOfRangeException(nameof(quote), quote, "An attribute value is quoted with \" or '.");

        StringBuilder encoded = new(value.Length);
        foreach (var character in value)
        {
            encoded.Append(character switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '"' when quote == '"' => "&quot;",
                '\'' when quote == '\'' => "&apos;",
                '\t' => "&#9;",
                '\n' => "&#10;",
                '\r' => "&#13;",
                _ => character.ToString(),
            });
        }

        return encoded.ToString();
    }

    /// <summary>Reads a UTF-8 file.</summary>
    /// <exception cref="FlatXmlException">The text isn't well-formed flat XML.</exception>
    /// <exception cref="IOException">The file couldn't be read.</exception>
    public static FlatXmlDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Parse(File.ReadAllText(path, Encoding.UTF8));
    }

    private sealed class Reader(string text, bool allowNesting)
    {
        private int position;
        private int line = 1;

        public FlatXmlDocument ReadDocument()
        {
            // A byte order mark is allowed before everything else.
            if (Peek() == '\uFEFF')
                Advance();

            if (StartsWith("<?xml"))
                SkipDeclaration();

            // SkipMisc refuses a DOCTYPE, CDATA, or processing instruction wherever one appears.
            SkipMisc();
            if (AtEnd)
                throw Error("The document is empty.");

            List<FlatXmlElement?> children = [];
            var root = ReadElement(children, FlatXmlDocument.RootIndex, -1)!;

            SkipMisc();
            if (!AtEnd)
                throw Error($"Nothing may follow the closing </{root.Name}>.");

            return new FlatXmlDocument(root, [.. children.Select(child => child!)]);
        }

        /// <summary>
        /// Reads one element and everything inside it. Each element takes its place in <paramref name="children"/>
        /// before those inside it, so the list is in document order; the root (at depth -1) isn't added.
        /// </summary>
        private FlatXmlElement? ReadElement(List<FlatXmlElement?> children, int parent, int depth)
        {
            var elementLine = line;
            var start = position;
            var (name, attributes, empty, nameEnd, sources) = ReadStartTag();
            var index = FlatXmlDocument.RootIndex;
            if (depth >= 0)
            {
                index = children.Count;
                children.Add(null);
            }

            int? close = null;
            if (!empty)
            {
                while (true)
                {
                    SkipMisc();
                    if (AtEnd)
                        throw Error($"<{name}> is never closed.");
                    if (StartsWith("</"))
                    {
                        close = position;
                        ReadEndTag(name);
                        break;
                    }

                    // Inside the root, the only question is text; inside another element, whether it may hold any.
                    if (depth >= 0 && !allowNesting)
                        throw Error($"<{name}> can't hold text or other elements.");
                    if (Peek() != '<')
                        throw Error($"Text isn't allowed inside <{name}>; put it in an attribute.");
                    if (depth + 1 >= MaximumDepth)
                        throw Error($"Elements nest more than {MaximumDepth} deep inside <{name}>.");

                    ReadElement(children, index, depth + 1);
                }
            }

            FlatXmlElementSource source = new(new(start, position - start), nameEnd, sources, close);
            FlatXmlElement element = new(name, attributes, elementLine, source, parent, Math.Max(depth, 0));
            if (depth >= 0)
                children[index] = element;
            return element;
        }

        private bool AtEnd => position >= text.Length;

        private char Peek(int offset = 0) => position + offset < text.Length ? text[position + offset] : '\0';

        private bool StartsWith(string value) => string.CompareOrdinal(text, position, value, 0, value.Length) == 0;

        private void Advance(int count = 1)
        {
            for (var index = 0; index < count && position < text.Length; index++)
            {
                if (text[position] == '\n')
                    line++;
                position++;
            }
        }

        private void SkipWhitespace()
        {
            while (!AtEnd && IsWhitespace(Peek()))
                Advance();
        }

        /// <summary>Skips whitespace and comments.</summary>
        private void SkipMisc()
        {
            while (true)
            {
                SkipWhitespace();
                if (StartsWith("<!--"))
                {
                    var end = text.IndexOf("-->", position + 4, StringComparison.Ordinal);
                    if (end < 0)
                        throw Error("A comment is never closed.");
                    Advance(end + 3 - position);
                }
                else if (StartsWith("<?"))
                {
                    throw Error("Processing instructions aren't allowed, except the <?xml ...?> declaration first.");
                }
                else if (StartsWith("<![CDATA["))
                {
                    throw Error("CDATA sections aren't allowed.");
                }
                else if (StartsWith("<!DOCTYPE"))
                {
                    throw Error("DOCTYPE declarations aren't allowed.");
                }
                else
                {
                    return;
                }
            }
        }

        private void SkipDeclaration()
        {
            var end = text.IndexOf("?>", position, StringComparison.Ordinal);
            if (end < 0)
                throw Error("The <?xml ...?> declaration is never closed.");
            Advance(end + 2 - position);
        }

        private (string Name, List<KeyValuePair<string, string>> Attributes, bool Empty, int NameEnd,
            List<FlatXmlAttributeSource> Sources) ReadStartTag()
        {
            if (Peek() != '<')
                throw Error("Expected an element.");
            Advance();

            var name = ReadName("an element name");
            var nameEnd = position;
            List<KeyValuePair<string, string>> attributes = [];
            List<FlatXmlAttributeSource> sources = [];

            while (true)
            {
                var hadSpace = IsWhitespace(Peek());
                SkipWhitespace();
                if (AtEnd)
                    throw Error($"<{name} is never closed.");

                if (StartsWith("/>"))
                {
                    Advance(2);
                    return (name, attributes, true, nameEnd, sources);
                }

                if (Peek() == '>')
                {
                    Advance();
                    return (name, attributes, false, nameEnd, sources);
                }

                if (!hadSpace)
                    throw Error($"Expected a space before the next attribute of <{name}>.");

                var attributeLine = line;
                var attributeStart = position;
                var attribute = ReadName("an attribute name");
                SkipWhitespace();
                if (Peek() != '=')
                    throw Error($"The attribute {attribute} of <{name}> needs a value, as {attribute}=\"...\".");
                Advance();
                SkipWhitespace();

                var quote = Peek();
                var valueStart = position + 1;
                var value = ReadValue(attribute);
                FlatXmlSpan valueSpan = new(valueStart, position - 1 - valueStart);
                foreach (var (existing, _) in attributes)
                {
                    if (existing == attribute)
                        throw new FlatXmlException($"<{name}> has the attribute {attribute} twice.", attributeLine);
                }

                attributes.Add(new(attribute, value));
                sources.Add(new(attribute, new(attributeStart, position - attributeStart), valueSpan, quote));
            }
        }

        private void ReadEndTag(string name)
        {
            Advance(2);
            var closing = ReadName("an element name");
            if (closing != name)
                throw Error($"</{closing}> closes <{name}>; expected </{name}>.");
            SkipWhitespace();
            if (Peek() != '>')
                throw Error($"</{name} is never closed.");
            Advance();
        }

        private string ReadName(string what)
        {
            var start = position;
            if (!IsNameStart(Peek()))
                throw Error($"Expected {what}.");

            while (!AtEnd && IsNameCharacter(Peek()))
                Advance();

            return text[start..position];
        }

        private string ReadValue(string attribute)
        {
            var quote = Peek();
            if (quote is not ('"' or '\''))
                throw Error($"The value of {attribute} must be in quotes.");
            Advance();

            StringBuilder value = new();
            while (true)
            {
                if (AtEnd)
                    throw Error($"The value of {attribute} is never closed.");

                var character = Peek();
                if (character == quote)
                {
                    Advance();
                    return value.ToString();
                }

                switch (character)
                {
                    case '<':
                        throw Error($"The value of {attribute} can't contain <; write &lt;.");
                    case '&':
                        value.Append(ReadEntity());
                        break;
                    case '\t' or '\n' or '\r':
                        // XML turns whitespace characters in attribute values into spaces.
                        value.Append(' ');
                        Advance();
                        break;
                    default:
                        value.Append(character);
                        Advance();
                        break;
                }
            }
        }

        private string ReadEntity()
        {
            var end = text.IndexOf(';', position);
            if (end < 0 || end - position > 12)
                throw Error("An & must start an entity such as &amp;.");

            var entity = text[(position + 1)..end];
            Advance(end + 1 - position);

            switch (entity)
            {
                case "lt": return "<";
                case "gt": return ">";
                case "amp": return "&";
                case "quot": return "\"";
                case "apos": return "'";
            }

            if (entity.StartsWith('#'))
            {
                var isHex = entity.Length > 1 && entity[1] is 'x' or 'X';
                var digits = isHex ? entity[2..] : entity[1..];
                var style = isHex ? NumberStyles.AllowHexSpecifier : NumberStyles.None;
                if (digits.Length > 0 && int.TryParse(digits, style, CultureInfo.InvariantCulture, out var code)
                    && code is > 0 and <= 0x10FFFF and not (>= 0xD800 and <= 0xDFFF))
                    return char.ConvertFromUtf32(code);

                throw Error($"&{entity}; isn't a valid character reference.");
            }

            throw Error($"&{entity}; isn't a known entity; only &lt;, &gt;, &amp;, &quot;, and &apos; are.");
        }

        private FlatXmlException Error(string message) => new(message, line);

        private static bool IsWhitespace(char character) => character is ' ' or '\t' or '\n' or '\r';

        internal static bool IsNameStart(char character) =>
            char.IsLetter(character) || character is '_' or ':';

        internal static bool IsNameCharacter(char character) =>
            IsNameStart(character) || char.IsDigit(character) || character is '-' or '.';
    }
}
