using System.Text;
using Venture.OS.Interface.Xml;

namespace Venture.OS.Interface;

/// <summary>
/// A <c>.uix</c> document as an editor holds it: its exact text, where each element and attribute sits in that text,
/// and what VentureOS reads from it. Edits change the text in place, so everything else in the file (comments,
/// spacing, attribute order, quotes, and entities) stays as it was written, and a document nobody edited is written
/// back byte for byte.
/// </summary>
/// <remarks>
/// A document VentureOS refuses still opens, so its text can be fixed, but it can only be edited as text:
/// <see cref="Problem"/> says why, and <see cref="IsEditable"/> is <see langword="false"/>.
/// </remarks>
public sealed class UIDocument
{
    // Strict UTF-8: text that isn't valid UTF-8 couldn't be written back as it was, so it's refused instead.
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>The root element, <c>&lt;UIScript&gt;</c>, wherever an edit takes an element's index.</summary>
    public const int Root = -1;

    private UIDocument(string text, FlatXmlDocument? xml, UIScript? script, UIScriptException? problem)
    {
        Text = text;
        Xml = xml;
        Script = script;
        Problem = problem;
    }

    /// <summary>The document's text, exactly, including a byte order mark if the file has one.</summary>
    public string Text { get; }

    /// <summary>
    /// The document's elements with where each sits in <see cref="Text"/>, or <see langword="null"/> when the text
    /// isn't well-formed XML.
    /// </summary>
    public FlatXmlDocument? Xml { get; }

    /// <summary>What VentureOS reads from the document, or <see langword="null"/> when it refuses it.</summary>
    public UIScript? Script { get; }

    /// <summary>Why VentureOS refuses the document, with the line, or <see langword="null"/> when it reads it.</summary>
    public UIScriptException? Problem { get; }

    /// <summary>
    /// Whether the document can be edited other than as text: only one VentureOS reads, so an edit never starts from
    /// a document VentureOS would refuse, such as one with an element it doesn't know.
    /// </summary>
    public bool IsEditable => Script is not null;

    /// <summary>Reads a document from its text. Text VentureOS refuses still gives a document, with its <see cref="Problem"/>.</summary>
    public static UIDocument Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        FlatXmlDocument xml;
        try
        {
            xml = FlatXml.Parse(text);
        }
        catch (FlatXmlException exception)
        {
            return new UIDocument(text, null, null, new UIScriptException(exception.Reason, exception.Line));
        }

