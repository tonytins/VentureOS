using Venture.OS.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Scripting;

public sealed class VentureLibraryTests
{
    private readonly RecordingOutput output = new();

    [Fact]
    public void ReportsTheVersion()
    {
        Run(new FakeSystemInfo(), """
            local v = venture.version()
            print(v.name, v.version, v.build, math.type(v.build), v.commit, v.cosmos)
            print(v.display)
            """);

        Assert.Equal(["VentureOS\t1.2.3\t456\tinteger\tabc1234\t3.0.94", "VentureOS 1.2.3 (Build 456)"], output.Lines);
    }

    [Fact]
    public void ReportsUptimeInWholeSeconds()
    {
        Run(new FakeSystemInfo { Uptime = TimeSpan.FromSeconds(61.9) }, "print(venture.uptime(), math.type(venture.uptime()))");

        Assert.Equal(["61\tinteger"], output.Lines);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-5)]
    public void ReportsUnknownOrNegativeUptimeAsNil(int? seconds)
    {
        TimeSpan? uptime = seconds is null ? null : TimeSpan.FromSeconds(seconds.Value);

        Run(new FakeSystemInfo { Uptime = uptime }, "print(venture.uptime())");

        Assert.Equal(["nil"], output.Lines);
    }

    [Fact]
    public void ReportsMemoryInBytes()
    {
        Run(new FakeSystemInfo { Memory = new(1024, 4096) }, "local m = venture.memory() print(m.free, m.total)");

        Assert.Equal(["1024\t4096"], output.Lines);
    }

    [Fact]
    public void ReportsUnknownOrInconsistentMemoryAsNil()
    {
        Run(new FakeSystemInfo { Memory = null }, "print(venture.memory())");
        Run(new FakeSystemInfo { Memory = new(5000, 4096) }, "print(venture.memory())");

        Assert.Equal(["nil", "nil"], output.Lines);
    }

    [Fact]
    public void KeepsMemoryAboveTheIntegerRangeAsAFloat()
    {
        Run(new FakeSystemInfo { Memory = new(0, ulong.MaxValue) }, "print(math.type(venture.memory().total))");

        Assert.Equal(["float"], output.Lines);
    }

    [Fact]
    public void ReportsTheNetwork()
    {
        Run(new FakeSystemInfo(), """
            local n = venture.net()
            print(n.device, n.mac, n.link_up, n.ready, n.address, n.subnet, n.gateway)
            """);

        Assert.Equal(["e1000e\t52:54:00:12:34:56\ttrue\tfalse\t10.0.2.15\t255.255.255.0\t10.0.2.2"], output.Lines);
    }

    [Fact]
    public void LeavesUnconfiguredAddressesNil()
    {
        var system = new FakeSystemInfo { Network = new("e1000e", "mac", false, false, null, null, null) };

        Run(system, "local n = venture.net() print(n.address, n.subnet, n.gateway)");

        Assert.Equal(["nil\tnil\tnil"], output.Lines);
    }

    [Fact]
    public void ReportsNoNetworkAsNil()
    {
        Run(new FakeSystemInfo { Network = null }, "print(venture.net())");

        Assert.Equal(["nil"], output.Lines);
    }

    [Fact]
    public void PassesTextThroughAsUtf8()
    {
        Run(new FakeSystemInfo { Name = "VéntureOS" }, "local name = venture.version().name print(name, #name)");

        Assert.Equal(["VéntureOS\t10"], output.Lines);
    }

    [Fact]
    public void TurnsAFailingDeviceIntoALuaErrorThatPcallCatches()
    {
        var system = new FakeSystemInfo { UptimeFailure = new InvalidOperationException("timer gone") };

        var finished = Run(system, "local ok, message = pcall(venture.uptime) print(ok, message)");

        Assert.True(finished);
        Assert.StartsWith("false\t", Assert.Single(output.Lines));
        Assert.Contains("timer gone", output.Lines[0]);
    }

    [Fact]
    public void IsLeftOutWithoutSystemInfo()
    {
        LuaRunner.Run(output, Path.GetTempPath(), lua => lua.DoString("print(venture)"));

        Assert.Equal(["nil"], output.Lines);
    }

    [Fact]
    public void RejectsNullArguments()
    {
        using Cosmos.Executable.Lua.LuaInterpreter lua = new();

        Assert.Throws<ArgumentNullException>(() => VentureLibrary.Open(null!, new FakeSystemInfo()));
        Assert.Throws<ArgumentNullException>(() => VentureLibrary.Open(lua.State, null!));
    }

    private bool Run(ISystemInfo system, string code) =>
        LuaRunner.Run(output, Path.GetTempPath(), lua => lua.DoString(code), system);
}
