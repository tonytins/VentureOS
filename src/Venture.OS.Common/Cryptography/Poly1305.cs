using System.Buffers.Binary;

namespace Venture.OS.Cryptography;

/// <summary>
/// Poly1305 (RFC 8439, section 2.5): a one-time authenticator, keyed afresh for every message by ChaCha20. The
/// 130-bit arithmetic is done in five 26-bit limbs with 64-bit products, as poly1305-donna does, so nothing needs
/// more than plain integer arithmetic.
/// </summary>
public sealed class Poly1305
{
    /// <summary>A key's length in bytes: <c>r</c> then <c>s</c>.</summary>
    public const int KeySize = 32;

    /// <summary>A tag's length in bytes.</summary>
    public const int TagSize = 16;

    private const uint Mask = 0x3ffffff;

    private readonly uint r0, r1, r2, r3, r4;
    private readonly uint s1, s2, s3, s4;
    private readonly uint pad0, pad1, pad2, pad3;
    private uint h0, h1, h2, h3, h4;
    private readonly byte[] buffer = new byte[16];
    private int buffered;
    private bool finished;

    /// <summary>Starts a tag under a one-time key.</summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> isn't <see cref="KeySize"/> bytes.</exception>
    public Poly1305(ReadOnlySpan<byte> key)
    {
        if (key.Length != KeySize)
            throw new ArgumentException($"A Poly1305 key is {KeySize} bytes.", nameof(key));

        // r, clamped as the RFC says, in 26-bit limbs.
        r0 = Le(key, 0) & 0x3ffffff;
        r1 = (Le(key, 3) >> 2) & 0x3ffff03;
        r2 = (Le(key, 6) >> 4) & 0x3ffc0ff;
        r3 = (Le(key, 9) >> 6) & 0x3f03fff;
        r4 = (Le(key, 12) >> 8) & 0x00fffff;
        (s1, s2, s3, s4) = (r1 * 5, r2 * 5, r3 * 5, r4 * 5);
        (pad0, pad1, pad2, pad3) = (Le(key, 16), Le(key, 20), Le(key, 24), Le(key, 28));
    }

    /// <summary>Adds bytes to the message.</summary>
    /// <exception cref="InvalidOperationException">The tag was already finished.</exception>
    public void Append(ReadOnlySpan<byte> data)
    {
        if (finished)
            throw new InvalidOperationException("This Poly1305 tag is finished.");

        if (buffered > 0)
        {
            var take = Math.Min(16 - buffered, data.Length);
            data[..take].CopyTo(buffer.AsSpan(buffered));
            buffered += take;
            data = data[take..];
            if (buffered < 16)
                return;
            Block(buffer, 1u << 24);
            buffered = 0;
        }

        while (data.Length >= 16)
        {
            Block(data[..16], 1u << 24);
            data = data[16..];
        }

        data.CopyTo(buffer);
        buffered = data.Length;
    }

    /// <summary>
    /// Adds zeros up to the next 16-byte boundary, as the AEAD construction pads its data and ciphertext; nothing
    /// when already on one.
    /// </summary>
    public void PadTo16()
    {
        if (buffered == 0)
            return;
        Span<byte> zeros = stackalloc byte[16];
        zeros.Clear();
        Append(zeros[..(16 - buffered)]);
    }

