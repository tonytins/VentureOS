namespace Venture.OS.Shell;

/// <summary>
/// Where commands write what they show the user.
/// </summary>
public interface IShellOutput
{
    /// <summary>Writes <paramref name="text"/> followed by a line break.</summary>
    void WriteLine(string text);

    /// <summary>Clears everything written so far, such as the screen.</summary>
    void Clear();
}
