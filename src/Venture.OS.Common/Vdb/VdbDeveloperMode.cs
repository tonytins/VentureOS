namespace Venture.OS.Vdb;

/// <summary>
/// Developer mode: whether VentureOS listens for vdb, and the computers it trusts. Off at every boot; Settings or a
/// script, such as <c>init.lua</c>, turns it on with <c>venture.developer.enable()</c>, and like everything else in
/// memory it lasts until VentureOS restarts. The kernel's listener reads it every frame and reports back through
/// <see cref="Problem"/> and <see cref="IsConnected"/>.
/// </summary>
public sealed class VdbDeveloperMode
{
    /// <summary>Whether VentureOS should listen for vdb while the desktop runs.</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>The port it listens on.</summary>
    public int Port => VdbProtocol.DefaultPort;

    /// <summary>The computers it trusts.</summary>
    public VdbTrust Trust { get; } = new();

    /// <summary>Why it isn't listening although it's on, such as no network device, or <see langword="null"/>.</summary>
    public string? Problem { get; set; }

    /// <summary>Whether a computer is connected now.</summary>
    public bool IsConnected { get; set; }

    /// <summary>Turns developer mode on; listening starts the next time the desktop draws a frame.</summary>
    public void Enable()
    {
        IsEnabled = true;
        Problem = null;
    }

    /// <summary>Turns developer mode off, closing any connection. Trusted computers stay trusted until restart.</summary>
    public void Disable()
    {
        IsEnabled = false;
        Problem = null;
    }
}
