namespace Venture.OS.Startup;

/// <summary>
/// A block device backed by an in-memory buffer. Its contents are lost when the system halts.
/// </summary>
internal sealed class BootRamDisk : IBlockDevice
{
    private readonly byte[] storage;

    public string Name { get; }
    public ulong BlockSize { get; }
    public ulong BlockCount { get; }

    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="blockSize"/> or <paramref name="blockCount"/> is zero, or the disk is too large for one buffer.
    /// </exception>
    public BootRamDisk(string name, ulong blockSize, ulong blockCount)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (blockSize == 0)
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be greater than zero.");
        if (blockCount == 0)
            throw new ArgumentOutOfRangeException(nameof(blockCount), "Block count must be greater than zero.");

        // Dividing instead of multiplying keeps the size check itself from overflowing.
        if (blockCount > (ulong)Array.MaxLength / blockSize)
            throw new ArgumentOutOfRangeException(nameof(blockCount), "The disk is larger than a single buffer can hold.");

        Name = name;
        BlockSize = blockSize;
        BlockCount = blockCount;
        storage = new byte[blockSize * blockCount];
    }

    void IBlockDevice.ReadBlock(ulong blockNo, ulong blockCount, Span<byte> data)
    {
        var (offset, length) = GetByteRange(blockNo, blockCount, data.Length);
        storage.AsSpan(offset, length).CopyTo(data);
    }

    void IBlockDevice.WriteBlock(ulong blockNo, ulong blockCount, ReadOnlySpan<byte> data)
    {
        var (offset, length) = GetByteRange(blockNo, blockCount, data.Length);
        data[..length].CopyTo(storage.AsSpan(offset, length));
    }

    void IBlockDevice.Flush() { }

    /// <summary>
    /// Converts a block request into a byte range within <see cref="storage"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The request runs past the end of the disk.</exception>
    /// <exception cref="ArgumentException">The caller's buffer is smaller than the requested blocks.</exception>
    private (int Offset, int Length) GetByteRange(ulong blockNo, ulong blockCount, int bufferLength)
    {
        // Compare in blocks first so the byte arithmetic below can't overflow.
        if (blockNo > BlockCount || blockCount > BlockCount - blockNo)
            throw new ArgumentOutOfRangeException(nameof(blockNo), "The request runs past the end of the disk.");

        // Both values fit in an int because the constructor capped the whole disk at Array.MaxLength.
        var offset = (int)(blockNo * BlockSize);
        var length = (int)(blockCount * BlockSize);

        if (bufferLength < length)
            throw new ArgumentException("The buffer is smaller than the requested blocks.", "data");

        return (offset, length);
    }
}
