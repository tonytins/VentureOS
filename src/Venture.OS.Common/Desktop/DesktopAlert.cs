using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>A small window with a message and an OK button, such as what an app said while it opened.</summary>
public static class DesktopAlert
{
    private const int Width = 380;
    private const int Margin = 16;
    private const int LineHeight = 18;
    private const int MaximumLines = 20;

    // Roughly how wide the interface font's average character is at the desktop's text size, to size the window
    // before anything is measured. Text that runs past the estimate is cut off at the bottom, not lost from view
    // entirely, and the window is never smaller than one line.
    private const float AverageCharacterWidth = 7f;

    /// <summary>Makes the window, which OK or its close button closes.</summary>
    /// <param name="title">The window's title.</param>
    /// <param name="message">The text, with line breaks where it has them.</param>
    /// <param name="app">The app it speaks for, whose menu stays in the bar while it's in front.</param>
    public static DesktopWindow Create(string title, string message, string? app = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);

        var textWidth = Width - (2 * Margin);
        var lines = 0;
        foreach (var paragraph in message.Split('\n'))
            lines += Math.Max(1, (int)MathF.Ceiling(paragraph.Length * AverageCharacterWidth / textWidth));
        lines = Math.Clamp(lines, 1, MaximumLines);

        var textHeight = (lines * LineHeight) + 4;
        var buttonY = Margin + textHeight + 12;
        var height = buttonY + 24 + 12;

        // The message is set on the view, not written into the document, so nothing in it is read as markup.
        var document = $"""
            <UIScript version="1">
              <Window width="{Width}" height="{height}"/>
              <Style name="plain" backgroundColor="#F0F3F7"/>
              <FormattedText name="message" x="{Margin}" y="{Margin}" width="{textWidth}" height="{textHeight}" style="plain"/>
              <Button name="ok" x="{Width - Margin - 74}" y="{buttonY}" width="74" height="24" text="OK"/>
            </UIScript>
            """;

        UIView view = new(UIScript.Parse(document));
        view.SetText("message", message);
        return new DesktopWindow(title, view)
        {
            App = app,
            EventHandler = (window, uiEvent) =>
            {
                if (uiEvent is { Kind: UIEventKind.Pressed, Control: "ok" })
                    window.RequestClose();
            },
        };
    }
}
