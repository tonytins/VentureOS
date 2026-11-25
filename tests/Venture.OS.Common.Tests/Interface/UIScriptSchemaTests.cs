using System.Text;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Interface;

/// <summary>
/// <see cref="UIScriptSchema"/> against the reader: everything it lists is read, and everything shipped documents
/// use that the reader reads is listed.
/// </summary>
public sealed class UIScriptSchemaTests
{
    // Something for every kind of reference to name, a menu for menu items to join, and a sidebar for a pane.
    private const string DeclaredSidebar = """<Sidebar name="side" width="80"/>""";

    private const string Declarations = """
          <Image name="img" path="img.png"/>
          <String name="str" table="t" index="0"/>
          <Style name="s" fontSize="12"/>
          <Menu name="m" title="M"/>
          <MenuItem menu="m" name="mi" title="Item"/>
          <List name="l" x="0" y="0" width="10" height="10"/>
          <Sidebar name="side" width="80"/>
          <Pane name="pn" title="P"/>
        """;

    // Attributes that only read with another, or need a value the usual sample isn't.
    private static readonly Dictionary<string, string> Companions = new(StringComparer.Ordinal)
    {
        ["minWidth"] = """ minHeight="1" """,
        ["minHeight"] = """ minWidth="1" """,
        ["minValue"] = "",

        // A step fits within the slider's range, which is 0 to 1 without one.
        ["step"] = """ maxValue="10" """,
    };

    private static readonly Dictionary<string, string> Samples = new(StringComparer.Ordinal)
    {
        ["minValue"] = "0",
        ["maxValue"] = "5",
        ["minWidth"] = "1",
        ["minHeight"] = "1",
    };

    public static TheoryData<string, string> EveryAttribute()
    {
        TheoryData<string, string> rows = [];
        foreach (var element in UIScriptSchema.Elements.Order(StringComparer.Ordinal))
        {
            foreach (var attribute in UIScriptSchema.For(element))
                rows.Add(element, attribute.Name);
        }

        return rows;
    }

    [Theory]
    [MemberData(nameof(EveryAttribute))]
    public void TheReaderReadsEveryListedAttribute(string element, string attribute)
    {
        var info = UIScriptSchema.Find(element, attribute)!;
        var probe = $"""{attribute}="{Sample(info)}"{Companion(element, attribute)}""";

        var text = element switch
        {
            "UIScript" => $"""<UIScript {(attribute == "requires" ? "" : """requires="menus sidebar shelf panes" """)}{probe}><Window width="300" height="200"/>{Declarations}</UIScript>""",
            "Window" => $"""<UIScript requires="menus sidebar shelf panes"><Window width="300" height="200" {(attribute is "width" or "height" ? "" : probe)}/>{Declarations}</UIScript>""",
            "Panel" => $"""<UIScript requires="menus shelf"><Panel {(attribute is "width" ? "" : "width=\"300\" ")}{(attribute is "height" ? "" : "height=\"72\" ")}{(attribute is "edge" ? "" : "edge=\"top\" ")}{probe}/></UIScript>""",
            // An item reads inside a grid, and shows a title when the probe gives it nothing else to show.
            "StatusItem" => $"""<UIScript><Image name="img" path="img.png"/><StatusItem {(attribute == "name" ? "" : """name="s" """)}{(attribute is "title" or "image" ? "" : """title="t" """)}{probe}/></UIScript>""",
            "Item" => $"""<UIScript><Window width="300" height="200"/>{Declarations}<Grid name="g" x="0" y="0" width="100" height="100" itemWidth="20" itemHeight="20"><Item {(attribute is "title" or "string" or "image" or "color" ? "" : """title="t" """)}{probe}/></Grid></UIScript>""",
            // A probed sidebar stands in for the declared one, which panes still need.
            _ => $"""<UIScript requires="menus sidebar shelf panes"><Window width="300" height="200"/>{(element == "Sidebar" ? Declarations.Replace(DeclaredSidebar, "", StringComparison.Ordinal) : Declarations)}<{element} {Required(element, attribute)}{probe}/></UIScript>""",
        };

        var script = UIScript.Parse(text);

        Assert.DoesNotContain(script.IgnoredAttributes, ignored => ignored.Element == element && ignored.Attribute == attribute);
    }