    /// <summary>Writes the tag.</summary>
    /// <exception cref="ArgumentException"><paramref name="tag"/> is shorter than <see cref="TagSize"/>.</exception>
    /// <exception cref="InvalidOperationException">The tag was already finished.</exception>
    public void Finish(Span<byte> tag)
    {
        if (tag.Length < TagSize)
            throw new ArgumentException($"A Poly1305 tag needs {TagSize} bytes.", nameof(tag));
        if (finished)
            throw new InvalidOperationException("This Poly1305 tag is finished.");
        finished = true;

        // A last partial block ends with a 1 byte instead of the high bit.
        if (buffered > 0)
        {
            buffer[buffered] = 1;
            buffer.AsSpan(buffered + 1).Clear();
            Block(buffer, 0);
        }

        // Carry fully, then reduce mod 2^130 - 5 by computing h + 5 - 2^130 and keeping it if it isn't negative.
        uint c;
        c = h1 >> 26; h1 &= Mask; h2 += c;
        c = h2 >> 26; h2 &= Mask; h3 += c;
        c = h3 >> 26; h3 &= Mask; h4 += c;
        c = h4 >> 26; h4 &= Mask; h0 += c * 5;
        c = h0 >> 26; h0 &= Mask; h1 += c;

        var g0 = h0 + 5; c = g0 >> 26; g0 &= Mask;
        var g1 = h1 + c; c = g1 >> 26; g1 &= Mask;
        var g2 = h2 + c; c = g2 >> 26; g2 &= Mask;
        var g3 = h3 + c; c = g3 >> 26; g3 &= Mask;
        var g4 = h4 + c - (1u << 26);

        // All ones when g4 didn't go negative, so g is chosen; done with masks, so no branch depends on the value.
        var keepG = (g4 >> 31) - 1;
        var keepH = ~keepG;
        h0 = (h0 & keepH) | (g0 & keepG);
        h1 = (h1 & keepH) | (g1 & keepG);
        h2 = (h2 & keepH) | (g2 & keepG);
        h3 = (h3 & keepH) | (g3 & keepG);
        h4 = (h4 & keepH) | (g4 & keepG);

        // Back to four 32-bit words, then add s mod 2^128.
        var w0 = h0 | (h1 << 26);
        var w1 = (h1 >> 6) | (h2 << 20);
        var w2 = (h2 >> 12) | (h3 << 14);
        var w3 = (h3 >> 18) | (h4 << 8);

        ulong f = (ulong)w0 + pad0;
        BinaryPrimitives.WriteUInt32LittleEndian(tag, (uint)f);
        f = (ulong)w1 + pad1 + (f >> 32);
        BinaryPrimitives.WriteUInt32LittleEndian(tag[4..], (uint)f);
        f = (ulong)w2 + pad2 + (f >> 32);
        BinaryPrimitives.WriteUInt32LittleEndian(tag[8..], (uint)f);
        f = (ulong)w3 + pad3 + (f >> 32);
        BinaryPrimitives.WriteUInt32LittleEndian(tag[12..], (uint)f);

        (h0, h1, h2, h3, h4) = (0, 0, 0, 0, 0);
        Array.Clear(buffer);
    }

    /// <summary>The tag of a whole message under a one-time key.</summary>
    public static void Compute(ReadOnlySpan<byte> key, ReadOnlySpan<byte> message, Span<byte> tag)
    {
        Poly1305 poly = new(key);
        poly.Append(message);
        poly.Finish(tag);
    }

    private void Block(ReadOnlySpan<byte> block, uint highBit)
    {
        h0 += Le(block, 0) & Mask;
        h1 += (Le(block, 3) >> 2) & Mask;
        h2 += (Le(block, 6) >> 4) & Mask;
        h3 += (Le(block, 9) >> 6) & Mask;
        h4 += (Le(block, 12) >> 8) | highBit;

        ulong d0 = ((ulong)h0 * r0) + ((ulong)h1 * s4) + ((ulong)h2 * s3) + ((ulong)h3 * s2) + ((ulong)h4 * s1);
        ulong d1 = ((ulong)h0 * r1) + ((ulong)h1 * r0) + ((ulong)h2 * s4) + ((ulong)h3 * s3) + ((ulong)h4 * s2);
        ulong d2 = ((ulong)h0 * r2) + ((ulong)h1 * r1) + ((ulong)h2 * r0) + ((ulong)h3 * s4) + ((ulong)h4 * s3);
        ulong d3 = ((ulong)h0 * r3) + ((ulong)h1 * r2) + ((ulong)h2 * r1) + ((ulong)h3 * r0) + ((ulong)h4 * s4);
        ulong d4 = ((ulong)h0 * r4) + ((ulong)h1 * r3) + ((ulong)h2 * r2) + ((ulong)h3 * r1) + ((ulong)h4 * r0);

        ulong carry;
        carry = d0 >> 26; h0 = (uint)d0 & Mask; d1 += carry;
        carry = d1 >> 26; h1 = (uint)d1 & Mask; d2 += carry;
        carry = d2 >> 26; h2 = (uint)d2 & Mask; d3 += carry;
        carry = d3 >> 26; h3 = (uint)d3 & Mask; d4 += carry;
        carry = d4 >> 26; h4 = (uint)d4 & Mask;
        h0 += (uint)carry * 5;
        h1 += h0 >> 26;
        h0 &= Mask;
    }

    private static uint Le(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
}
