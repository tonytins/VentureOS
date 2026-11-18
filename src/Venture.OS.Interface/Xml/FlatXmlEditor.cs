using System.Text;

namespace Venture.OS.Interface.Xml;

/// <summary>
/// Edits a <see cref="FlatXml"/> document's text in place, from where <see cref="FlatXml"/> found each element and
/// attribute: only what's edited changes, and comments, spacing, attribute order, quotes, and entities stay as
/// written. Each edit gives new text; whether a format accepts it is the caller's to check, as
/// <see cref="UIDocument"/> does for UIScript and a bundle's <c>Info.xml</c> document does for bundles.
/// </summary>
/// <remarks>Elements are named by their index among the root's children, or <see cref="Root"/>.</remarks>
public static class FlatXmlEditor
{
    /// <summary>The root element, wherever an edit takes an element's index.</summary>
    public const int Root = -1;

    /// <summary>
    /// Sets an attribute, in place if the element has it, or after its last attribute if not, laid out as the
    /// element's attributes are: on the same line, or each on its own line with the same indentation, in the quotes
    /// of the one before it. A value that already reads as <paramref name="value"/> is left as written.
    /// </summary>
    /// <returns>The edited text, or <paramref name="text"/> itself when nothing changes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">There's no such element.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> isn't an attribute name.</exception>
    public static string SetAttribute(string text, FlatXmlDocument xml, int element, string name, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var (target, source) = Find(xml, element, name);

        for (var index = 0; index < source.Attributes.Count; index++)
        {
            var attribute = source.Attributes[index];
            if (attribute.Name != name)
                continue;

            return target.Attributes[index].Value == value
                ? text
                : Replace(text, attribute.Value, FlatXml.EncodeValue(value, attribute.Quote));
        }

        // A new attribute follows the last one, as it was set apart from the one before it.
        var (separator, after, quote) = source.Attributes.Count switch
        {
            0 => (" ", source.NameEnd, '"'),
            _ => (text[Gap(source, source.Attributes.Count - 1)..source.Attributes[^1].Span.Start],
                source.Attributes[^1].Span.End,
                source.Attributes[^1].Quote),
        };
        if (!separator.Contains('\n', StringComparison.Ordinal))
            separator = " ";

        return Replace(text, new FlatXmlSpan(after, 0), $"{separator}{name}={quote}{FlatXml.EncodeValue(value, quote)}{quote}");
    }

    /// <summary>
    /// Removes an attribute and the space before it, so one on its own line takes the line. Removing one the element
    /// doesn't have changes nothing.
    /// </summary>
    /// <returns>The edited text, or <paramref name="text"/> itself when nothing changes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">There's no such element.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> isn't an attribute name.</exception>
    public static string RemoveAttribute(string text, FlatXmlDocument xml, int element, string name)
    {
        var (_, source) = Find(xml, element, name);
        for (var index = 0; index < source.Attributes.Count; index++)
        {
            if (source.Attributes[index].Name != name)
                continue;

            var start = Gap(source, index);
            return Replace(text, new FlatXmlSpan(start, source.Attributes[index].Span.End - start), "");
        }

        return text;
    }

    /// <summary>
    /// Adds an element, written as <c>&lt;Name a="1"/&gt;</c>, so it becomes the root's child at
    /// <paramref name="index"/>: right after the element before it, on a line of its own with the same indentation
    /// as its neighbours, or on their line when they share one, with the document's own line breaks. A comment
    /// above the element now after it stays with that element.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is past the end.</exception>
    /// <exception cref="ArgumentException">A name isn't one, or an attribute is given twice.</exception>
    /// <exception cref="InvalidOperationException">The root is written as <c>&lt;Root/&gt;</c>, so it holds nothing.</exception>
    public static string InsertElement(
        string text, FlatXmlDocument xml, int index, string name, IReadOnlyList<KeyValuePair<string, string>> attributes)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(attributes);
        if (index < 0 || index > xml.Children.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"An element goes from 0 to {xml.Children.Count}.");
        if (!FlatXml.IsName(name))
            throw new ArgumentException($"\"{name}\" isn't an element name.", nameof(name));

