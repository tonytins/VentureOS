namespace Venture.OS.Vdb;

/// <summary>
/// The fixed values of vdb/1, the protocol a computer uses to send apps to a running VentureOS, as
/// <c>docs/design/2026-11-18-vdb-design.md</c> sets them. The vdb tool keeps the same values; a change here is a
/// change to the protocol, so it changes there and in the design too.
/// </summary>
public static class VdbProtocol
{
    /// <summary>The version VentureOS speaks, as <c>hello</c> names it.</summary>
    public const string Version = "vdb/1";

    /// <summary>The port VentureOS listens on: five digits, so it meets nothing else's, adb's included.</summary>
    public const int DefaultPort = 28455;

    /// <summary>The longest line either side sends, in bytes of UTF-8, not counting its line break.</summary>
    public const int MaximumLineLength = 8192;

    /// <summary>
    /// The most bytes one <c>data</c> line carries: the most whose base64, after <c>data </c>, fits in a line.
    /// </summary>
    public const int MaximumDataBytes = (MaximumLineLength - 5) / 4 * 3;

    /// <summary>The largest file in a bundle, once decoded.</summary>
    public const long MaximumFileSize = 4 * 1024 * 1024;

    /// <summary>The largest bundle, all its files together, once decoded.</summary>
    public const long MaximumBundleSize = 16 * 1024 * 1024;

    /// <summary>The most files in one bundle.</summary>
    public const int MaximumFiles = 256;

    /// <summary>The longest path inside a bundle, in characters.</summary>
    public const int MaximumPathLength = 255;

    /// <summary>How long a connection may say nothing before VentureOS closes it.</summary>
    public static readonly TimeSpan IdleLimit = TimeSpan.FromSeconds(30);

    /// <summary>How long the Trust alert waits for an answer before it counts as Deny.</summary>
    public static readonly TimeSpan TrustLimit = TimeSpan.FromSeconds(60);
}
