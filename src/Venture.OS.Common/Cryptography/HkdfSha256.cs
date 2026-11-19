namespace Venture.OS.Cryptography;

/// <summary>
/// HKDF with SHA-256 (RFC 5869): turns a secret, such as vdb's identity, and values both sides chose into keys for
/// one connection, a different key for each <c>info</c>.
/// </summary>
public static class HkdfSha256
{
    /// <summary>The most HKDF-SHA-256 can derive from one key: 255 blocks.</summary>
    public const int MaximumLength = 255 * Sha256.HashSize;

    /// <summary>
    /// Extracts a pseudorandom key from <paramref name="secret"/>, with <paramref name="salt"/>; an empty salt is a
    /// block of zeros, as the RFC says.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is shorter than a SHA-256 digest.</exception>
    public static void Extract(ReadOnlySpan<byte> salt, ReadOnlySpan<byte> secret, Span<byte> key)
    {
        Span<byte> zeros = stackalloc byte[Sha256.HashSize];
        zeros.Clear();
        HmacSha256.Compute(salt.IsEmpty ? zeros : salt, secret, key);
    }

    /// <summary>Expands a pseudorandom key into <paramref name="output"/>, bound to <paramref name="info"/>.</summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="key"/> is shorter than a SHA-256 digest, or <paramref name="output"/> is longer than
    /// <see cref="MaximumLength"/>.
    /// </exception>
    public static void Expand(ReadOnlySpan<byte> key, ReadOnlySpan<byte> info, Span<byte> output)
    {
        if (key.Length < Sha256.HashSize)
            throw new ArgumentException($"An HKDF key needs at least {Sha256.HashSize} bytes.", nameof(key));
        if (output.Length > MaximumLength)
            throw new ArgumentException($"HKDF-SHA-256 derives at most {MaximumLength} bytes.", nameof(output));

        // Each block is HMAC(key, previous block | info | counter), the first with no previous block.
        var input = new byte[Sha256.HashSize + info.Length + 1];
        Span<byte> block = stackalloc byte[Sha256.HashSize];
        var previous = 0;
        for (var counter = 1; output.Length > 0; counter++)
        {
            block[..previous].CopyTo(input);
            info.CopyTo(input.AsSpan(previous));
            input[previous + info.Length] = (byte)counter;
            HmacSha256.Compute(key, input.AsSpan(0, previous + info.Length + 1), block);

            var take = Math.Min(block.Length, output.Length);
            block[..take].CopyTo(output);
            output = output[take..];
            previous = Sha256.HashSize;
        }

        block.Clear();
        Array.Clear(input);
    }

    /// <summary>Extracts and expands at once.</summary>
    public static void DeriveKey(ReadOnlySpan<byte> secret, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> info, Span<byte> output)
    {
        Span<byte> key = stackalloc byte[Sha256.HashSize];
        Extract(salt, secret, key);
        Expand(key, info, output);
        key.Clear();
    }
}
