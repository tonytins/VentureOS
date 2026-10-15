namespace Venture.OS.Shell;

/// <summary>
/// Writes command output to the console.
/// </summary>
internal sealed class ConsoleShellOutput : IShellOutput
{
    public void WriteLine(string text) => Console.WriteLine(text);

    public void Clear() => Console.Clear();
}
