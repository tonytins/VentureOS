using System.Text;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Interface;

/// <summary>
/// <see cref="UIDocument"/>, the document an editor holds: the exact text, where everything sits in it, and what
/// VentureOS reads from it.
/// </summary>
public sealed class UIDocumentTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"uidocument-{Guid.NewGuid():N}");

    public UIDocumentTests() => Directory.CreateDirectory(folder);

    public void Dispose() => Directory.Delete(folder, recursive: true);

    /// <summary>Hudini's examples and every <c>.uix</c> VentureOS ships, as copied beside the tests.</summary>
    public static TheoryData<string> Documents() => [.. DocumentFiles()];

    [Fact]
    public void FindsHudinisExamplesAndEveryShippedDocument()
    {
        var documents = DocumentFiles();

        var hudini = documents.Count(file => file.StartsWith(Path.Combine("Interface", "Fixtures", "Hudini"), StringComparison.Ordinal));
        Assert.Equal(14, hudini);
        Assert.True(documents.Count - hudini >= 8);
    }

    [Theory]
    [MemberData(nameof(Documents))]
    public void AnUneditedDocumentIsWrittenBackByteForByte(string file)
    {
        var original = Path.Combine(AppContext.BaseDirectory, file);
        var copy = Path.Combine(folder, Path.GetFileName(file));

        var document = UIDocument.Load(original);
        document.Save(copy);

        Assert.True(document.IsEditable, document.Problem?.Message);
        Assert.Equal(File.ReadAllBytes(original), File.ReadAllBytes(copy));
    }

    [Theory]
    [MemberData(nameof(Documents))]
    public void EveryElementAndAttributeIsFoundWhereItsWritten(string file)
    {
        var document = UIDocument.Load(Path.Combine(AppContext.BaseDirectory, file));
        var text = document.Text;
        var root = document.Xml!.Root.Source!;

        var previousEnd = root.NameEnd;
        foreach (var element in document.Xml.Children)
        {
            var source = element.Source!;

            // Children follow one another inside the root, without overlapping.
            Assert.True(source.Span.Start >= previousEnd && source.Span.End <= root.CloseStart);
            previousEnd = source.Span.End;
            Assert.Equal($"<{element.Name}", text[source.Span.Start..source.NameEnd]);
            Assert.Equal('>', text[source.Span.End - 1]);

            Assert.Equal(element.Attributes.Count, source.Attributes.Count);
            for (var index = 0; index < source.Attributes.Count; index++)
            {
                var attribute = source.Attributes[index];
                Assert.Equal(element.Attributes[index].Key, attribute.Name);
                Assert.StartsWith(attribute.Name, attribute.Span.Of(text), StringComparison.Ordinal);
                Assert.Equal(attribute.Quote, text[attribute.Value.Start - 1]);
                Assert.Equal(attribute.Quote, text[attribute.Value.End]);
                Assert.Equal(attribute.Value.End + 1, attribute.Span.End);
            }
        }
    }

    [Fact]
    public void AByteOrderMarkAndWindowsLineEndingsSurviveTheTrip()
    {
        var path = Path.Combine(folder, "bom.uix");
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("<UIScript>\r\n  <Window width=\"10\" height=\"10\"/>\r\n</UIScript>\r\n")];
        File.WriteAllBytes(path, bytes);

        var document = UIDocument.Load(path);
        document.Save(path);

        Assert.True(document.IsEditable, document.Problem?.Message);
        Assert.Equal('﻿', document.Text[0]);
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void AFileThatIsntUtf8IsRefusedRatherThanChanged()
    {
        var path = Path.Combine(folder, "latin1.uix");
        File.WriteAllBytes(path, [.. Encoding.ASCII.GetBytes("<UIScript><Window width=\"1\" height=\"1\" title=\""), 0xE9, .. Encoding.ASCII.GetBytes("\"/></UIScript>")]);

        var exception = Assert.Throws<InvalidDataException>(() => UIDocument.Load(path));

        Assert.Contains("isn't UTF-8", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownElementOpensForTextEditingOnlyAndSaysWhy()
    {
        const string text = """
            <UIScript>
              <Window width="100" height="100"/>
              <Hologram name="h"/>
            </UIScript>
            """;

        var document = UIDocument.Read(text);

        Assert.False(document.IsEditable);
        Assert.Null(document.Script);
        Assert.NotNull(document.Xml);
        Assert.Equal(3, document.Problem!.Line);
        Assert.Contains("Hologram", document.Problem.Message, StringComparison.Ordinal);
        Assert.Equal(text, document.Text);
    }

    [Fact]
    public void TextThatIsntXmlOpensWithTheLineThatsWrong()
    {
        var document = UIDocument.Read("<UIScript>\n  <Window width=\"1\"\n");

        Assert.False(document.IsEditable);
        Assert.Null(document.Xml);
        Assert.NotNull(document.Problem!.Line);
    }

    [Fact]
    public void ReadsWhatUIScriptReads()
    {
        var document = UIDocument.Load(Path.Combine(AppContext.BaseDirectory, "user", "apps", "Calculator.app", "Contents", "Resources", "main.uix"));

        Assert.NotNull(document.Script!.Control("equals"));
        Assert.Equal(document.Text, document.ToString());
    }

    private static List<string> DocumentFiles()
    {
        var root = AppContext.BaseDirectory;
        List<string> files = [.. Directory.EnumerateFiles(Path.Combine(root, "Interface", "Fixtures", "Hudini"), "*.uix", SearchOption.AllDirectories)];
        foreach (var top in new[] { "system", "user" })
            files.AddRange(Directory.EnumerateFiles(Path.Combine(root, top), "*.uix", SearchOption.AllDirectories));

        return [.. files.Select(file => Path.GetRelativePath(root, file)).Order(StringComparer.Ordinal)];
    }
}
