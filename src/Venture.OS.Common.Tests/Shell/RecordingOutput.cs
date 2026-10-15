using Venture.OS.Shell;

namespace Venture.OS.Tests.Shell;

/// <summary>
/// Keeps every line a command writes, so tests can check the output.
/// </summary>
internal sealed class RecordingOutput : IShellOutput
{
    public List<string> Lines { get; } = [];

    public int ClearCount { get; private set; }

    public void WriteLine(string text) => Lines.Add(text);

    public void Clear() => ClearCount++;
}
