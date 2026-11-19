namespace Venture.OS.Cryptography;

/// <summary>
/// Lowercase hexadecimal, as vdb writes identities, key IDs, and nonces, done by hand rather than with
/// <c>Convert</c>'s, which the kernel's pinned runtime isn't known to have.
/// </summary>
public static class Hex
{
    private const string Digits = "0123456789abcdef";

    /// <summary>Whether a text is lowercase hexadecimal of exactly <paramref name="length"/> characters.</summary>
    public static bool IsLowercase(string? text, int length)
    {
        if (text is null || text.Length != length)
            return false;
        foreach (var character in text)
        {
            if (character is not ((>= '0' and <= '9') or (>= 'a' and <= 'f')))
                return false;
        }

        return true;
    }

    /// <summary>Bytes as lowercase hexadecimal.</summary>
    public static string Encode(ReadOnlySpan<byte> bytes)
    {
        var characters = new char[bytes.Length * 2];
        for (var index = 0; index < bytes.Length; index++)
        {
            characters[index * 2] = Digits[bytes[index] >> 4];
            characters[(index * 2) + 1] = Digits[bytes[index] & 0xf];
        }

        return new string(characters);
    }

    /// <summary>Lowercase hexadecimal as bytes.</summary>
    /// <exception cref="ArgumentException">The text isn't lowercase hexadecimal twice as long as <paramref name="bytes"/>.</exception>
    public static void Decode(string text, Span<byte> bytes)
    {
        if (!IsLowercase(text, bytes.Length * 2))
            throw new ArgumentException($"\"{text}\" isn't {bytes.Length * 2} lowercase hexadecimal characters.", nameof(text));
        for (var index = 0; index < bytes.Length; index++)
            bytes[index] = (byte)((Value(text[index * 2]) << 4) | Value(text[(index * 2) + 1]));
    }

    private static int Value(char digit) => digit <= '9' ? digit - '0' : digit - 'a' + 10;
}
