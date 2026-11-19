namespace Venture.OS.Cryptography;

/// <summary>
/// HMAC with SHA-256 (RFC 2104), which HKDF is built on. A key longer than a block is hashed first, as the RFC says.
/// </summary>
public static class HmacSha256
{
    /// <summary>A tag's length in bytes.</summary>
    public const int TagSize = Sha256.HashSize;

    /// <summary>The tag of <paramref name="message"/> under <paramref name="key"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="tag"/> is shorter than <see cref="TagSize"/>.</exception>
    public static void Compute(ReadOnlySpan<byte> key, ReadOnlySpan<byte> message, Span<byte> tag)
    {
        if (tag.Length < TagSize)
            throw new ArgumentException($"An HMAC-SHA-256 tag needs {TagSize} bytes.", nameof(tag));

        Span<byte> block = stackalloc byte[Sha256.BlockSize];
        block.Clear();
        if (key.Length > Sha256.BlockSize)
            Sha256.Hash(key, block);
        else
            key.CopyTo(block);

        Span<byte> pad = stackalloc byte[Sha256.BlockSize];
        Span<byte> inner = stackalloc byte[Sha256.HashSize];
        Sha256 sha = new();

        for (var index = 0; index < pad.Length; index++)
            pad[index] = (byte)(block[index] ^ 0x36);
        sha.Append(pad);
        sha.Append(message);
        sha.Finish(inner);

        sha.Reset();
        for (var index = 0; index < pad.Length; index++)
            pad[index] = (byte)(block[index] ^ 0x5c);
        sha.Append(pad);
        sha.Append(inner);
        sha.Finish(tag);

        block.Clear();
    }
}
