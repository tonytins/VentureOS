using Venture.OS.Scripting;

namespace Venture.OS.Tests.Scripting;

/// <summary>A desktop that records what scripts ask of it, and answers as told.</summary>
internal sealed class FakeScriptDesktop : IScriptDesktop
{
    public bool IsShowing { get; set; } = true;

    public List<(string Title, string Message)> Alerts { get; } = [];

    public List<string> Previews { get; } = [];

    public List<string> Toasts { get; } = [];

    public List<(string App, string? File, string WorkingDirectory)> Launches { get; } = [];

    /// <summary>What Preview and Launch answer: <see langword="null"/> for success, or why not.</summary>
    public string? Problem { get; set; }

    public void Alert(string title, string message, string? app = null) => Alerts.Add((title, message));

    public string? Preview(string path)
    {
        Previews.Add(path);
        return Problem;
    }

    public string? Toast(string text)
    {
        Toasts.Add(text);
        return Problem;
    }

    public string? Launch(string app, string? file, string workingDirectory)
    {
        Launches.Add((app, file, workingDirectory));
        return Problem;
    }
}
