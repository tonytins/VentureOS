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

    public override string ToString() => Text;
}
