namespace Venture.OS;

internal struct HostInfo
{
    /// <summary>
    /// The name of the operating system.
    /// </summary>
    public const string Name = "VentureOS";

    /// <summary>
    /// The version of the operating system using the ThisAssembly.Git.SemVer object.
    /// </summary>
    public const string Version = $"{ThisAssembly.Git.SemVer.Major}.{ThisAssembly.Git.SemVer.Minor}.{ThisAssembly.Git.SemVer.Patch}";

    /// <summary>
    /// The name, version, and build number as shown to the user, such as <c>VentureOS 0.1.116 (Build 123456)</c>.
    /// </summary>
    public static string DisplayVersion => $"{Name} {Version} (Build {BuildNumber})";

    public static string? IpAddress { get; set; } = string.Empty;

    public static string? MacAddress { get; set; } = string.Empty;

    /// <summary>
    ///  Host's name given by the user.
    /// </summary>
    public static string? Domain { get; set; } = string.Empty;

    public static bool IsLinkedToNetwork => NetworkManager.DeviceCount > 0;

    public static bool HasIp => IsLinkedToNetwork && NetworkManager.Primary.IPConfig is not null;

    /// <summary>
    /// A build number of up to six digits, derived from the commit hash.
    /// </summary>
    /// <remarks>
    /// The first four characters of the hash are read as a little-endian integer and reduced modulo 1,000,000.
    /// The number identifies a build but doesn't increase over time. It's 0 when the hash is unavailable.
    /// </remarks>
    public static uint BuildNumber
    {
        get
        {
            var commit = Encoding.UTF8.GetBytes(ThisAssembly.Git.Commit);
            return commit.Length < sizeof(uint) ? 0 : BitConverter.ToUInt32(commit, 0) % 1_000_000;
        }
    }
}
