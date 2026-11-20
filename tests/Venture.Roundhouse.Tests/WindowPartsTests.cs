using Venture.OS.Desktop;
using Venture.OS.Interface;
using Venture.Roundhouse.Designer;
using Venture.Roundhouse.Drawing;

namespace Venture.Roundhouse.Tests;

/// <summary>
/// The window's parts beyond its controls: the title bar's items and title field, menus, and the sidebar, added from
/// the library, found on the canvas, and resized.
/// </summary>
[Collection(nameof(PaletteCollection))]
public sealed class WindowPartsTests : IDisposable
{
    private const string Plain = """
        <UIScript version="1">
          <Window width="300" height="200"/>
          <Button name="ok" x="10" y="10" width="60" height="24" text="OK"/>
        </UIScript>
        """;

    private readonly SkiaFonts fonts = SkiaFonts.LoadShipped();

    public void Dispose()
    {
        fonts.Dispose();
        DesktopAppearance.Use(DesktopLook.Standard);
    }

    [Fact]
    public void AMenuAddsNoRequires()
    {
        var (once, menu) = DesignEdits.Add(UIDocument.Read(Plain), "Menu", null);
        var (twice, _) = DesignEdits.Add(once, "Menu", null);

        Assert.DoesNotContain("requires", twice.Text, StringComparison.Ordinal);
        Assert.Equal("""<Menu name="menu" title="Menu"/>""", once.Xml!.Children[menu].Source!.Span.Of(once.Text));
        Assert.Equal(["menu", "menu2"], twice.Script!.Menus.Select(item => item.Name));
    }

    [Fact]
    public void AShelfAddsNoRequires()
    {
        var (edited, shelf) = DesignEdits.Add(UIDocument.Read(Plain), "Shelf", null);

        Assert.DoesNotContain("requires", edited.Text, StringComparison.Ordinal);
        Assert.Equal(UIControlType.Shelf, edited.Script!.Controls.Single(control => control.Name == "shelf").Type);
        Assert.Equal("Shelf", edited.Xml!.Children[shelf].Name);
    }

    [Fact]
    public void APanelIsDrawnAsDesignedWithSampleIcons()
    {
        UIView view = new(UIScript.Parse("""
            <UIScript version="1" requires="shelf">
              <Panel width="400" height="72" edge="bottom"/>
              <Shelf name="apps" x="0" y="0" width="400" height="72"/>
            </UIScript>
            """));

        using var painter = RenderCommand.Draw(view, "dock.uix", fonts, out var window);

        Assert.True(window.IsPanel);
        Assert.Equal(new UIRect(24, 24, 400, 72), window.Bounds);
        Assert.Equal(4, view.Shelf("apps")!.Items.Count);
    }

    [Fact]
    public void ARetiredRequiresIsLeftAsWritten()
    {
        var document = UIDocument.Read(Plain.Replace("""version="1">""", """version="1" requires="menus">""", StringComparison.Ordinal));

        var (edited, _) = DesignEdits.Add(document, "Sidebar", null);

        Assert.Contains("""requires="menus">""", edited.Text, StringComparison.Ordinal);
        Assert.Equal(160, edited.Script!.Sidebar!.Width);
    }

    [Fact]
    public void AMenuItemJoinsTheSelectedMenuAfterItsLastEntry()
    {
        var (withFile, file) = DesignEdits.Add(UIDocument.Read(Plain), "Menu", null);
        var (withView, _) = DesignEdits.Add(withFile, "Menu", null);
        var (withItem, item) = DesignEdits.Add(withView, "MenuItem", file);
        var (withSeparator, separator) = DesignEdits.Add(withItem, "MenuSeparator", item);

        Assert.Equal(file + 1, item);
        Assert.Equal(item + 1, separator);
        Assert.Equal("menu", withSeparator.Xml!.Children[separator]["menu"]);
        Assert.Equal(2, withSeparator.Script!.Menus[0].Entries.Count);
        Assert.Empty(withSeparator.Script.Menus[1].Entries);
    }

