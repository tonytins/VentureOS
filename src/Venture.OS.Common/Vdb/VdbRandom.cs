using System.Buffers.Binary;
using Venture.OS.Cryptography;

namespace Venture.OS.Vdb;

/// <summary>
/// VentureOS's nonces for vdb, as good as Cosmos allows: a pool hashed with SHA-256 along with a fine clock read at
/// every use and a count, so no two nonces are ever the same, and none can be guessed without the clock's exact
/// value. It is not a cryptographic random generator. The keys stay fresh even if it's weak, since the computer's
/// nonce, from its own system's generator, goes into them too.
/// </summary>
public sealed class VdbRandom
{
    private readonly Func<long> clock;
    private readonly byte[] pool = new byte[Sha256.HashSize];
    private readonly byte[] input = new byte[Sha256.HashSize + 17];
    private readonly Sha256 sha = new();
    private ulong count;

    /// <param name="clock">A clock as fine as the system has, such as the CPU's timestamp counter.</param>
    /// <param name="seed">Whatever differs from boot to boot, such as the time of day and the clock at boot.</param>
    public VdbRandom(Func<long> clock, ReadOnlySpan<byte> seed)
    {
        ArgumentNullException.ThrowIfNull(clock);
        this.clock = clock;
        Sha256.Hash(seed, pool);
    }

    /// <summary>Fills <paramref name="output"/> with bytes no earlier call gave.</summary>
    public void Fill(Span<byte> output)
    {
        Span<byte> block = stackalloc byte[Sha256.HashSize];
        while (output.Length > 0)
        {
            // The pool moves on with each block, and what's handed out is a hash of it, never the pool itself.
            pool.CopyTo(input, 0);
            BinaryPrimitives.WriteUInt64LittleEndian(input.AsSpan(Sha256.HashSize), count++);
            BinaryPrimitives.WriteInt64LittleEndian(input.AsSpan(Sha256.HashSize + 8), clock());
            input[^1] = 0;
            sha.Reset();
            sha.Append(input);
            sha.Finish(pool);

            input[^1] = 1;
            sha.Reset();
            sha.Append(input);
            sha.Finish(block);

            var take = Math.Min(block.Length, output.Length);
            block[..take].CopyTo(output);
            output = output[take..];
        }

        block.Clear();
    }
}
