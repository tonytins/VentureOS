namespace Venture.OS.Kernel;

/// <summary>
/// Writes system messages to both the serial port and the console.
/// </summary>
/// <remarks>
/// Use it for boot step results, the boot banner, and failures, so a headless boot (or CI) can read them from the
/// serial log. Ordinary command output belongs on the console only.
/// </remarks>
internal static class KernelLog
{
    /// <summary>The prefix on every serial line, which tells VentureOS's messages apart from Cosmos's own.</summary>
    public const string SerialPrefix = "[Venture] ";

    /// <summary>
    /// Writes <paramref name="message"/> to serial, then to the console.
    /// </summary>
    /// <remarks>
    /// Serial comes first so the message survives even if the console is what failed. Each line of a multi-line
    /// message gets the serial prefix.
    /// </remarks>
    public static void WriteLine(string message)
    {
        foreach (var line in message.Split('\n'))
        {
            Log.WriteString(SerialPrefix);
            Log.WriteString(line);
            Log.WriteString("\n");
        }

        Console.WriteLine(message);
    }
}
