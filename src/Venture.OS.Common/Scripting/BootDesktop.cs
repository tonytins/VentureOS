namespace Venture.OS.Scripting;

/// <summary>
/// Whether boot ends in the desktop rather than at the prompt, as <c>venture.startDesktop()</c> asks: a developer's
/// <c>developer.local.lua</c> does, so nobody types <c>desktop</c> into QEMU. <c>init.lua</c> can't open the desktop
/// itself, since it runs until it's left and <c>init.lua</c> is stopped after ten seconds, so it only asks.
/// </summary>
/// <remarks>Later versions may ask for everyone, from <c>init.lua</c> or a setting, through the same request.</remarks>
public sealed class BootDesktop
{
    /// <summary>Whether boot has finished, after which asking is too late.</summary>
    public bool HasBooted { get; private set; }

    /// <summary>Whether the desktop was asked for.</summary>
    public bool IsRequested { get; private set; }

    /// <summary>Asks for the desktop once boot finishes; asking twice is asking once.</summary>
    /// <returns>Why it can't be, or <see langword="null"/> when it will.</returns>
    public string? Request()
    {
        if (HasBooted)
            return "VentureOS has already booted; type desktop to open it";
        IsRequested = true;
        return null;
    }

    /// <summary>Ends boot, once, giving whether to open the desktop now.</summary>
    public bool TakeAtBoot()
    {
        if (HasBooted)
            return false;
        HasBooted = true;
        return IsRequested;
    }
}
