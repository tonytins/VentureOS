namespace Venture.OS.Shell;

/// <summary>
/// Where commands write what they show the user.
/// </summary>
internal interface IShellOutput
{
    void WriteLine(string text);

    void Clear();
}
