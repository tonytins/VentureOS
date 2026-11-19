using System.Text;

namespace Venture.OS.Vdb;

/// <summary>
/// Splits what arrives on a vdb connection into lines, however the bytes are cut up on the way. A line ends at
/// <c>\n</c>, with a <c>\r</c> before it dropped, and must be UTF-8 text no longer than the reader's limit,
/// <see cref="VdbProtocol.MaximumLineLength"/> bytes unless it's given another, such as a sealed line's; anything
/// else is a <see cref="Problem"/>, after which the reader takes nothing more, since there's no telling where the next
/// line starts.
/// </summary>
public sealed class VdbLineReader
{
    private static readonly UTF8Encoding Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    // One more than a line may hold, for the \r a line may end with.
    private readonly byte[] line;
    private readonly Queue<string> lines = new();
    private readonly int maximum;
    private int length;

    /// <param name="maximumLength">The longest line, in bytes, not counting its break.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumLength"/> isn't positive.</exception>
    public VdbLineReader(int maximumLength = VdbProtocol.MaximumLineLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumLength);
        maximum = maximumLength;
        line = new byte[maximumLength + 1];
    }

    /// <summary>Why the reader stopped, or <see langword="null"/> while it reads.</summary>
    public string? Problem { get; private set; }

    /// <summary>Whether a whole line is waiting.</summary>
    public bool HasLine => lines.Count > 0;

    /// <summary>Takes in bytes as they arrived, keeping any part of a line for the bytes that finish it.</summary>
    public void Feed(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            if (Problem is not null)
                return;

            if (value == (byte)'\n')
            {
                Finish();
                continue;
            }

            if (length == line.Length)
            {
                Problem = $"a line is longer than {maximum} bytes";
                return;
            }

            line[length++] = value;
        }
    }

    /// <summary>Takes the next whole line, or <see langword="null"/> when none is waiting.</summary>
    public string? Take() => lines.TryDequeue(out var next) ? next : null;

    private void Finish()
    {
        var end = length > 0 && line[length - 1] == (byte)'\r' ? length - 1 : length;
        length = 0;
        if (end > maximum)
        {
            Problem = $"a line is longer than {maximum} bytes";
            return;
        }

        try
        {
            lines.Enqueue(Strict.GetString(line, 0, end));
        }
        catch (DecoderFallbackException)
        {
            Problem = "a line isn't UTF-8 text";
        }
    }
}
