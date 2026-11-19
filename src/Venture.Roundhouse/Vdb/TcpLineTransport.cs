using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using Venture.OS.Vdb;

namespace Venture.Roundhouse.Vdb;

/// <summary>
/// vdb's lines over TCP, for <see cref="VdbClient"/>: each line UTF-8 and ended with <c>\n</c>, no longer than a sealed
/// line may be. Every wait is bounded, and a connection that ends or breaks is a
/// <see cref="VdbClientProblem.Connection"/>.
/// </summary>
public sealed class TcpLineTransport : IVdbLineTransport, IDisposable
{
    private static readonly UTF8Encoding Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly TcpClient client;
    private readonly NetworkStream stream;
    private readonly List<byte> pending = [];
    private readonly byte[] buffer = new byte[16 * 1024];

    private TcpLineTransport(TcpClient client)
    {
        this.client = client;
        stream = client.GetStream();
    }

    /// <summary>Connects to VentureOS, giving up after <paramref name="timeout"/>.</summary>
    /// <exception cref="VdbClientException">Nothing answered, or the connection was refused.</exception>
    public static async Task<TcpLineTransport> ConnectAsync(string host, int port, TimeSpan timeout, CancellationToken stop)
    {
        TcpClient client = new() { NoDelay = true };
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(stop);
        limit.CancelAfter(timeout);
        try
        {
            await client.ConnectAsync(host, port, limit.Token).ConfigureAwait(false);
            return new TcpLineTransport(client);
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException)
        {
            client.Dispose();
            stop.ThrowIfCancellationRequested();
            throw new VdbClientException(VdbClientProblem.Connection,
                $"Nothing answered at {host}:{port}. Is VentureOS running with its port forwarded (./run.sh --vdb), the desktop open, and developer mode on?");
        }
    }

    public void Send(string line)
    {
        var bytes = Encoding.UTF8.GetBytes(line + "\n");
        try
        {
            stream.Write(bytes);
        }
        catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException)
        {
            throw new VdbClientException(VdbClientProblem.Connection, $"The connection to VentureOS broke: {exception.Message}");
        }
    }

    public string? Receive(TimeSpan timeout)
    {
        var waited = Stopwatch.StartNew();
        for (var first = true; ; first = false)
        {
            if (TakeLine() is { } line)
                return line;

            // Whatever the timeout, what has already arrived is read once.
            var remaining = timeout - waited.Elapsed;
            if (remaining < TimeSpan.Zero && !first)
                return null;

            int read;
            try
            {
                var micro = (int)Math.Clamp(remaining.TotalMicroseconds, 0, int.MaxValue);
                if (!client.Client.Poll(micro, SelectMode.SelectRead))
                    return null;
                read = stream.Read(buffer);
            }
            catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException)
            {
                throw new VdbClientException(VdbClientProblem.Connection, $"The connection to VentureOS broke: {exception.Message}");
            }

            if (read == 0)
                throw new VdbClientException(VdbClientProblem.Connection, "VentureOS ended the connection.");
            pending.AddRange(buffer.AsSpan(0, read));
        }
    }

    public void Dispose()
    {
        stream.Dispose();
        client.Dispose();
    }

    private string? TakeLine()
    {
        var end = pending.IndexOf((byte)'\n');
        if (end < 0)
        {
            if (pending.Count > VdbChannel.MaximumLineLength + 1)
                throw new VdbClientException(VdbClientProblem.Connection, "VentureOS sent a line longer than vdb allows.");
            return null;
        }

        var length = end > 0 && pending[end - 1] == (byte)'\r' ? end - 1 : end;
        var bytes = pending.GetRange(0, length).ToArray();
        pending.RemoveRange(0, end + 1);
        try
        {
            return Strict.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new VdbClientException(VdbClientProblem.Connection, "VentureOS sent a line that isn't UTF-8.");
        }
    }
}