        StringBuilder element = new($"<{name}");
        HashSet<string> seen = [];
        foreach (var (attribute, value) in attributes)
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!FlatXml.IsName(attribute))
                throw new ArgumentException($"\"{attribute}\" isn't an attribute name.", nameof(attributes));
            if (!seen.Add(attribute))
                throw new ArgumentException($"The attribute {attribute} is given twice.", nameof(attributes));
            element.Append($" {attribute}=\"{FlatXml.EncodeValue(value, '"')}\"");
        }

        element.Append("/>");
        return Insert(text, xml, index, element.ToString());
    }

    /// <summary>
    /// Removes the root's child at <paramref name="index"/> with the comments that describe it (those right above it,
    /// with no blank line between); one on a line of its own takes the line.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">There's no such element.</exception>
    public static string RemoveElement(string text, FlatXmlDocument xml, int index)
    {
        ArgumentNullException.ThrowIfNull(xml);
        CheckChild(xml, index, nameof(index));
        return Remove(text, xml, index);
    }

    /// <summary>
    /// Moves the root's child at <paramref name="from"/> so it's at <paramref name="to"/>, written exactly as it was,
    /// with the comments that describe it.
    /// </summary>
    /// <returns>The edited text, or <paramref name="text"/> itself when the element doesn't move.</returns>
    /// <exception cref="ArgumentOutOfRangeException">There's no such element, or no such place.</exception>
    public static string MoveElement(string text, FlatXmlDocument xml, int from, int to)
    {
        ArgumentNullException.ThrowIfNull(xml);
        CheckChild(xml, from, nameof(from));
        CheckChild(xml, to, nameof(to));
        if (from == to)
            return text;

        var element = Block(text, xml, from).Of(text);
        var removed = Remove(text, xml, from);

        // The text between the two steps can lack something a format needs, so it's only read as XML.
        return Insert(removed, FlatXml.Parse(removed), to, element);
    }

    private static void CheckChild(FlatXmlDocument xml, int index, string parameter)
    {
        if (index < 0 || index >= xml.Children.Count)
            throw new ArgumentOutOfRangeException(parameter, index, $"The document has {xml.Children.Count} elements inside its root.");
    }

    private static (FlatXmlElement Element, FlatXmlElementSource Source) Find(FlatXmlDocument xml, int element, string attribute)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(attribute);
        if (!FlatXml.IsName(attribute))
            throw new ArgumentException($"\"{attribute}\" isn't an attribute name.", nameof(attribute));
        if (element != Root)
            CheckChild(xml, element, nameof(element));

        var found = element == Root ? xml.Root : xml.Children[element];
        return (found, found.Source ?? throw new ArgumentException("The document wasn't read from text, so it can't be edited in place.", nameof(xml)));
    }

    private static string Replace(string text, FlatXmlSpan span, string replacement) =>
        string.Concat(text.AsSpan(0, span.Start), replacement, text.AsSpan(span.End));

    private static string Insert(string text, FlatXmlDocument xml, int index, string element)
    {
        var children = xml.Children;
        var root = xml.Root.Source!;
        if (root.CloseStart is null)
            throw new InvalidOperationException($"The root is written as <{xml.Root.Name}/>, so it has nowhere to hold an element.");

        // The new element follows the one before it, or the root's start tag, and takes its neighbour's layout.
        var after = index > 0 ? children[index - 1].Source!.Span.End : ContentStart(text, root);
        var neighbour = index < children.Count ? children[index] : children.Count > 0 ? children[^1] : null;

        string separator;
        if (neighbour is null)
        {
            separator = NewLine(text) + LineIndent(text, root.Span.Start, out _) + "  ";
        }
        else
        {
            var indent = LineIndent(text, neighbour.Source!.Span.Start, out var ownLine);
            separator = ownLine ? NewLine(text) + indent : "";
        }

        return string.Concat(text.AsSpan(0, after), separator, element, text.AsSpan(after));
    }

    private static string Remove(string text, FlatXmlDocument xml, int index)
    {
        var span = Block(text, xml, index);
        var start = span.Start;
        var end = span.End;

        // An element alone on its line takes the line, from its indentation through its line break.
        LineIndent(text, start, out var ownLine);
        var rest = end;
        while (rest < text.Length && text[rest] is ' ' or '\t')
            rest++;
        if (ownLine && (rest == text.Length || text[rest] is '\n' or '\r'))
        {
            start = text.LastIndexOf('\n', Math.Max(0, start - 1)) + 1;
            end = rest;
            if (end < text.Length && text[end] == '\r')
                end++;
            if (end < text.Length && text[end] == '\n')
                end++;
        }

        return string.Concat(text.AsSpan(0, start), text.AsSpan(end));
    }

    /// <summary>
    /// An element with the comments that describe it: those right above it, each starting its own line, with no
    /// blank line between, or one just before it on its own line.
    /// </summary>
    private static FlatXmlSpan Block(string text, FlatXmlDocument xml, int index)
    {
        var span = xml.Children[index].Source!.Span;
        var floor = index > 0 ? xml.Children[index - 1].Source!.Span.End : ContentStart(text, xml.Root.Source!);

        // Between two elements there's only space and comments, as the reader checked, so they're found going forward.
        List<FlatXmlSpan> comments = [];
        for (var at = text.IndexOf("<!--", floor, span.Start - floor, StringComparison.Ordinal); at >= 0;)
        {
            var close = text.IndexOf("-->", at + 4, span.Start - at - 4, StringComparison.Ordinal) + 3;
            comments.Add(new(at, close - at));
            at = close >= span.Start ? -1 : text.IndexOf("<!--", close, span.Start - close, StringComparison.Ordinal);
        }

        var start = span.Start;
        for (var before = comments.Count - 1; before >= 0; before--)
        {
            var comment = comments[before];
            var gap = text.AsSpan(comment.End, start - comment.End);
            var breaks = gap.Count('\n');
            LineIndent(text, comment.Start, out var ownLine);
            if (breaks > 1 || (breaks == 1 && !ownLine))
                break;
            start = comment.Start;
        }

        return new(start, span.End - start);
    }

    /// <summary>Where the root's children begin: just after its start tag.</summary>
    private static int ContentStart(string text, FlatXmlElementSource root) =>
        text.IndexOf('>', root.Attributes.Count > 0 ? root.Attributes[^1].Span.End : root.NameEnd) + 1;

    /// <summary>The spaces and tabs before <paramref name="position"/> on its line, and whether nothing else is.</summary>
    private static string LineIndent(string text, int position, out bool ownLine)
    {
        var start = position;
        while (start > 0 && text[start - 1] is ' ' or '\t')
            start--;
        ownLine = start == 0 || text[start - 1] is '\n' or '\r';
        return text[start..position];
    }

    /// <summary>The document's own line break: Windows's when it uses them, otherwise a line feed.</summary>
    private static string NewLine(string text) => text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    /// <summary>Where the space before an attribute starts: the end of the attribute before it, or of the name.</summary>
    private static int Gap(FlatXmlElementSource source, int attribute) =>
        attribute == 0 ? source.NameEnd : source.Attributes[attribute - 1].Span.End;
}
