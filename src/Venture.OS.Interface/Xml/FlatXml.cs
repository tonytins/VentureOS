using System.Globalization;
using System.Text;

namespace Venture.OS.Xml;

/// <summary>
/// One element of a <see cref="FlatXml"/> document: a name, its attributes in document order, and its line.
/// </summary>
public sealed class FlatXmlElement(string name, IReadOnlyList<KeyValuePair<string, string>> attributes, int line)
{
    /// <summary>The element's name, such as <c>Info</c>.</summary>
    public string Name { get; } = name;

    /// <summary>The attributes in the order they were written.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Attributes { get; } = attributes;

    /// <summary>The line the element starts on, from 1.</summary>
    public int Line { get; } = line;

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

/// <summary>A <see cref="FlatXml"/> document: one root element and the elements directly inside it.</summary>
public sealed class FlatXmlDocument(FlatXmlElement root, IReadOnlyList<FlatXmlElement> children)
{
    /// <summary>The root element.</summary>
    public FlatXmlElement Root { get; } = root;

    /// <summary>The elements inside the root, in document order.</summary>
    public IReadOnlyList<FlatXmlElement> Children { get; } = children;
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
/// <item><description>One root element, holding elements that don't nest further. Elements hold no text, only
/// attributes, in single or double quotes.</description></item>
/// <item><description>The five standard entities, such as <c>&amp;amp;</c>, and numeric references such as
/// <c>&amp;#233;</c>.</description></item>
/// </list>
/// <para>Anything else, such as a <c>DOCTYPE</c>, text, deeper nesting, or an unknown entity, is refused with the
/// line it's on, rather than read some other way. A document either reads completely or throws.</para>
/// </remarks>
public static class FlatXml
{
    /// <summary>Reads a document.</summary>
    /// <exception cref="FlatXmlException">The text isn't well-formed flat XML.</exception>
    public static FlatXmlDocument Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new Reader(text).ReadDocument();
    }

    /// <summary>Reads a UTF-8 file.</summary>
    /// <exception cref="FlatXmlException">The text isn't well-formed flat XML.</exception>
    /// <exception cref="IOException">The file couldn't be read.</exception>
    public static FlatXmlDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Parse(File.ReadAllText(path, Encoding.UTF8));
    }

    private sealed class Reader(string text)
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

            var rootLine = line;
            var (rootName, rootAttributes, rootEmpty) = ReadStartTag();
            var root = new FlatXmlElement(rootName, rootAttributes, rootLine);
            List<FlatXmlElement> children = [];

            if (!rootEmpty)
            {
                while (true)
                {
                    SkipMisc();
                    if (AtEnd)
                        throw Error($"<{rootName}> is never closed.");
                    if (StartsWith("</"))
                    {
                        ReadEndTag(rootName);
                        break;
                    }

                    if (Peek() != '<')
                        throw Error($"Text isn't allowed inside <{rootName}>; put it in an attribute.");

                    var childLine = line;
                    var (name, attributes, empty) = ReadStartTag();
                    if (!empty)
                    {
                        SkipMisc();
                        if (!StartsWith("</"))
                            throw Error($"<{name}> can't hold text or other elements.");
                        ReadEndTag(name);
                    }

                    children.Add(new FlatXmlElement(name, attributes, childLine));
                }
            }

            SkipMisc();
            if (!AtEnd)
                throw Error($"Nothing may follow the closing </{rootName}>.");

            return new FlatXmlDocument(root, children);
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

        private (string Name, List<KeyValuePair<string, string>> Attributes, bool Empty) ReadStartTag()
        {
            if (Peek() != '<')
                throw Error("Expected an element.");
            Advance();

            var name = ReadName("an element name");
            List<KeyValuePair<string, string>> attributes = [];

            while (true)
            {
                var hadSpace = IsWhitespace(Peek());
                SkipWhitespace();
                if (AtEnd)
                    throw Error($"<{name} is never closed.");

                if (StartsWith("/>"))
                {
                    Advance(2);
                    return (name, attributes, true);
                }

                if (Peek() == '>')
                {
                    Advance();
                    return (name, attributes, false);
                }

                if (!hadSpace)
                    throw Error($"Expected a space before the next attribute of <{name}>.");

                var attributeLine = line;
                var attribute = ReadName("an attribute name");
                SkipWhitespace();
                if (Peek() != '=')
                    throw Error($"The attribute {attribute} of <{name}> needs a value, as {attribute}=\"...\".");
                Advance();
                SkipWhitespace();

                var value = ReadValue(attribute);
                foreach (var (existing, _) in attributes)
                {
                    if (existing == attribute)
                        throw new FlatXmlException($"<{name}> has the attribute {attribute} twice.", attributeLine);
                }

                attributes.Add(new(attribute, value));
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

        private static bool IsNameStart(char character) =>
            char.IsLetter(character) || character is '_' or ':';

        private static bool IsNameCharacter(char character) =>
            IsNameStart(character) || char.IsDigit(character) || character is '-' or '.';
    }
}
