using System.Buffers.Binary;

namespace Venture.OS.Cryptography;

/// <summary>
/// ChaCha20 and the ChaCha20-Poly1305 AEAD (RFC 8439), which seals vdb's lines. ChaCha20 is only 32-bit adds,
/// rotations, and XORs, with no tables, so its timing doesn't depend on the key, and it compiles ahead of time as
/// plainly as anything else here.
/// </summary>
/// <remarks>
/// A key must never seal two messages under the same nonce: that gives away both. vdb numbers its messages for the
/// nonce, with a key per direction, so it never does.
/// </remarks>
public static class ChaCha20Poly1305
{
    /// <summary>A key's length in bytes.</summary>
    public const int KeySize = 32;

    /// <summary>A nonce's length in bytes.</summary>
    public const int NonceSize = 12;

    /// <summary>A tag's length in bytes.</summary>
    public const int TagSize = Poly1305.TagSize;

    private const int BlockSize = 64;

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> into <paramref name="ciphertext"/>, the same length, and writes the tag
    /// that authenticates it with <paramref name="associatedData"/>.
    /// </summary>
    /// <exception cref="ArgumentException">A key, nonce, or tag is the wrong size, or the lengths don't match.</exception>
    public static void Seal(
        ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData,
        Span<byte> ciphertext, Span<byte> tag)
    {
        Check(key, nonce, tag);
        if (ciphertext.Length != plaintext.Length)
            throw new ArgumentException("The ciphertext must be as long as the plaintext.", nameof(ciphertext));

        Xor(key, nonce, 1, plaintext, ciphertext);
        ComputeTag(key, nonce, associatedData, ciphertext, tag);
    }

    /// <summary>
    /// Checks <paramref name="tag"/> and, only when it's right, decrypts <paramref name="ciphertext"/> into
    /// <paramref name="plaintext"/>.
    /// </summary>
    /// <returns>Whether the tag was right. When it isn't, <paramref name="plaintext"/> is cleared.</returns>
    /// <exception cref="ArgumentException">A key, nonce, or tag is the wrong size, or the lengths don't match.</exception>
    public static bool Open(
        ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> associatedData,
        ReadOnlySpan<byte> tag, Span<byte> plaintext)
    {
        Check(key, nonce, tag);
        if (plaintext.Length != ciphertext.Length)
            throw new ArgumentException("The plaintext must be as long as the ciphertext.", nameof(plaintext));

        Span<byte> expected = stackalloc byte[TagSize];
        ComputeTag(key, nonce, associatedData, ciphertext, expected);
        if (!FixedTimeEquals(expected, tag))
        {
            plaintext.Clear();
            return false;
        }

        Xor(key, nonce, 1, ciphertext, plaintext);
        return true;
    }

    /// <summary>
    /// ChaCha20 itself: XORs <paramref name="input"/> with the key stream from block <paramref name="counter"/> on.
    /// </summary>
    /// <exception cref="ArgumentException">The key or nonce is the wrong size, or the lengths don't match.</exception>
    public static void Xor(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, uint counter, ReadOnlySpan<byte> input, Span<byte> output)
    {
        if (key.Length != KeySize)
            throw new ArgumentException($"A ChaCha20 key is {KeySize} bytes.", nameof(key));
        if (nonce.Length != NonceSize)
            throw new ArgumentException($"A ChaCha20 nonce is {NonceSize} bytes.", nameof(nonce));
        if (output.Length != input.Length)
            throw new ArgumentException("The output must be as long as the input.", nameof(output));

        Span<uint> state = stackalloc uint[16];
        Span<uint> working = stackalloc uint[16];
        Span<byte> stream = stackalloc byte[BlockSize];
        for (var offset = 0; offset < input.Length; offset += BlockSize)
        {
            Block(key, nonce, counter++, state, working, stream);
            var length = Math.Min(BlockSize, input.Length - offset);
            for (var index = 0; index < length; index++)
                output[offset + index] = (byte)(input[offset + index] ^ stream[index]);
        }

        stream.Clear();
        working.Clear();
    }

