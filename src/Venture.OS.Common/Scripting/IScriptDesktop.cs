namespace Venture.OS.Scripting;

/// <summary>
/// The part of the desktop any script may use, app or not: showing a message, Quick Look, and opening an app. None
/// of it calls back into the script, so code that ends when it finishes, such as a shell command, can use it.
/// </summary>
/// <remarks>Interactive windows are for apps, whose code lasts while their windows do (see <c>venture.ui.open</c>).</remarks>
public interface IScriptDesktop
{
    /// <summary>Whether the desktop is on the screen; without it, an alert is printed instead.</summary>
    bool IsShowing { get; }

    /// <summary>Shows a message in a window with an OK button.</summary>
    /// <param name="app">The app it speaks for, whose menu stays in the bar while it's in front, or <see langword="null"/>.</param>
    void Alert(string title, string message, string? app = null);

    /// <summary>Shows a file in Quick Look.</summary>
    /// <returns>Why it couldn't, or <see langword="null"/>.</returns>
    string? Preview(string path);

    /// <summary>
    /// Opens an app, by name or bundle path, and hands it a file when one is given; an open app comes forward.
    /// </summary>
    /// <returns>Why it couldn't, or <see langword="null"/>.</returns>
    string? Launch(string app, string? file, string workingDirectory);
}
