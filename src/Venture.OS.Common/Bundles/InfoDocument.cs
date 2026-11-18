using Venture.OS.Interface.Xml;

namespace Venture.OS.Bundles;

/// <summary>
/// A bundle's <c>Contents/Info.xml</c> as an editor holds it, such as Roundhouse's form: its exact text, where each
/// attribute sits in it, and the <see cref="Bundles.Bundle"/> VentureOS reads from it, or why VentureOS refuses it.
/// Edits to <c>&lt;Info&gt;</c>'s attributes change only those attributes in the text, and an edit VentureOS would
/// refuse, such as an identifier that isn't reverse-DNS or a <c>main</c> that isn't there, is refused with the reason.
/// </summary>
public sealed class InfoDocument
{
    /// <summary>Where a bundle's <c>Info.xml</c> is, inside its folder.</summary>
    public const string Path = "Contents/Info.xml";

    private InfoDocument(string text, string root, BundleKind kind, FlatXmlDocument? xml, Bundle? bundle, BundleException? problem)
    {
        Text = text;
        Root = root;
        Kind = kind;
        Xml = xml;
        Bundle = bundle;
        Problem = problem;
    }

    /// <summary>The text, exactly.</summary>
    public string Text { get; }

    /// <summary>The bundle's folder, whose files <c>main</c> and <c>icon</c> must name.</summary>
    public string Root { get; }

    /// <summary>The kind of bundle it describes, from its folder's extension.</summary>
    public BundleKind Kind { get; }

    /// <summary>The elements, with where each sits in <see cref="Text"/>, or <see langword="null"/> when it isn't well-formed XML.</summary>
    public FlatXmlDocument? Xml { get; }

    /// <summary>The bundle VentureOS reads from it, or <see langword="null"/> when it refuses it.</summary>
    public Bundle? Bundle { get; }

    /// <summary>Why VentureOS refuses it, or <see langword="null"/>.</summary>
    public BundleException? Problem { get; }

    /// <summary>Whether its attributes can be edited other than as text: only when VentureOS reads it.</summary>
    public bool IsEditable => Bundle is not null;

    /// <summary>An attribute of <c>&lt;Info&gt;</c>, as written, or <see langword="null"/> when it's absent or can't be read.</summary>
    public string? this[string attribute] => Info()?[attribute];

    /// <summary>The kind of bundle a folder is, by its extension, or <see langword="null"/> for a folder that isn't one.</summary>
    public static BundleKind? KindOf(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var trimmed = folder.TrimEnd('/', '\\');
        foreach (var kind in (BundleKind[])[BundleKind.App, BundleKind.Theme, BundleKind.Library, BundleKind.Wallpaper])
        {
            if (trimmed.EndsWith(Bundle.Extension(kind), StringComparison.OrdinalIgnoreCase))
                return kind;
        }

        return null;
    }

    /// <summary>
    /// Reads a bundle's <c>Info.xml</c> from its text. Text VentureOS refuses still gives a document, with its
    /// <see cref="Problem"/>, so it can be fixed as text.
    /// </summary>
    /// <param name="root">The bundle's folder, as a full path.</param>
    public static InfoDocument Read(string text, string root, BundleKind kind)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrEmpty(root);
        var label = System.IO.Path.GetFileName(root.TrimEnd('/', '\\'));

        FlatXmlDocument xml;
        try
        {
            xml = FlatXml.Parse(text);
        }
        catch (FlatXmlException exception)
        {
            return new InfoDocument(text, root, kind, null, null, new BundleException($"{label}'s {Path}, line {exception.Line}: {exception.Reason}"));
        }

        try
        {
            return new InfoDocument(text, root, kind, xml, Bundle.FromInfo(root.Replace('\\', '/'), xml, kind), null);
        }
        catch (BundleException exception)
        {
            return new InfoDocument(text, root, kind, xml, null, exception);
        }
    }

    /// <summary>Sets one of <c>&lt;Info&gt;</c>'s attributes, in place, as written attributes are.</summary>
    /// <returns>The edited document; this one is unchanged.</returns>
    /// <exception cref="InvalidOperationException">VentureOS refuses the document, so it can only be edited as text.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> isn't an attribute name.</exception>
    /// <exception cref="BundleException">VentureOS would refuse the result; the message says why.</exception>
    public InfoDocument SetAttribute(string name, string value) =>
        Finish(FlatXmlEditor.SetAttribute(Text, Editable(), InfoIndex(), name, value));

    /// <summary>Removes one of <c>&lt;Info&gt;</c>'s attributes, and the space before it.</summary>
    /// <returns>The edited document; this one is unchanged.</returns>
    /// <exception cref="InvalidOperationException">VentureOS refuses the document, so it can only be edited as text.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> isn't an attribute name.</exception>
    /// <exception cref="BundleException">VentureOS would refuse the result, such as one without a name.</exception>
    public InfoDocument RemoveAttribute(string name) =>
        Finish(FlatXmlEditor.RemoveAttribute(Text, Editable(), InfoIndex(), name));

    public override string ToString() => Text;

    private FlatXmlElement? Info() => Xml?.Children.FirstOrDefault(child => child.Name == "Info");

    private FlatXmlDocument Editable()
    {
        if (!IsEditable || Xml is null)
            throw new InvalidOperationException($"This Info.xml can only be edited as text: {Problem?.Message}");
        return Xml;
    }

    // VentureOS reads only documents with exactly one <Info>, so an editable one has it.
    private int InfoIndex()
    {
        var children = Xml!.Children;
        for (var index = 0; index < children.Count; index++)
        {
            if (children[index].Name == "Info")
                return index;
        }

        throw new InvalidOperationException("This Info.xml has no <Info>.");
    }

    private InfoDocument Finish(string text)
    {
        if (ReferenceEquals(text, Text))
            return this;

        var edited = Read(text, Root, Kind);
        return edited.Problem is { } problem ? throw problem : edited;
    }
}
