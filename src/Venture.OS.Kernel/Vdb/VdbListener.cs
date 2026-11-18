using Venture.OS.Bundles;
using Venture.OS.Desktop;

namespace Venture.OS.Vdb;

/// <summary>
/// Listens for vdb while the desktop runs and developer mode is on, a frame at a time: the desktop's frame loop
/// calls <see cref="Poll"/>, which accepts a waiting computer, hands what it sent to a <see cref="VdbSession"/>,
/// and asks the person whether to trust it. Sockets in Cosmos are safe on one thread only, so nothing here waits.
/// </summary>
/// <remarks>
/// One computer at a time; another is told it's busy and closed. Nothing that goes wrong here stops the desktop: a
/// failure ends the connection, or turns listening off with the reason in <see cref="VdbDeveloperMode.Problem"/>.
/// </remarks>
internal sealed class VdbListener
{
    // How much is read in one frame, so a fast sender can't stall the desktop.
    private const int ReadsPerFrame = 16;

    private readonly VdbDeveloperMode developer;
    private readonly WindowManager windows;
    private readonly KernelVdbHost host;
    private readonly byte[] buffer = new byte[4096];

    private TcpListener? listener;
    private TcpClient? client;
    private NetworkStream? stream;
    private VdbSession? session;
    private DesktopWindow? trustAlert;
    private bool changed;

    /// <param name="developer">Whether to listen, and whom to trust.</param>
    /// <param name="windows">The desktop's windows, for the Trust alert.</param>
    /// <param name="appWindows">Apps' windows, for opening apps.</param>
    /// <param name="apps">The system's apps, once they're ready.</param>
    /// <param name="appOutput">What apps print, which goes back to a trusted computer too.</param>
    public VdbListener(
        VdbDeveloperMode developer, WindowManager windows, AppWindows appWindows, Func<AppManager?> apps, HeldOutput appOutput)
    {
        this.developer = developer;
        this.windows = windows;
        host = new KernelVdbHost(appWindows, apps, appOutput);
        appOutput.Echo = text => session?.Print(text);
    }

    /// <summary>Does one frame's listening.</summary>
    /// <param name="now">The time, on a clock that only goes forward.</param>
    /// <returns>Whether the desktop should draw again, such as for the Trust alert or an app that opened.</returns>
    public bool Poll(TimeSpan now)
    {
        changed = false;
        try
        {
            Step(now);
        }
        catch (Exception exception)
        {
            // Last resort: whatever this was, listening stops and the desktop goes on.
            KernelLog.WriteSerialLine($"vdb stopped ({exception.GetType().Name}): {exception.Message}");
            Stop();
            developer.Disable();
            developer.Problem = $"vdb stopped: {exception.Message}";
        }

        return changed;
    }

    /// <summary>Closes any connection and stops listening, as the desktop closes.</summary>
    public void Stop()
    {
        EndConnection();
        if (listener is null)
            return;

        try
        {
            listener.Stop();
        }
        catch (Exception exception) when (exception is SocketException or InvalidOperationException)
        {
            KernelLog.WriteSerialLine($"vdb couldn't stop listening: {exception.Message}");
        }

        listener = null;
        KernelLog.WriteSerialLine("vdb stopped listening.");
    }

    private void Step(TimeSpan now)
    {
        if (!developer.IsEnabled)
        {
            if (listener is not null)
                Stop();
            return;
        }

        if (listener is null && !Listen())
            return;

        if (listener!.Pending())
        {
            if (client is null)
                Accept(now);
            else
                TurnAway();
        }

        if (session is null || stream is null)
            return;

        for (var reads = 0; reads < ReadsPerFrame && !session.IsClosed && stream.DataAvailable; reads++)
        {
            var count = stream.Read(buffer, 0, buffer.Length);
            if (count <= 0)
            {
                session.Close();
                break;
            }

            session.Receive(buffer.AsSpan(0, count), now);
        }

        session.Tick(now);
        AskAboutTrust();
        changed |= host.TakeChanged();
        if (session.IsClosed)
            EndConnection();
    }

    private bool Listen()
    {
        if (NetworkManager.DeviceCount == 0)
        {
            developer.Disable();
            developer.Problem = "there's no network device to listen on";
            return false;
        }

        try
        {
            TcpListener started = new(IPAddress.Any, developer.Port);
            started.Start();
            listener = started;
        }
        catch (Exception exception) when (exception is SocketException or InvalidOperationException)
        {
            developer.Disable();
            developer.Problem = $"vdb couldn't listen on port {developer.Port}: {exception.Message}";
            KernelLog.WriteSerialLine(developer.Problem);
            return false;
        }

        KernelLog.WriteSerialLine($"vdb is listening on port {developer.Port}.");
        return true;
    }

