using System.Buffers.Binary;
using System.Text;
using Venture.OS.Cryptography;

namespace Venture.OS.Vdb;

/// <summary>
/// The sealed half of a vdb/1 connection, after hello: each line of the protocol travels as <c>sealed &lt;base64&gt;</c>,
/// ChaCha20-Poly1305 over its UTF-8, with keys derived from the computer's identity and both sides' nonces. Either
/// side makes one, VentureOS with <see cref="ForVentureOS"/> and a computer with <see cref="ForComputer"/>, so
/// Roundhouse can speak vdb with the same code VentureOS does.
/// </summary>
/// <remarks>
/// <para>Each direction has its own key, from HKDF-SHA-256 with the identity's 16 bytes as the secret, the client's
/// nonce then the server's as the salt, and <c>vdb/1 to venture</c> or <c>vdb/1 to computer</c> as the info. A
/// line's nonce is four zero bytes and the count of lines sent that way before it, little-endian, which is never
/// sent, so a line can't be dropped, repeated, or moved without the next one failing.</para>
/// <para>A line that fails to open is never acted on: <see cref="Open"/> returns <see langword="null"/>, and the
/// connection should end, since nothing after it can be trusted to be in its place.</para>
/// </remarks>
public sealed class VdbChannel
{
    /// <summary>What starts every sealed line.</summary>
    public const string Prefix = "sealed ";

    /// <summary>The longest sealed line: the longest plain line, its tag, in base64, after the prefix.</summary>
    public const int MaximumLineLength = 7 + ((VdbProtocol.MaximumLineLength + ChaCha20Poly1305.TagSize + 2) / 3 * 4);

    /// <summary>A nonce's length in bytes; each side sends 16 random bytes, as 32 hexadecimal characters.</summary>
    public const int NonceSize = 16;

    private static readonly UTF8Encoding Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly byte[] sendKey = new byte[ChaCha20Poly1305.KeySize];
    private readonly byte[] receiveKey = new byte[ChaCha20Poly1305.KeySize];
    private ulong sent;
    private ulong received;

    private VdbChannel(string identity, ReadOnlySpan<byte> clientNonce, ReadOnlySpan<byte> serverNonce, bool isVentureOS)
    {
        if (!VdbTrust.IsIdentity(identity))
            throw new ArgumentException("A vdb channel is keyed from an identity, 32 lowercase hexadecimal characters.", nameof(identity));
        if (clientNonce.Length != NonceSize || serverNonce.Length != NonceSize)
            throw new ArgumentException($"Each side's nonce is {NonceSize} bytes.");

        Span<byte> secret = stackalloc byte[16];
        Hex.Decode(identity, secret);
        Span<byte> salt = stackalloc byte[NonceSize * 2];
        clientNonce.CopyTo(salt);
        serverNonce.CopyTo(salt[NonceSize..]);

        HkdfSha256.DeriveKey(secret, salt, Encoding.ASCII.GetBytes("vdb/1 to venture"), isVentureOS ? receiveKey : sendKey);
        HkdfSha256.DeriveKey(secret, salt, Encoding.ASCII.GetBytes("vdb/1 to computer"), isVentureOS ? sendKey : receiveKey);
        secret.Clear();
    }

    /// <summary>VentureOS's side of a connection.</summary>
    /// <exception cref="ArgumentException">The identity or a nonce isn't one.</exception>
    public static VdbChannel ForVentureOS(string identity, ReadOnlySpan<byte> clientNonce, ReadOnlySpan<byte> serverNonce) =>
        new(identity, clientNonce, serverNonce, isVentureOS: true);

    /// <summary>A computer's side of a connection.</summary>
    /// <exception cref="ArgumentException">The identity or a nonce isn't one.</exception>
    public static VdbChannel ForComputer(string identity, ReadOnlySpan<byte> clientNonce, ReadOnlySpan<byte> serverNonce) =>
        new(identity, clientNonce, serverNonce, isVentureOS: false);

    /// <summary>Seals a line of the protocol, without its line break, as the next line this side sends.</summary>
    /// <exception cref="ArgumentException">The line is longer than vdb allows, or holds a line break.</exception>
    /// <exception cref="InvalidOperationException">This side has sent as many lines as a key may seal.</exception>
    public string Seal(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.Contains('\n') || line.Contains('\r'))
            throw new ArgumentException("A vdb line can't hold a line break.", nameof(line));
        var plain = Encoding.UTF8.GetBytes(line);
        if (plain.Length > VdbProtocol.MaximumLineLength)
            throw new ArgumentException($"A vdb line is at most {VdbProtocol.MaximumLineLength} bytes.", nameof(line));
        if (sent == ulong.MaxValue)
            throw new InvalidOperationException("This connection has sealed as many lines as its key may; start a new one.");

        var sealedBytes = new byte[plain.Length + ChaCha20Poly1305.TagSize];
        Span<byte> nonce = stackalloc byte[ChaCha20Poly1305.NonceSize];
        Nonce(sent++, nonce);
        ChaCha20Poly1305.Seal(sendKey, nonce, plain, [], sealedBytes.AsSpan(0, plain.Length), sealedBytes.AsSpan(plain.Length));
        return Prefix + Convert.ToBase64String(sealedBytes);
    }

    /// <summary>
    /// Opens the next line the other side sent: its line of the protocol, or <see langword="null"/> when it isn't a
    /// sealed line, wasn't sealed with this connection's key, isn't the next one, or holds something vdb doesn't
    /// allow. After <see langword="null"/>, end the connection.
    /// </summary>
    public string? Open(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (!line.StartsWith(Prefix, StringComparison.Ordinal) || line.Length > MaximumLineLength || received == ulong.MaxValue)
            return null;

        var encoded = line.AsSpan(Prefix.Length);
        var most = encoded.Length / 4 * 3;
        if (encoded.Length % 4 != 0 || most < ChaCha20Poly1305.TagSize)
            return null;

        var sealedBytes = new byte[most];
        if (!Convert.TryFromBase64Chars(encoded, sealedBytes, out var length) || length < ChaCha20Poly1305.TagSize)
            return null;

        var textLength = length - ChaCha20Poly1305.TagSize;
        if (textLength > VdbProtocol.MaximumLineLength)
            return null;

        var plain = new byte[textLength];
        Span<byte> nonce = stackalloc byte[ChaCha20Poly1305.NonceSize];
        Nonce(received, nonce);
        if (!ChaCha20Poly1305.Open(receiveKey, nonce, sealedBytes.AsSpan(0, textLength), [], sealedBytes.AsSpan(textLength, ChaCha20Poly1305.TagSize), plain))
            return null;
        received++;

        string text;
        try
        {
            text = Strict.GetString(plain);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }

        // A line can't hold its own break; one inside would be two lines the other side never sent as such.
        return text.Contains('\n') || text.Contains('\r') ? null : text;
    }

    private static void Nonce(ulong count, Span<byte> nonce)
    {
        nonce.Clear();
        BinaryPrimitives.WriteUInt64LittleEndian(nonce[4..], count);
    }
}