    [Theory]
    [MemberData(nameof(UIDocumentTests.Documents), MemberType = typeof(UIDocumentTests))]
    public void EveryAttributeAShippedDocumentUsesIsListedOrSkippedByTheReader(string file)
    {
        var document = UIDocument.Load(Path.Combine(AppContext.BaseDirectory, file));
        var ignored = document.Script!.IgnoredAttributes.Select(item => (item.Element, item.Attribute)).ToHashSet();

        foreach (var element in document.Xml!.Children.Prepend(document.Xml.Root))
        {
            foreach (var (attribute, _) in element.Attributes)
            {
                Assert.True(UIScriptSchema.Find(element.Name, attribute) is not null || ignored.Contains((element.Name, attribute)),
                    $"<{element.Name}> {attribute} is read but not listed.");
            }
        }
    }

    [Fact]
    public void EveryElementTheReaderKnowsIsListed()
    {
        Assert.Equal(
            ["Button", "FormattedText", "Grid", "Image", "Item", "List", "Menu", "MenuItem", "MenuSeparator", "Pane", "Panel", "ProgressBar", "Region", "Shelf",
                "Sidebar", "Slider", "StatusItem", "String", "Style", "Text", "TextArea", "TextField", "TitleField", "ToolbarItem", "UIScript",
                "Visual", "Window"],
            UIScriptSchema.Elements.Order(StringComparer.Ordinal));
        Assert.All(UIScriptSchema.Controls, control => Assert.NotNull(UIScriptSchema.Find(control, "x")));
        Assert.Empty(UIScriptSchema.For("Hologram"));
    }

    [Fact]
    public void AControlTakesOnlyWhatItsKindAllows()
    {
        Assert.NotNull(UIScriptSchema.Find("TextField", "placeholder"));
        Assert.Null(UIScriptSchema.Find("Text", "placeholder"));
        Assert.NotNull(UIScriptSchema.Find("Button", "image"));
        Assert.Null(UIScriptSchema.Find("List", "image"));
        Assert.NotNull(UIScriptSchema.Find("List", "selectable"));
        Assert.Null(UIScriptSchema.Find("TextArea", "secure"));
    }

    [Theory]
    [InlineData("Text", "selectable")]
    [InlineData("Text", "placeholder")]
    [InlineData("List", "image")]
    [InlineData("TextField", "follows")]
    [InlineData("Button", "minValue")]
    public void WhatAKindDoesntTakeIsRefusedByTheReader(string element, string attribute)
    {
        var text = $"""<UIScript><Window width="300" height="200"/>{Declarations}<{element} {Required(element, attribute)}{attribute}="{(attribute == "minValue" ? "0" : attribute is "selectable" or "follows" ? "true" : attribute == "image" ? "img" : "x")}"/></UIScript>""";

        Assert.Throws<UIScriptException>(() => UIScript.Parse(text.Replace("<UIScript>", """<UIScript requires="menus sidebar shelf panes">""", StringComparison.Ordinal)));
    }

    private static string Required(string element, string except)
    {
        StringBuilder written = new();
        foreach (var info in UIScriptSchema.For(element))
        {
            var needed = info.IsRequired || (UIScriptSchema.Controls.Contains(element) && info.Name is "width" or "height")
                || (element == "Image" && info.Name == "path");
            if (needed && info.Name != except)
                written.Append($"""{info.Name}="{Sample(info)}" """);
        }

        return written.ToString();
    }

    private static string Companion(string element, string attribute) => Companions.GetValueOrDefault(attribute, "");

    private static string Sample(UIAttributeInfo info) => Samples.TryGetValue(info.Name, out var sample) ? sample : info.Kind switch
    {
        UIAttributeKind.Name => "probe",
        UIAttributeKind.Number => "5",
        UIAttributeKind.Integer => "1",
        UIAttributeKind.Boolean => "true",
        UIAttributeKind.Text => "x",
        UIAttributeKind.Choice => info.Choices![0],
        UIAttributeKind.Color => "#FFFFFF",
        UIAttributeKind.FontSize => "12",
        UIAttributeKind.Reference => info.RefersTo switch
        {
            "Style" => "s",
            "String" => "str",
            "Image" => "img",
            "Menu" => "m",
            "Pane" => "pn",
            _ => "l",
        },
        UIAttributeKind.Hotkey => "q",
        UIAttributeKind.Shortcut => "Ctrl+K",
        UIAttributeKind.Features => "menus sidebar shelf panes",
        UIAttributeKind.Path => "p.png",
        _ => throw new ArgumentOutOfRangeException(nameof(info)),
    };
}