    private void Accept(TimeSpan now)
    {
        client = listener!.AcceptTcpClient();
        stream = client.GetStream();
        session = new VdbSession(host, developer.Trust, Send, now);
        developer.IsConnected = true;
        KernelLog.WriteSerialLine("vdb: a computer connected.");
    }

    // Only one computer at a time; the next is told why and let go.
    private void TurnAway()
    {
        var other = listener!.AcceptTcpClient();
        try
        {
            var reply = Encoding.UTF8.GetBytes("error closing busy: another computer is connected\n");
            other.GetStream().Write(reply, 0, reply.Length);
        }
        catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException)
        {
            // It's being turned away; whether it heard why doesn't matter here.
        }
        finally
        {
            other.Close();
        }

        KernelLog.WriteSerialLine("vdb: turned away a second computer.");
    }

    private void Send(string line)
    {
        if (stream is null)
            return;

        try
        {
            var bytes = Encoding.UTF8.GetBytes(line + "\n");
            stream.Write(bytes, 0, bytes.Length);
        }
        catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException)
        {
            KernelLog.WriteSerialLine($"vdb: the computer stopped listening: {exception.Message}");
            session?.Close();
        }
    }

    /// <summary>Shows the Trust alert while a computer is being asked about, and takes it away once it isn't.</summary>
    private void AskAboutTrust()
    {
        var identity = session is { IsClosed: false } ? session.Identity : null;
        var asking = identity is not null && developer.Trust.State(identity) == VdbTrustState.Asking;

        if (asking && trustAlert is null)
        {
            trustAlert = DesktopAlert.Ask(
                "Trust This Computer?",
                $"A computer, {identity![..8]}, wants to install and open apps over vdb until VentureOS restarts. "
                + "Trust it only if it's yours.",
                "Trust",
                "Deny",
                trust => developer.Trust.Answer(identity!, trust));
            windows.Open(trustAlert);
            changed = true;
        }
        else if (!asking && trustAlert is not null)
        {
            CloseTrustAlert();
        }
    }

    private void CloseTrustAlert()
    {
        if (trustAlert is null)
            return;

        trustAlert.RequestClose();
        trustAlert = null;
        changed = true;
    }

    private void EndConnection()
    {
        session?.Close();
        CloseTrustAlert();
        if (client is not null)
        {
            try
            {
                client.Close();
            }
            catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
            {
                KernelLog.WriteSerialLine($"vdb couldn't close the connection: {exception.Message}");
            }

            KernelLog.WriteSerialLine("vdb: the computer disconnected.");
        }

        (client, stream, session) = (null, null, null);
        developer.IsConnected = false;
    }

    /// <summary>The kernel's apps, as a vdb session reaches them.</summary>
    private sealed class KernelVdbHost(AppWindows appWindows, Func<AppManager?> apps, HeldOutput output) : IVdbHost
    {
        private bool changed;

        public string StagingFolder => $"{Startup.Bootstrap.UserDirectoryPath}/.vdb";

        public string AppsFolder => Startup.Bootstrap.UserAppsDirectoryPath;

        public bool IsSystemApp(string identifier)
        {
            foreach (var listing in apps()?.Available() ?? [])
            {
                if (!listing.IsUser && listing.Bundle?.Identifier == identifier)
                    return true;
            }

            return false;
        }

        public void CloseApp(string identifier)
        {
            if (apps() is { } manager && manager.IsOpen(identifier) && manager.CloseApp(identifier, output))
            {
                changed = true;
                KernelLog.WriteSerialLine($"vdb closed {identifier} to replace it.");
            }
        }

        public string? Launch(string app)
        {
            if (apps() is not { } manager)
                return "the system's apps aren't ready";

            // The user's copy first, which is where vdb installs, then the system's.
            var user = $"{AppsFolder}/{app}";
            var system = $"{Startup.Bootstrap.AppsDirectoryPath}/{app}";
            var path = Directory.Exists(user) ? user : Directory.Exists(system) ? system : null;
            var problem = path is null
                ? $"there's no {app} in {AppsFolder} or {Startup.Bootstrap.AppsDirectoryPath}"
                : UiLibrary.LaunchApp(appWindows, manager, path, null, "/");
            changed = true;
            KernelLog.WriteSerialLine(problem is null ? $"vdb opened {app}." : $"vdb couldn't open {app}: {problem}");
            return problem;
        }

        /// <summary>Whether an app opened or closed since the last call.</summary>
        public bool TakeChanged()
        {
            var was = changed;
            changed = false;
            return was;
        }
    }
}
