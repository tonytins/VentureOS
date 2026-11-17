using Venture.OS.Shell;

namespace Venture.OS.Kernel;

/// <summary>
/// Shell output that goes through <see cref="KernelLog"/>, to serial as well as the console.
/// </summary>
/// <remarks>
/// For Lua that runs as part of the system rather than as a command, such as <c>init.lua</c>, so a headless boot
/// can see what it printed.
/// </remarks>
internal sealed class KernelLogOutput : IShellOutput
{
    public void WriteLine(string text) => KernelLog.WriteLine(text);

    public void Clear() => Console.Clear();
}
