using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.Roundhouse.Designer;

/// <summary>
/// Where each element of a document was drawn on the canvas, so a point on the canvas finds the element there and an
/// element finds where to draw its selection. Elements are named by their index among the root's children, as
/// <see cref="UIDocument"/>'s edits name them.
/// </summary>
/// <remarks>
/// It's made from the window that drew the canvas, so it matches that drawing exactly, and a new one is made each time
/// the canvas is drawn.
/// </remarks>
public sealed class DesignMap
{
    private readonly DesktopWindow window;
    private readonly List<(int Element, UIRect Bounds)> drawn = [];

    /// <summary>Maps <paramref name="document"/> as <paramref name="window"/> drew it.</summary>
    /// <exception cref="ArgumentException">The document isn't one VentureOS reads, so nothing was drawn from it.</exception>
    public DesignMap(UIDocument document, DesktopWindow window)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(window);
        if (!document.IsEditable || document.Xml is null)
            throw new ArgumentException("Only a document VentureOS reads is drawn, so only one can be mapped.", nameof(document));

        this.window = window;
        var (left, top) = window.ContentOrigin;
        var children = document.Xml.Children;
        Window = FindWindow(children);

        // Visuals are drawn first and controls over them, each in document order, so later entries are in front.
        foreach (var visuals in (bool[])[true, false])
        {
            for (var index = 0; index < children.Count; index++)
            {
                var element = children[index];
                var isVisual = element.Name == "Visual";
                if (isVisual != visuals || (!isVisual && !UIScriptSchema.Controls.Contains(element.Name)))
                    continue;
                // A control in a pane that isn't showing isn't drawn, so it can't be found or selected on the canvas.
                if (element["name"] is { } name && window.View.IsShowing(name) && window.View.Bounds(name) is { } bounds)
                    drawn.Add((index, bounds with { X = bounds.X + left, Y = bounds.Y + top }));
            }
        }

        // The title bar's items and title field, and the sidebar down the window's left, around the content.
        var toolbarItem = 0;
        var pane = 0;
        for (var index = 0; index < children.Count; index++)
        {
            switch (children[index].Name)
            {
                case "ToolbarItem":
                    if (window.ToolbarItemBounds(toolbarItem++) is { Width: > 0 } item)
                        drawn.Add((index, item));
                    break;
                case "TitleField":
                    if (window.TitleFieldBounds is { Width: > 0 } field)
                        drawn.Add((index, field));
                    break;
                case "Sidebar" when window.SidebarBounds is { } sidebar:
                    drawn.Add((index, sidebar with { Y = top, Height = window.Height - window.BarHeight }));
                    break;
            }
        }

        // Each pane is its row, in front of the sidebar, so clicking a row picks its pane. Panes are rows in
        // document order, as the reader reads them.
        for (var index = 0; index < children.Count; index++)
        {
            if (children[index].Name == "Pane" && window.View.HasPanes && window.View.SidebarRowBounds(pane++) is { } row)
                drawn.Add((index, row with { X = row.X + left, Y = row.Y + top }));
        }
    }

    /// <summary>The <c>&lt;Window&gt;</c> element's index, which is selected by clicking the window's own background.</summary>
    public int? Window { get; }

    /// <summary>The window's content on the canvas: everything below its title bar and beside its sidebar.</summary>
    public UIRect Content
    {
        get
        {
            var (left, top) = window.ContentOrigin;
            return new UIRect(left, top, window.View.WindowSize.Width, window.View.WindowSize.Height);
        }
    }

    /// <summary>
    /// The element at a point on the canvas: a title bar item, the title field, a pane's row, or the sidebar; the frontmost control
    /// or visual there; or the window, anywhere else on it, its bar included. <see langword="null"/> off the window.
    /// </summary>
    public int? ElementAt(float x, float y)
    {
        for (var index = drawn.Count - 1; index >= 0; index--)
        {
            if (drawn[index].Bounds.Contains(x, y))
                return drawn[index].Element;
        }

        return window.Bounds.Contains(x, y) ? Window : null;
    }

    /// <summary>Where an element was drawn on the canvas, or <see langword="null"/> for one that isn't drawn, such as a style.</summary>
    public UIRect? BoundsOf(int element)
    {
        if (element == Window)
            return Content;

        foreach (var (index, bounds) in drawn)
        {
            if (index == element)
                return bounds;
        }

        return null;
    }

    private static int? FindWindow(IReadOnlyList<OS.Interface.Xml.FlatXmlElement> children)
    {
        for (var index = 0; index < children.Count; index++)
        {
            if (children[index].Name is "Window" or "Panel")
                return index;
        }

        return null;
    }
}
