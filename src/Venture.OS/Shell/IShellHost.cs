namespace Venture.OS.Shell;

/// <summary>
/// What the shell asks of the kernel when the user wants to stop the system.
/// </summary>
/// <remarks>
/// The kernel implements this, so the power calls stay in <c>Venture.OS.Kernel</c>.
/// </remarks>
internal interface IShellHost
{
    /// <summary>Ends the shell loop; the CPU then halts.</summary>
    void Halt();

    /// <summary>Restarts the machine.</summary>
    void Reboot();

    /// <summary>Powers the machine off.</summary>
    void Shutdown();
}
