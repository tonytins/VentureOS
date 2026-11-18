/// The fixed values of vdb/1, as `docs/design/2026-11-18-vdb-design.md` sets them.
///
/// VentureOS keeps the same values in `Venture.OS.Common/Vdb/VdbProtocol.cs`; a change to one is a change to the
/// protocol, so it changes in both and in the design.
public enum VdbProtocol {
    /// The version vdb speaks, as `hello` names it.
    public static let version = "vdb/1"

    /// The port VentureOS listens on: five digits, so it meets nothing else's, adb's included.
    public static let defaultPort = 28455

    /// The longest line either side sends, in bytes of UTF-8, not counting its line break.
    public static let maximumLineLength = 8192

    /// The most bytes one `data` line carries: the most whose base64, after `data `, fits in a line.
    public static let maximumDataBytes = (maximumLineLength - 5) / 4 * 3

    /// The largest file in a bundle, once decoded.
    public static let maximumFileSize = 4 * 1024 * 1024

    /// The largest bundle, all its files together, once decoded.
    public static let maximumBundleSize = 16 * 1024 * 1024

    /// The most files in one bundle.
    public static let maximumFiles = 256

    /// The longest path inside a bundle, in characters.
    public static let maximumPathLength = 255

    /// How long a connection may say nothing before VentureOS closes it, in seconds.
    public static let idleLimitSeconds: Double = 30

    /// How long VentureOS's Trust alert waits for an answer before it counts as Deny, in seconds.
    public static let trustLimitSeconds: Double = 60
}
