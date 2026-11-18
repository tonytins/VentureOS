using Venture.Roundhouse.Simulator;

namespace Venture.Roundhouse.Tests;

/// <summary>The simulator's disk, a fresh copy of VentureOS's files for each run, and what it tells scripts.</summary>
public sealed class SimulatorTests
{
    [Fact]
    public void ADiskCopiesTheSystemAndUserFilesWhereTheKernelPutsThemAndGoesWhenDisposed()
    {
        var rootfs = SimulatedDisk.FindRootfs()!;
        string root;
        using (var disk = SimulatedDisk.Create(rootfs))
        {
            root = disk.Root;
            Assert.True(File.Exists(Path.Combine(root, "mnt", "system", "init.lua")));
            Assert.True(File.Exists(disk.HostPath("/mnt/user/settings/appearance.xml")));
            Assert.Equal(
                Directory.GetFiles(Path.Combine(rootfs, "user"), "*", SearchOption.AllDirectories).Length,
                Directory.GetFiles(disk.HostPath(SimulatedDisk.UserFolder), "*", SearchOption.AllDirectories).Length);
        }

        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void EachRunGetsItsOwnCopy()
    {
        var rootfs = SimulatedDisk.FindRootfs()!;
        using var first = SimulatedDisk.Create(rootfs);
        using var second = SimulatedDisk.Create(rootfs);

        File.WriteAllText(first.HostPath("/mnt/user/note.txt"), "only in the first");

        Assert.NotEqual(first.Root, second.Root);
        Assert.False(File.Exists(second.HostPath("/mnt/user/note.txt")));
    }

    [Fact]
    public void CopiesLeftByAnEndedRoundhouseAreClearedAndARunningOnesKept()
    {
        // No process has this ID: process IDs stay well below it on every system Roundhouse runs on.
        var ended = Path.Combine(Path.GetTempPath(), $"{SimulatedDisk.Prefix}2147483646-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(ended, "mnt"));
        using var running = SimulatedDisk.Create(SimulatedDisk.FindRootfs()!);

        Assert.False(Directory.Exists(ended));
        Assert.True(Directory.Exists(running.Root));
        Assert.StartsWith($"{SimulatedDisk.Prefix}{Environment.ProcessId}-", Path.GetFileName(running.Root));
    }

    [Fact]
    public void AFolderWithoutVentureOSsFilesIsRefused()
    {
        Assert.Throws<DirectoryNotFoundException>(() => SimulatedDisk.Create(Path.GetTempPath()));
    }

    [Theory]
    [InlineData("mnt/user")]
    [InlineData("/mnt/../../etc")]
    [InlineData("/..")]
    public void OnlyAbsoluteVentureOSPathsThatStayInsideAreMapped(string path)
    {
        using var disk = SimulatedDisk.Create(SimulatedDisk.FindRootfs()!);

        Assert.Throws<ArgumentException>(() => disk.HostPath(path));
    }

    [Fact]
    public void TheSystemReportsWhatTheKernelBuiltFromThisCommitWould()
    {
        using var disk = SimulatedDisk.Create(SimulatedDisk.FindRootfs()!);
        SimulatedSystem system = new(disk);

        Assert.Equal("VentureOS", system.Name);
        Assert.Matches(@"^0\.6\.\d+$", system.Version);
        Assert.Matches("^[0-9a-f]{7,}$", system.Commit);
        var commit = System.Text.Encoding.UTF8.GetBytes(system.Commit);
        Assert.Equal(commit.Length < 4 ? 0 : BitConverter.ToUInt32(commit, 0) % 1_000_000, system.BuildNumber);
        Assert.Equal("/mnt/system", system.SystemDirectory);
        Assert.NotNull(system.WallClock!.UtcNow);
        Assert.NotNull(system.Space("/mnt"));
        Assert.Contains("simulated", system.CosmosVersion);
    }
}
