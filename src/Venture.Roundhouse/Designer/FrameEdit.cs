using System.Globalization;
using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.Roundhouse.Designer;

/// <summary>Writes a frame change into a document's <c>x</c>, <c>y</c>, <c>width</c>, and <c>height</c>.</summary>
public static class FrameEdit
{
    /// <summary>Whether an element has a frame the designer can move and resize: a control or a visual.</summary>
    public static bool IsFramed(string element) => element == "Visual" || UIScriptSchema.Controls.Contains(element);

    /// <summary>
    /// Moves and resizes an element by <paramref name="change"/>, writing only the attributes that change. A size the
    /// element doesn't write, such as a button sized by its image, starts from the size it was drawn at.
    /// </summary>
    /// <param name="drawn">Where the element was drawn, for the sizes it doesn't write; x and y are always written.</param>
    /// <returns>The edited document, or <paramref name="document"/> when nothing changes.</returns>
    /// <exception cref="ArgumentException">The element has no frame to change.</exception>
    /// <exception cref="UIScriptException">VentureOS would refuse the result.</exception>
    public static UIDocument Apply(UIDocument document, int element, UIRect drawn, FrameChange change)
    {
        ArgumentNullException.ThrowIfNull(document);
        var target = document.Xml?.Children[element]
            ?? throw new ArgumentException("The document isn't one VentureOS reads.", nameof(document));
        if (!IsFramed(target.Name))
            throw new ArgumentException($"<{target.Name}> has no frame to move or resize.", nameof(element));

        var edited = document;
        foreach (var (attribute, by, fallback) in (ReadOnlySpan<(string, float, float)>)
                 [("x", change.X, 0), ("y", change.Y, 0), ("width", change.Width, drawn.Width), ("height", change.Height, drawn.Height)])
        {
            if (by == 0)
                continue;
            var current = Number(target[attribute]) ?? fallback;
            edited = edited.SetAttribute(element, attribute, Write(current + by));
        }

        return edited;
    }

    /// <summary>A number as a document writes it: whole numbers without a point, others to three places.</summary>
    public static string Write(float value) =>
        value == MathF.Round(value)
            ? ((int)value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.###", CultureInfo.InvariantCulture);

    private static float? Number(string? value) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
}
