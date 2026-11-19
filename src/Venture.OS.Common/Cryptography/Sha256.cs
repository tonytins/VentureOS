using System.Buffers.Binary;

namespace Venture.OS.Cryptography;

/// <summary>
/// SHA-256, from FIPS 180-4, written out because Cosmos has no cryptography of its own. It's fed in pieces with
/// <see cref="Append"/> and read once with <see cref="Finish"/>, so HMAC can hash a key's pad and a message without
/// joining them; <see cref="Hash"/> does both at once.
/// </summary>
/// <remarks>
/// Rotations are written out rather than taken from <c>BitOperations</c>, so nothing here depends on what the
/// ahead-of-time compiler supplies beyond plain integer arithmetic.
/// </remarks>
public sealed class Sha256
{
    /// <summary>A digest's length in bytes.</summary>
    public const int HashSize = 32;

    /// <summary>The length of a block, the unit the compression function works in, and HMAC's key pad.</summary>
    public const int BlockSize = 64;

    private static readonly uint[] K =
    [
        0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
        0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
        0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
        0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
        0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
        0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
        0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
        0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2,
    ];

    private readonly uint[] state = new uint[8];
    private readonly uint[] schedule = new uint[64];
    private readonly byte[] buffer = new byte[BlockSize];
    private int buffered;
    private ulong length;
    private bool finished;

    /// <summary>Starts a new digest.</summary>
    public Sha256() => Reset();

    /// <summary>Starts over, as if newly made, so one instance can hash many messages without allocating.</summary>
    public void Reset()
    {
        state[0] = 0x6a09e667;
        state[1] = 0xbb67ae85;
        state[2] = 0x3c6ef372;
        state[3] = 0xa54ff53a;
        state[4] = 0x510e527f;
        state[5] = 0x9b05688c;
        state[6] = 0x1f83d9ab;
        state[7] = 0x5be0cd19;
        buffered = 0;
        length = 0;
        finished = false;
    }

    /// <summary>Adds bytes to the message.</summary>
    /// <exception cref="InvalidOperationException">The digest was already finished; <see cref="Reset"/> first.</exception>
    public void Append(ReadOnlySpan<byte> data)
    {
        if (finished)
            throw new InvalidOperationException("This SHA-256 digest is finished; reset it to hash another message.");

        length += (ulong)data.Length;
        if (buffered > 0)
        {
            var take = Math.Min(BlockSize - buffered, data.Length);
            data[..take].CopyTo(buffer.AsSpan(buffered));
            buffered += take;
            data = data[take..];
            if (buffered < BlockSize)
                return;
            Compress(buffer);
            buffered = 0;
        }

        while (data.Length >= BlockSize)
        {
            Compress(data[..BlockSize]);
            data = data[BlockSize..];
        }

        data.CopyTo(buffer);
        buffered = data.Length;
    }

    /// <summary>Pads the message, as FIPS 180-4 says, and writes its digest.</summary>
    /// <exception cref="ArgumentException"><paramref name="digest"/> is shorter than <see cref="HashSize"/>.</exception>
    /// <exception cref="InvalidOperationException">The digest was already finished.</exception>
    public void Finish(Span<byte> digest)
    {
        if (digest.Length < HashSize)
            throw new ArgumentException($"A SHA-256 digest needs {HashSize} bytes.", nameof(digest));
        if (finished)
            throw new InvalidOperationException("This SHA-256 digest is finished; reset it to hash another message.");

        var bits = length * 8;
        buffer[buffered++] = 0x80;
        if (buffered > BlockSize - 8)
        {
            buffer.AsSpan(buffered).Clear();
            Compress(buffer);
            buffered = 0;
        }

        buffer.AsSpan(buffered, BlockSize - 8 - buffered).Clear();
        BinaryPrimitives.WriteUInt64BigEndian(buffer.AsSpan(BlockSize - 8), bits);
        Compress(buffer);

        for (var index = 0; index < 8; index++)
            BinaryPrimitives.WriteUInt32BigEndian(digest[(index * 4)..], state[index]);
        finished = true;
    }

    /// <summary>The digest of a whole message.</summary>
    public static void Hash(ReadOnlySpan<byte> data, Span<byte> digest)
    {
        Sha256 sha = new();
        sha.Append(data);
        sha.Finish(digest);
    }

    /// <summary>The digest of a whole message, as a new array.</summary>
    public static byte[] Hash(ReadOnlySpan<byte> data)
    {
        var digest = new byte[HashSize];
        Hash(data, digest);
        return digest;
    }

    private void Compress(ReadOnlySpan<byte> block)
    {
        var w = schedule;
        for (var t = 0; t < 16; t++)
            w[t] = BinaryPrimitives.ReadUInt32BigEndian(block[(t * 4)..]);
        for (var t = 16; t < 64; t++)
        {
            var s0 = Rotate(w[t - 15], 7) ^ Rotate(w[t - 15], 18) ^ (w[t - 15] >> 3);
            var s1 = Rotate(w[t - 2], 17) ^ Rotate(w[t - 2], 19) ^ (w[t - 2] >> 10);
            w[t] = w[t - 16] + s0 + w[t - 7] + s1;
        }

        var (a, b, c, d, e, f, g, h) = (state[0], state[1], state[2], state[3], state[4], state[5], state[6], state[7]);
        for (var t = 0; t < 64; t++)
        {
            var sum1 = Rotate(e, 6) ^ Rotate(e, 11) ^ Rotate(e, 25);
            var choose = (e & f) ^ (~e & g);
            var temp1 = h + sum1 + choose + K[t] + w[t];
            var sum0 = Rotate(a, 2) ^ Rotate(a, 13) ^ Rotate(a, 22);
            var majority = (a & b) ^ (a & c) ^ (b & c);
            var temp2 = sum0 + majority;
            (h, g, f, e, d, c, b, a) = (g, f, e, d + temp1, c, b, a, temp1 + temp2);
        }

        state[0] += a;
        state[1] += b;
        state[2] += c;
        state[3] += d;
        state[4] += e;
        state[5] += f;
        state[6] += g;
        state[7] += h;
    }

    private static uint Rotate(uint value, int by) => (value >> by) | (value << (32 - by));
}
