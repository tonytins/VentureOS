using System.Text;

namespace Venture.OS.Interface.Xml;

/// <summary>
/// Edits a <see cref="FlatXml"/> document's text in place, from where <see cref="FlatXml"/> found each element and
/// attribute: only what's edited changes, and comments, spacing, attribute order, quotes, and entities stay as
/// written. Each edit gives new text; whether a format accepts it is the caller's to check, as
/// <see cref="UIDocument"/> does for UIScript and a bundle's <c>Info.xml</c> document does for bundles.
/// </summary>
/// <remarks>
/// Elements are named by their index in <see cref="FlatXmlDocument.Children"/>, which is document order with each
/// element before those inside it, or <see cref="Root"/>. An element is removed or moved with everything inside it.
/// </remarks>
public static class FlatXmlEditor
{
    /// <summary>The root element, wherever an edit takes an element's index.</summary>
    public const int Root = FlatXmlDocument.RootIndex;

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
    /// Adds an element, written as <c>&lt;Name a="1"/&gt;</c>, inside <paramref name="parent"/> so that its index
    /// becomes <paramref name="index"/>: right after the element before it, on a line of its own with the same
    /// indentation as its neighbours, or on their line when they share one, with the document's own line breaks. A
    /// comment above the element now after it stays with that element. A parent written as <c>&lt;Name/&gt;</c> is
    /// opened up to hold it, with its closing tag on a line of its own.
    /// </summary>
    /// <param name="index">
    /// Where it goes: the index of one of <paramref name="parent"/>'s elements, which it goes before, or the index
    /// just past the parent's last element (<see cref="FlatXmlDocument.End"/>), to go after them all.
    /// </param>
    /// <param name="parent">The element it goes inside, or <see cref="Root"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">There's no such parent, or <paramref name="index"/> isn't a place inside it.</exception>
    /// <exception cref="ArgumentException">A name isn't one, or an attribute is given twice.</exception>
    /// <exception cref="InvalidOperationException">The root is written as <c>&lt;Root/&gt;</c>, so it holds nothing.</exception>
    public static string InsertElement(
        string text, FlatXmlDocument xml, int index, string name, IReadOnlyList<KeyValuePair<string, string>> attributes,
        int parent = Root)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(attributes);
        if (parent != Root)
            CheckChild(xml, parent, nameof(parent));
        var places = xml.ChildrenOf(parent);
        if (index != xml.End(parent) && Array.IndexOf(places, index) < 0)
            throw new ArgumentOutOfRangeException(nameof(index), index, parent == Root
                ? $"An element goes before one directly inside the root, or at {xml.End(parent)}, after them all."
                : $"An element inside element {parent} goes before one directly inside it, or at {xml.End(parent)}, after them all.");
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
        return Insert(text, xml, parent, index, element.ToString());
    }

    /// <summary>
    /// Removes the element at <paramref name="index"/>, everything inside it, and the comments that describe it (those
    /// right above it, with no blank line between); one on a line of its own takes the line.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">There's no such element.</exception>
    public static string RemoveElement(string text, FlatXmlDocument xml, int index)
    {
        ArgumentNullException.ThrowIfNull(xml);
        CheckChild(xml, index, nameof(index));
        return Remove(text, xml, index);
    }

    /// <summary>
    /// Moves the element at <paramref name="from"/> into the place of <paramref name="to"/>, another element inside
    /// the same parent, written exactly as it was, with everything inside it and the comments that describe it.
    /// Moving back puts it before <paramref name="to"/>, and moving forward after it, so its neighbours keep their
    /// order, as they would in a list.
    /// </summary>
    /// <returns>The edited text, or <paramref name="text"/> itself when the element doesn't move.</returns>
    /// <exception cref="ArgumentOutOfRangeException">There's no such element, or the two aren't inside the same parent.</exception>
    public static string MoveElement(string text, FlatXmlDocument xml, int from, int to)
    {
        ArgumentNullException.ThrowIfNull(xml);
        CheckChild(xml, from, nameof(from));
        CheckChild(xml, to, nameof(to));
        if (from == to)
            return text;
        var parent = xml.Children[from].Parent;
        if (xml.Children[to].Parent != parent)
            throw new ArgumentOutOfRangeException(nameof(to), to, $"Element {to} isn't inside the same element as {from}, so it can't take its place.");

        var place = Array.IndexOf(xml.ChildrenOf(parent), to);
        var element = Block(text, xml, from).Of(text);
        var removed = Remove(text, xml, from);

        // The text between the two steps can lack something a format needs, so it's only read as XML. The parent
        // comes before the element, so its index is the same in both.
        var between = FlatXml.Parse(removed, allowNesting: true);
        var siblings = between.ChildrenOf(parent);
        var index = place < siblings.Length ? siblings[place] : between.End(parent);
        return Insert(removed, between, parent, index, element);
    }

    /// <summary>
    /// Moves the element at <paramref name="element"/>, with everything inside it and the comments that describe it,
    /// into <paramref name="parent"/>, after everything already there. Each of its lines is indented as the parent's
    /// children are, so a block moved a level deeper stays tidy.
    /// </summary>
    /// <returns>The edited text, and the element's index in it.</returns>
    /// <exception cref="ArgumentOutOfRangeException">There's no such element or parent, or the parent is the element or inside it.</exception>
    /// <exception cref="InvalidOperationException">The root is written as <c>&lt;Root/&gt;</c>, so it holds nothing.</exception>
    public static (string Text, int Element) MoveInto(string text, FlatXmlDocument xml, int element, int parent)
    {
        ArgumentNullException.ThrowIfNull(xml);
        CheckChild(xml, element, nameof(element));
        if (parent != Root)
            CheckChild(xml, parent, nameof(parent));
        var end = xml.End(element);
        if (parent >= element && parent < end)
            throw new ArgumentOutOfRangeException(nameof(parent), parent, $"Element {element} can't go inside itself.");

        // Removing the element shifts everything after it back by its size, its parent included when it's later.
        var block = Block(text, xml, element);
        var indent = LineIndent(text, block.Start, out _);
        var removed = Remove(text, xml, element);
        var between = FlatXml.Parse(removed, allowNesting: true);
        var holder = parent > element ? parent - (end - element) : parent;
        var index = between.End(holder);
        return (Insert(removed, between, holder, index, block.Of(text), indent), index);
    }

    /// <summary>
    /// Moves the element at <paramref name="element"/>, with everything inside it and the comments that describe it,
    /// out of its parent, to just after it, indented as the parent is.
    /// </summary>
    /// <returns>The edited text, and the element's index in it.</returns>
    /// <exception cref="ArgumentOutOfRangeException">There's no such element, or it's directly inside the root.</exception>
    public static (string Text, int Element) MoveOut(string text, FlatXmlDocument xml, int element)
    {
        ArgumentNullException.ThrowIfNull(xml);
        CheckChild(xml, element, nameof(element));
        var parent = xml.Children[element].Parent;
        if (parent == Root)
            throw new ArgumentOutOfRangeException(nameof(element), element, $"Element {element} is directly inside the root, so it has nothing to move out of.");

        var block = Block(text, xml, element);
        var indent = LineIndent(text, block.Start, out _);
        var removed = Remove(text, xml, element);

        // Its parent comes before it, so it keeps its index.
        var between = FlatXml.Parse(removed, allowNesting: true);
        var index = between.End(parent);
        return (Insert(removed, between, between.Children[parent].Parent, index, block.Of(text), indent), index);
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

    /// <summary>
    /// Puts <paramref name="element"/>'s text inside <paramref name="parent"/>, before <paramref name="index"/>. When
    /// the text was moved from a line indented by <paramref name="movedFrom"/>, each of its lines is indented afresh
    /// for where it lands.
    /// </summary>
    private static string Insert(string text, FlatXmlDocument xml, int parent, int index, string element, string? movedFrom = null)
    {
        var holder = parent == Root ? xml.Root : xml.Children[parent];
        var source = holder.Source!;
        if (source.CloseStart is null)
        {
            if (parent == Root)
                throw new InvalidOperationException($"The root is written as <{xml.Root.Name}/>, so it has nowhere to hold an element.");
            return Open(text, holder, element, movedFrom);
        }

        // The new element follows the sibling before it, or the parent's start tag, and takes its neighbour's layout.
        var siblings = xml.ChildrenOf(parent);
        var place = index == xml.End(parent) ? siblings.Length : Array.IndexOf(siblings, index);
        var after = place > 0 ? xml.Children[siblings[place - 1]].Source!.Span.End : ContentStart(text, source);
        var neighbour = place < siblings.Length ? xml.Children[siblings[place]]
            : siblings.Length > 0 ? xml.Children[siblings[^1]] : null;

        string separator;
        string? landing;
        var closing = "";
        if (neighbour is null)
        {
            var indent = LineIndent(text, source.Span.Start, out _);
            landing = indent + "  ";
            separator = NewLine(text) + landing;

            // A parent closed on its own line keeps it; one written as <A></A> gets one, so its closing tag lines up.
            if (!text.AsSpan(after, source.CloseStart.Value - after).Contains('\n'))
                closing = NewLine(text) + indent;
        }
        else
        {
            var indent = LineIndent(text, neighbour.Source!.Span.Start, out var ownLine);
            landing = ownLine ? indent : null;
            separator = ownLine ? NewLine(text) + indent : "";
        }

        if (movedFrom is not null && landing is not null)
            element = Reindent(element, movedFrom, landing);
        return string.Concat(text[..after], separator, element, closing, text[after..]);
    }

    /// <summary>
    /// Opens up an element written as <c>&lt;Name/&gt;</c> to hold <paramref name="element"/>, which goes on a line
    /// of its own, indented one step further, with the closing tag on the next line.
    /// </summary>
    private static string Open(string text, FlatXmlElement holder, string element, string? movedFrom = null)
    {
        var source = holder.Source!;
        var slash = source.Span.End - 2;

        // Spaces before "/>" on the same line would be left before ">", so they go.
        var tagEnd = slash;
        while (tagEnd > source.NameEnd && text[tagEnd - 1] is ' ' or '\t')
            tagEnd--;
        var indent = LineIndent(text, source.Span.Start, out _);
        if (movedFrom is not null)
            element = Reindent(element, movedFrom, indent + "  ");
        var newLine = NewLine(text);
        return string.Concat(
            text.AsSpan(0, tagEnd),
            $">{newLine}{indent}  {element}{newLine}{indent}</{holder.Name}>",
            text.AsSpan(source.Span.End));
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

        // Comments can only be found back to the sibling before, or the parent's start tag.
        var parent = xml.Children[index].Parent;
        var siblings = xml.ChildrenOf(parent);
        var place = Array.IndexOf(siblings, index);
        var floor = place > 0
            ? xml.Children[siblings[place - 1]].Source!.Span.End
            : ContentStart(text, (parent == Root ? xml.Root : xml.Children[parent]).Source!);

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

    /// <summary>Where an element's children begin: just after its start tag.</summary>
    private static int ContentStart(string text, FlatXmlElementSource element) =>
        text.IndexOf('>', element.Attributes.Count > 0 ? element.Attributes[^1].Span.End : element.NameEnd) + 1;

    /// <summary>
    /// A moved block's lines after its first, which begin with <paramref name="from"/>, begin with
    /// <paramref name="to"/> instead, keeping any deeper indentation; the first line's comes from where it lands.
    /// </summary>
    private static string Reindent(string block, string from, string to)
    {
        if (from == to)
            return block;

        StringBuilder moved = new(block.Length);
        var lines = block.Split('\n');
        for (var line = 0; line < lines.Length; line++)
        {
            if (line > 0)
                moved.Append('\n');
            moved.Append(line > 0 && lines[line].StartsWith(from, StringComparison.Ordinal)
                ? to + lines[line][from.Length..]
                : lines[line]);
        }

        return moved.ToString();
    }

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