    [Fact]
    public void AMenuItemWithoutASelectedMenuJoinsTheLastAndWithoutAnyIsRefused()
    {
        var (withMenus, _) = DesignEdits.Add(UIDocument.Read(Plain), "Menu", null);
        var (two, _) = DesignEdits.Add(withMenus, "Menu", null);

        var (edited, item) = DesignEdits.Add(two, "MenuItem", null);

        Assert.Equal("menu2", edited.Xml!.Children[item]["menu"]);
        Assert.Contains("Add a menu first", Assert.Throws<ArgumentException>(() => DesignEdits.Add(UIDocument.Read(Plain), "MenuItem", null)).Message);
    }

    [Fact]
    public void TheTitleBarsPartsAreAddedAndASecondTitleFieldIsRefused()
    {
        var (withItem, _) = DesignEdits.Add(UIDocument.Read(Plain), "ToolbarItem", null);
        var (withField, _) = DesignEdits.Add(withItem, "TitleField", null);

        Assert.Equal("Item", withField.Script!.ToolbarItems[0].Title);
        Assert.Equal("title", withField.Script.TitleField!.Name);
        Assert.Throws<UIScriptException>(() => DesignEdits.Add(withField, "TitleField", null));
    }

    [Fact]
    public void ToolbarItemsMoveAlongTheBarAmongThemselves()
    {
        var (one, first) = DesignEdits.Add(UIDocument.Read(Plain), "ToolbarItem", null);
        var (two, _) = DesignEdits.Add(one, "ToolbarItem", null);

        var (moved, at) = DesignEdits.BringForward(two, first);

        Assert.Equal(["item2", "item"], moved.Script!.ToolbarItems.Select(item => item.Name));
        Assert.Equal(first + 1, at);
    }

    [Fact]
    public void TheMapFindsTheTitleBarsPartsAndTheSidebar()
    {
        var document = UIDocument.Read("""
            <UIScript requires="sidebar">
              <Window width="300" height="200"/>
              <Sidebar name="places" width="100"/>
              <ToolbarItem name="back" title="Back" placement="start"/>
              <TitleField name="where"/>
            </UIScript>
            """);
        var (map, window) = Map(document);

        var item = window.ToolbarItemBounds(0);
        Assert.Equal(2, map.ElementAt(item.X + 2, item.Y + 2));
        var title = window.TitleFieldBounds;
        Assert.Equal(3, map.ElementAt(title.X + (title.Width / 2), title.Y + (title.Height / 2)));
        Assert.Equal(1, map.ElementAt(window.X + 20, window.Y + window.BarHeight + 20));

        // The bar around them still selects the window.
        Assert.Equal(0, map.ElementAt(window.X + window.Width - 4, window.Y + 4));
    }

    [Fact]
    public void TheWindowAndSidebarOnlyGrowAndShrinkByTheirOwnHandles()
    {
        Assert.Equal([Handle.Right, Handle.BottomRight, Handle.Bottom], FrameEdit.HandlesFor("Window"));
        Assert.Equal([Handle.Right, Handle.BottomRight, Handle.Bottom], FrameEdit.HandlesFor("Panel"));
        Assert.Equal([Handle.Right], FrameEdit.HandlesFor("Sidebar"));
        Assert.Empty(FrameEdit.HandlesFor("Style"));

        var window = FrameEdit.Apply(UIDocument.Read(Plain), 0, new UIRect(0, 0, 300, 200), new FrameChange(5, 5, 20, 10));
        Assert.Contains("""<Window width="320" height="210"/>""", window.Text, StringComparison.Ordinal);

        var (withSidebar, sidebar) = DesignEdits.Add(UIDocument.Read(Plain), "Sidebar", null);
        var wider = FrameEdit.Apply(withSidebar, sidebar, default, new FrameChange(0, 0, 40, 30));
        Assert.Contains("""<Sidebar name="sidebar" width="200"/>""", wider.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AHandleIsOnlyFoundAmongTheOnesOffered()
    {
        UIRect frame = new(0, 0, 100, 50);

        Assert.Equal(Handle.None, FrameDrag.HandleAt(frame, 0, 0, FrameEdit.HandlesFor("Window")));
        Assert.Equal(Handle.BottomRight, FrameDrag.HandleAt(frame, 100, 50, FrameEdit.HandlesFor("Window")));
    }

    private (DesignMap Map, DesktopWindow Window) Map(UIDocument document)
    {
        var view = DocumentView.Open(document.Script!, Path.GetTempPath(), _ => null, _ => { });
        using var painter = RenderCommand.Draw(view, "test", fonts, out var window);
        return (new DesignMap(document, window), window);
    }
}