        try
        {
            return new UIDocument(text, xml, UIScript.Build(xml), null);
        }
        catch (UIScriptException exception)
        {
            return new UIDocument(text, xml, null, exception);
        }
    }

    /// <summary>Reads a UTF-8 file, keeping every byte of it, a byte order mark included.</summary>
    /// <exception cref="IOException">The file couldn't be read.</exception>
    /// <exception cref="InvalidDataException">The file isn't UTF-8, so it couldn't be written back unchanged.</exception>
    public static UIDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var bytes = File.ReadAllBytes(path);
        try
        {
            // GetString keeps a byte order mark as U+FEFF, where reading the file as text would drop it.
            return Read(Utf8.GetString(bytes));
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException($"{path} isn't UTF-8 text, so it can't be opened as a document.", exception);
        }
    }

    /// <summary>Writes the text to a file as UTF-8, byte for byte as <see cref="Load"/> read it if it's unchanged.</summary>
    /// <exception cref="IOException">The file couldn't be written.</exception>
    /// <exception cref="EncoderFallbackException">The text holds a broken surrogate pair, which UTF-8 can't hold.</exception>
    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        File.WriteAllBytes(path, Utf8.GetBytes(Text));
    }

    /// <summary>
    /// Sets an attribute, in place if the element has it, or after its last attribute if not, laid out as the
    /// element's attributes are: on the same line, or each on its own line with the same indentation. Nothing else in
    /// the text changes; a value that already reads as <paramref name="value"/> is left as written.
    /// </summary>
    /// <param name="element">The element's index among the root's children, or <see cref="Root"/>.</param>
    /// <returns>The edited document; this one is unchanged.</returns>
    /// <exception cref="InvalidOperationException">The document can only be edited as text; see <see cref="Problem"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">There's no such element.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> isn't an attribute name.</exception>
    /// <exception cref="UIScriptException">VentureOS would refuse the result, such as a width that isn't a number.</exception>
    public UIDocument SetAttribute(int element, string name, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var (target, source) = Find(element, name);

        for (var index = 0; index < source.Attributes.Count; index++)
        {
            var attribute = source.Attributes[index];
            if (attribute.Name != name)
                continue;

            return target.Attributes[index].Value == value
                ? this
                : Edit(attribute.Value, FlatXml.EncodeValue(value, attribute.Quote));
        }

        // A new attribute follows the last one, as it was set apart from the one before it.
        var (separator, after, quote) = source.Attributes.Count switch
        {
            0 => (" ", source.NameEnd, '"'),
            _ => (Text[Gap(source, source.Attributes.Count - 1)..source.Attributes[^1].Span.Start],
                source.Attributes[^1].Span.End,
                source.Attributes[^1].Quote),
        };
        if (!separator.Contains('\n', StringComparison.Ordinal))
            separator = " ";

        return Edit(new FlatXmlSpan(after, 0), $"{separator}{name}={quote}{FlatXml.EncodeValue(value, quote)}{quote}");
    }

    /// <summary>
    /// Removes an attribute and the space before it, leaving the rest of the text as it was. Removing an attribute
    /// the element doesn't have changes nothing.
    /// </summary>
    /// <param name="element">The element's index among the root's children, or <see cref="Root"/>.</param>
    /// <returns>The edited document; this one is unchanged.</returns>
    /// <exception cref="InvalidOperationException">The document can only be edited as text; see <see cref="Problem"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">There's no such element.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> isn't an attribute name.</exception>
    /// <exception cref="UIScriptException">VentureOS would refuse the result, such as a window without a width.</exception>
    public UIDocument RemoveAttribute(int element, string name)
    {
        var (_, source) = Find(element, name);
        for (var index = 0; index < source.Attributes.Count; index++)
        {
            if (source.Attributes[index].Name != name)
                continue;

            var start = Gap(source, index);
            return Edit(new FlatXmlSpan(start, source.Attributes[index].Span.End - start), "");
        }

        return this;
    }

    /// <summary>
    /// Adds an element, written as <c>&lt;Name a="1"/&gt;</c>, so it becomes the root's child at
    /// <paramref name="index"/>: right after the element before it, on a line of its own with the same indentation
    /// as its neighbours, or on their line when they share one. A comment above the element now after it stays
    /// with that element.
    /// </summary>
    /// <param name="index">Where the element goes among the root's children, from 0 to their count.</param>
    /// <returns>The edited document; this one is unchanged.</returns>
    /// <exception cref="InvalidOperationException">The document can only be edited as text; see <see cref="Problem"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is past the end.</exception>
    /// <exception cref="ArgumentException">A name isn't one, or an attribute is given twice.</exception>
    /// <exception cref="UIScriptException">VentureOS would refuse the result, such as a second window.</exception>
    public UIDocument InsertElement(int index, string name, IReadOnlyList<KeyValuePair<string, string>> attributes)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(attributes);
        var xml = Editable();
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
        return Finish(Insert(Text, xml, index, element.ToString()));
    }

    /// <summary>
    /// Removes the root's child at <paramref name="index"/>; one on a line of its own takes the line. Comments stay
    /// where they are.
    /// </summary>
    /// <returns>The edited document; this one is unchanged.</returns>
    /// <exception cref="InvalidOperationException">The document can only be edited as text; see <see cref="Problem"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">There's no such element.</exception>
    /// <exception cref="UIScriptException">VentureOS would refuse the result, such as one without its window.</exception>
    public UIDocument RemoveElement(int index)
    {
        var xml = Editable();
        CheckChild(xml, index, nameof(index));
        return Finish(Remove(Text, xml, index));
    }

    /// <summary>
    /// Moves the root's child at <paramref name="from"/> so it's at <paramref name="to"/>, written exactly as it
    /// was. Later controls are drawn over earlier ones, so this changes what's in front.
    /// </summary>
    /// <returns>The edited document; this one is unchanged, and it's returned as is when the element doesn't move.</returns>
    /// <exception cref="InvalidOperationException">The document can only be edited as text; see <see cref="Problem"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">There's no such element, or no such place.</exception>
    /// <exception cref="UIScriptException">VentureOS would refuse the result.</exception>
    public UIDocument MoveElement(int from, int to)
    {
        var xml = Editable();
        CheckChild(xml, from, nameof(from));
        CheckChild(xml, to, nameof(to));
        if (from == to)
            return this;

        var element = xml.Children[from].Source!.Span.Of(Text);
        var removed = Remove(Text, xml, from);

        // The document between the two steps can lack something VentureOS needs, so it's only read as XML.
        return Finish(Insert(removed, FlatXml.Parse(removed), to, element));
    }

    public override string ToString() => Text;

    private static void CheckChild(FlatXmlDocument xml, int index, string parameter)
    {
        if (index < 0 || index >= xml.Children.Count)
            throw new ArgumentOutOfRangeException(parameter, index, $"The document has {xml.Children.Count} elements inside its root.");
    }

    private static string Insert(string text, FlatXmlDocument xml, int index, string element)
    {
        var children = xml.Children;
        var root = xml.Root.Source!;
        if (root.CloseStart is null)
            throw new InvalidOperationException("The root is written as <UIScript/>, so it has nowhere to hold an element.");

        // The new element follows the one before it, or the root's start tag, and takes its neighbour's layout.
        var after = index > 0
            ? children[index - 1].Source!.Span.End
            : text.IndexOf('>', root.Attributes.Count > 0 ? root.Attributes[^1].Span.End : root.NameEnd) + 1;
        var neighbour = index < children.Count ? children[index] : children.Count > 0 ? children[^1] : null;

        string separator;
        if (neighbour is null)
            separator = NewLine(text) + LineIndent(text, root.Span.Start, out _) + "  ";
        else
        {
            var indent = LineIndent(text, neighbour.Source!.Span.Start, out var ownLine);
            separator = ownLine ? NewLine(text) + indent : "";
        }

        return string.Concat(text.AsSpan(0, after), separator, element, text.AsSpan(after));
    }

    private static string Remove(string text, FlatXmlDocument xml, int index)
    {
        var span = xml.Children[index].Source!.Span;
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

    private FlatXmlDocument Editable()
    {
        if (!IsEditable || Xml is null)
            throw new InvalidOperationException($"This document can only be edited as text: {Problem?.Message}");
        return Xml;
    }

    /// <summary>Where the space before an attribute starts: the end of the attribute before it, or of the name.</summary>
    private static int Gap(FlatXmlElementSource source, int attribute) =>
        attribute == 0 ? source.NameEnd : source.Attributes[attribute - 1].Span.End;

    private (FlatXmlElement Element, FlatXmlElementSource Source) Find(int element, string attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        var xml = Editable();
        if (!FlatXml.IsName(attribute))
            throw new ArgumentException($"\"{attribute}\" isn't an attribute name.", nameof(attribute));
        if (element != Root)
            CheckChild(xml, element, nameof(element));

        var found = element == Root ? xml.Root : xml.Children[element];
        return (found, found.Source!);
    }

    /// <summary>Replaces one span of the text, and reads the result as VentureOS would, refusing what it refuses.</summary>
    private UIDocument Edit(FlatXmlSpan span, string replacement) =>
        Finish(string.Concat(Text.AsSpan(0, span.Start), replacement, Text.AsSpan(span.End)));

    /// <summary>Reads edited text as VentureOS would, refusing what it refuses.</summary>
    private static UIDocument Finish(string text)
    {
        var edited = Read(text);
        return edited.Problem is { } problem ? throw problem : edited;
    }
}