    /// <summary>Whether two byte strings are equal, taking as long whichever byte differs.</summary>
    public static bool FixedTimeEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
            return false;
        var difference = 0;
        for (var index = 0; index < left.Length; index++)
            difference |= left[index] ^ right[index];
        return difference == 0;
    }

    private static void Check(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> tag)
    {
        if (key.Length != KeySize)
            throw new ArgumentException($"A ChaCha20-Poly1305 key is {KeySize} bytes.", nameof(key));
        if (nonce.Length != NonceSize)
            throw new ArgumentException($"A ChaCha20-Poly1305 nonce is {NonceSize} bytes.", nameof(nonce));
        if (tag.Length != TagSize)
            throw new ArgumentException($"A ChaCha20-Poly1305 tag is {TagSize} bytes.", nameof(tag));
    }

    // The tag: Poly1305, keyed by block 0, over the data and ciphertext, each padded to 16, and their lengths.
    private static void ComputeTag(
        ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> associatedData, ReadOnlySpan<byte> ciphertext,
        Span<byte> tag)
    {
        Span<byte> zeros = stackalloc byte[Poly1305.KeySize];
        zeros.Clear();
        Span<byte> polyKey = stackalloc byte[Poly1305.KeySize];
        Xor(key, nonce, 0, zeros, polyKey);

        Poly1305 poly = new(polyKey);
        poly.Append(associatedData);
        poly.PadTo16();
        poly.Append(ciphertext);
        poly.PadTo16();
        Span<byte> lengths = stackalloc byte[16];
        BinaryPrimitives.WriteUInt64LittleEndian(lengths, (ulong)associatedData.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(lengths[8..], (ulong)ciphertext.Length);
        poly.Append(lengths);
        poly.Finish(tag);
        polyKey.Clear();
    }

    private static void Block(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, uint counter, Span<uint> state, Span<uint> x, Span<byte> output)
    {
        state[0] = 0x61707865;
        state[1] = 0x3320646e;
        state[2] = 0x79622d32;
        state[3] = 0x6b206574;
        for (var index = 0; index < 8; index++)
            state[4 + index] = BinaryPrimitives.ReadUInt32LittleEndian(key[(index * 4)..]);
        state[12] = counter;
        state[13] = BinaryPrimitives.ReadUInt32LittleEndian(nonce);
        state[14] = BinaryPrimitives.ReadUInt32LittleEndian(nonce[4..]);
        state[15] = BinaryPrimitives.ReadUInt32LittleEndian(nonce[8..]);

        state.CopyTo(x);
        for (var round = 0; round < 10; round++)
        {
            Quarter(x, 0, 4, 8, 12);
            Quarter(x, 1, 5, 9, 13);
            Quarter(x, 2, 6, 10, 14);
            Quarter(x, 3, 7, 11, 15);
            Quarter(x, 0, 5, 10, 15);
            Quarter(x, 1, 6, 11, 12);
            Quarter(x, 2, 7, 8, 13);
            Quarter(x, 3, 4, 9, 14);
        }

        for (var index = 0; index < 16; index++)
            BinaryPrimitives.WriteUInt32LittleEndian(output[(index * 4)..], x[index] + state[index]);
        state.Clear();
    }

    private static void Quarter(Span<uint> x, int a, int b, int c, int d)
    {
        x[a] += x[b]; x[d] = Rotate(x[d] ^ x[a], 16);
        x[c] += x[d]; x[b] = Rotate(x[b] ^ x[c], 12);
        x[a] += x[b]; x[d] = Rotate(x[d] ^ x[a], 8);
        x[c] += x[d]; x[b] = Rotate(x[b] ^ x[c], 7);
    }

    private static uint Rotate(uint value, int by) => (value << by) | (value >> (32 - by));
}
