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
    public const int Root = FlatXmlEditor.Root;

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
    /// isn't well-formed XML. Its <see cref="FlatXmlDocument.Children"/> are every element in document order, each
    /// before those inside it, and that order is what every edit's element index counts.
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
            xml = FlatXml.Parse(text, allowNesting: true);
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
            throw new InvalidDataException($"{path} isn't UTF-8 text.", exception);
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
    /// <param name="element">The element's index in <see cref="Xml"/>'s children, or <see cref="Root"/>.</param>
    /// <returns>The edited document; this one is unchanged.</returns>
    /// <exception cref="InvalidOperationException">The document can only be edited as text; see <see cref="Problem"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">There's no such element.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> isn't an attribute name.</exception>
    /// <exception cref="UIScriptException">VentureOS would refuse the result, such as a width that isn't a number.</exception>
    public UIDocument SetAttribute(int element, string name, string value) =>
        Finish(FlatXmlEditor.SetAttribute(Text, Editable(), element, name, value));

    /// <summary>
    /// Removes an attribute and the space before it, leaving the rest of the text as it was. Removing an attribute
    /// the element doesn't have changes nothing.
    /// </summary>
    /// <param name="element">The element's index in <see cref="Xml"/>'s children, or <see cref="Root"/>.</param>
    /// <returns>The edited document; this one is unchanged.</returns>
    /// <exception cref="InvalidOperationException">The document can only be edited as text; see <see cref="Problem"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">There's no such element.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> isn't an attribute name.</exception>
    /// <exception cref="UIScriptException">VentureOS would refuse the result, such as a window without a width.</exception>
    public UIDocument RemoveAttribute(int element, string name) =>
        Finish(FlatXmlEditor.RemoveAttribute(Text, Editable(), element, name));

    /// <summary>
    /// Adds an element, written as <c>&lt;Name a="1"/&gt;</c>, inside <paramref name="parent"/> so that its index
    /// becomes <paramref name="index"/>: right after the element before it, on a line of its own with the same
    /// indentation as its neighbours, or on their line when they share one. A comment above the element now after it
    /// stays with that element.
    /// </summary>
    /// <param name="index">
    /// Where the element goes: before one of <paramref name="parent"/>'s elements, by its index, or at
    /// <see cref="FlatXmlDocument.End"/> of the parent, after them all. Directly inside the root, that's from 0 to
    /// the count of elements in a document that doesn't nest.
    /// </param>
    /// <param name="parent">The element it goes inside, or <see cref="Root"/>.</param>
    /// <returns>The edited document; this one is unchanged.</returns>
    /// <exception cref="InvalidOperationException">The document can only be edited as text; see <see cref="Problem"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">There's no such parent, or <paramref name="index"/> isn't a place inside it.</exception>
    /// <exception cref="ArgumentException">A name isn't one, or an attribute is given twice.</exception>
    /// <exception cref="UIScriptException">VentureOS would refuse the result, such as a second window.</exception>
    public UIDocument InsertElement(int index, string name, IReadOnlyList<KeyValuePair<string, string>> attributes, int parent = Root) =>
        Finish(FlatXmlEditor.InsertElement(Text, Editable(), index, name, attributes, parent));

    /// <summary>
    /// Removes the element at <paramref name="index"/>, everything inside it, and the comments that describe it (those
    /// right above it, with no blank line between); one on a line of its own takes the line.
    /// </summary>
    /// <returns>The edited document; this one is unchanged.</returns>
    /// <exception cref="InvalidOperationException">The document can only be edited as text; see <see cref="Problem"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">There's no such element.</exception>
    /// <exception cref="UIScriptException">VentureOS would refuse the result, such as one without its window.</exception>
    public UIDocument RemoveElement(int index) => Finish(FlatXmlEditor.RemoveElement(Text, Editable(), index));

    /// <summary>
    /// Moves the element at <paramref name="from"/> into the place of <paramref name="to"/>, inside the same
    /// parent, written exactly as it was, with everything inside it and the comments that describe it (those right
    /// above it, with no blank line between). Later controls are drawn over earlier ones, so this changes what's in
    /// front.
    /// </summary>
    /// <returns>The edited document; this one is unchanged, and it's returned as is when the element doesn't move.</returns>
    /// <exception cref="InvalidOperationException">The document can only be edited as text; see <see cref="Problem"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">There's no such element, or the two aren't inside the same parent.</exception>
    /// <exception cref="UIScriptException">VentureOS would refuse the result.</exception>
    public UIDocument MoveElement(int from, int to) => Finish(FlatXmlEditor.MoveElement(Text, Editable(), from, to));

    /// <summary>
    /// Moves the element at <paramref name="element"/>, with everything inside it, into <paramref name="parent"/>,
    /// after everything already there, such as an item into a menu or a control into a pane. Its lines are indented
    /// as the parent's children are, and a <c>menu</c> or <c>pane</c> that named the parent from outside goes.
    /// </summary>
    /// <returns>The edited document, and the element's index in it; this one is unchanged.</returns>
    /// <exception cref="InvalidOperationException">The document can only be edited as text; see <see cref="Problem"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">There's no such element or parent, or the parent is inside the element.</exception>
    /// <exception cref="UIScriptException">VentureOS would refuse the result, such as a pane inside a pane.</exception>
    public (UIDocument Document, int Element) MoveInto(int element, int parent)
    {
        var xml = Editable();
        var text = Text;

        // Inside its menu or pane, an element doesn't also name one from outside, so that attribute goes first. The
        // text between the two steps is only read as XML, since it can lack something UIScript needs.
        if (element >= 0 && element < xml.Children.Count && parent >= 0 && parent < xml.Children.Count)
        {
            var naming = xml.Children[parent].Name switch
            {
                "Menu" => "menu",
                "Pane" => "pane",
                _ => null,
            };
            if (naming is not null && xml.Children[element][naming] is not null)
            {
                text = FlatXmlEditor.RemoveAttribute(text, xml, element, naming);
                xml = FlatXml.Parse(text, allowNesting: true);
            }
        }

        var (moved, index) = FlatXmlEditor.MoveInto(text, xml, element, parent);
        return (Finish(moved), index);
    }

    /// <summary>
    /// Moves the element at <paramref name="element"/>, with everything inside it, out of its parent to just after
    /// it, such as a control out of a pane.
    /// </summary>
    /// <returns>The edited document, and the element's index in it; this one is unchanged.</returns>
    /// <exception cref="InvalidOperationException">The document can only be edited as text; see <see cref="Problem"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">There's no such element, or it's directly inside the root.</exception>
    /// <exception cref="UIScriptException">VentureOS would refuse the result, such as a menu item outside any menu.</exception>
    public (UIDocument Document, int Element) MoveOut(int element)
    {
        var (text, moved) = FlatXmlEditor.MoveOut(Text, Editable(), element);
        return (Finish(text), moved);
    }

    public override string ToString() => Text;

    private FlatXmlDocument Editable()
    {
        if (!IsEditable || Xml is null)
            throw new InvalidOperationException($"This document can only be edited as text: {Problem?.Message}");
        return Xml;
    }

    /// <summary>
    /// Reads edited text as VentureOS would, refusing what it refuses; text the edit left as it was gives this
    /// document back.
    /// </summary>
    private UIDocument Finish(string text)
    {
        if (ReferenceEquals(text, Text))
            return this;

        var edited = Read(text);
        return edited.Problem is { } problem ? throw problem : edited;
    }
}
