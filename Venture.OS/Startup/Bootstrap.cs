namespace Venture.OS.Startup;

internal static class Bootstrap
{
    private const string RamFilesystemName = "ramfat";
    private const string RamMountPoint = "/mnt";

    /// <summary>
    /// Formats a 32 MiB RAM disk as FAT16 and mounts it at <c>/mnt</c>, reporting the step that fails.
    /// </summary>
    /// <returns><see langword="true"/> if the disk is mounted.</returns>
    public static bool BootIntoRam()
    {
        Console.WriteLine("Initializing filesystem...");

        BootRamDisk ramdisk = new("RAMDISK", 512, 65536); // 32 MiB
        FatFileSystemType fat = new(ramdisk);

        if (!VfsManager.RegisterFileSystem(RamFilesystemName, fat))
        {
            Console.WriteLine("RAM disk setup failed: couldn't register the filesystem.");
            return false;
        }

        if (!VfsManager.TryFormat(RamFilesystemName, "", new FatFormatOptions { Type = FatType.Fat16 }))
        {
            Console.WriteLine("RAM disk setup failed: couldn't format the disk as FAT16.");
            return false;
        }

        if (!VfsManager.TryMount(RamFilesystemName, "", MountFlags.None, RamMountPoint, out _))
        {
            Console.WriteLine($"RAM disk setup failed: couldn't mount the disk at {RamMountPoint}.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Confirms that Cosmos initialized the timer manager.
    /// </summary>
    /// <returns><see langword="true"/> if the timer manager is ready.</returns>
    public static bool CheckTimeManager()
    {
        Console.WriteLine("Initializing time manager...");
        if (TimerManager.IsInitialized)
            return true;

        Console.WriteLine("Time manager setup failed.");
        return false;
    }

    /// <summary>
    /// Records the primary network device in <see cref="HostInfo"/> and requests an address over DHCP.
    /// Skips DHCP when no network device is present.
    /// </summary>
    public static void ConnectToNetwork()
    {
        Console.WriteLine("Initializing network...");

        if (!HostInfo.IsLinkedToNetwork)
        {
            Console.WriteLine("No network device found; skipping network setup.");
            return;
        }

        var networkInfo = $"""
                           Device:  {NetworkManager.Name}
                           MAC:     {NetworkManager.MacAddress}
                           Link up: {NetworkManager.LinkUp}
                           Ready:   {NetworkManager.Ready}
                           """;
        HostInfo.MacAddress = NetworkManager.MacAddress?.ToString();
        HostInfo.Domain = NetworkManager.Name;
        Console.WriteLine(networkInfo);

        DhcpClient dhcpClient = new();

        // SendDiscoverPacket returns -1 when no DHCP server answers in time.
        if (dhcpClient.SendDiscoverPacket() == -1)
        {
            Console.WriteLine("DHCP request timed out; no IP address assigned.");
            return;
        }

        if (!HostInfo.HasIp)
        {
            Console.WriteLine("DHCP finished without assigning an IP address.");
            return;
        }

        var config = NetworkManager.Primary.IPConfig;
        var dhcpInfo = $"""
                        IP address: {config?.Address}
                        Subnet: {config?.SubnetMask}
                        Gateway: {config?.DefaultGateway}
                        """;
        HostInfo.IpAddress = config?.Address.ToString();
        Console.WriteLine(dhcpInfo);
    }
}
