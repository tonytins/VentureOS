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
            print(n.device, n.mac, n.link_up, n.ready, n.address, n.subnet, n.gateway, n.dns)
            """);

        Assert.Equal(["e1000e\t52:54:00:12:34:56\ttrue\tfalse\t10.0.2.15\t255.255.255.0\t10.0.2.2\t10.0.2.3"], output.Lines);
    }

    [Fact]
    public void ResolvesAndPings()
    {
        FakeNetworkTools network = new();
        Run(new FakeSystemInfo { NetworkTools = network }, """
            print(venture.resolve("example.com"))
            print(venture.ping("10.0.2.2", 5), math.type(venture.ping("10.0.2.2")))
            print(venture.ping("10.0.2.9"))
            """);

        Assert.Equal(["93.184.216.34", "12\tinteger", "nil\tno reply within 2 s"], output.Lines);
        Assert.Equal(
            [("resolve", "example.com", TimeSpan.FromSeconds(3)), ("ping", "10.0.2.2", TimeSpan.FromSeconds(5)),
                ("ping", "10.0.2.2", TimeSpan.FromSeconds(2)), ("ping", "10.0.2.9", TimeSpan.FromSeconds(2))],
            network.Calls);
    }

    [Theory]
    [InlineData("""print(venture.resolve("bad name"))""", "isn't a host name")]
    [InlineData("""print(venture.resolve("example.com", 30))""", "from 1 to 10")]
    [InlineData("""print(venture.ping("10.0.2.2", 0))""", "from 1 to 10")]
    public void SaysWhyItWontAsk(string code, string reason)
    {
        FakeNetworkTools network = new();
        Run(new FakeSystemInfo { NetworkTools = network }, code);

        Assert.StartsWith("nil\t", output.Lines.Single());
        Assert.Contains(reason, output.Lines.Single());
        Assert.Empty(network.Calls);
    }

    [Fact]
    public void SaysWhenThereIsNoNetworkToAsk()
    {
        Run(new FakeSystemInfo { NetworkTools = null }, """print(venture.resolve("example.com")) print(venture.ping("10.0.2.2"))""");

        Assert.Equal(["nil\tthere's no network device", "nil\tthere's no network device"], output.Lines);
    }

    [Fact]
    public void ResolvingAndPingingNeedTheNetworkScope()
    {
        Assert.False(Run(new FakeSystemInfo(), """venture.resolve("example.com")""", ScriptScopes.System));
        Assert.Contains(output.Lines, line => line.Contains("network scope", StringComparison.Ordinal));
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

    [Theory]
    [InlineData("uptime", ScriptScopes.System)]
    [InlineData("memory", ScriptScopes.System)]
    [InlineData("net", ScriptScopes.Network)]
    public void RefusesFunctionsOutsideTheDeclaredScopes(string function, ScriptScopes scope)
    {
        var finished = Run(new FakeSystemInfo(), $"print(pcall(venture.{function}))", ScriptScopes.All & ~scope);

        Assert.True(finished);
        Assert.Equal([$"false\t{VentureLibrary.UndeclaredMessage(function, scope)}"], output.Lines);
    }

    [Fact]
    public void GivesTheTimeWithTheOffset()
    {
        FakeSystemInfo system = new();
        system.WallClock!.SetUtcOffset(TimeSpan.FromHours(-5));

        Run(system, """
            local t = venture.time()
            print(t.set, t.text, t.offset, t.zone)
            print(t.year, t.month, t.day, t.hour, t.minute, t.second, t.weekday, t.utc)
            """);

        Assert.Equal(
            ["true\tTue 3:45 PM\t-05:00\tUTC-05:00", "2026\t11\t17\t15\t45\t30\tTue\t2026-11-17T20:45:30Z"],
            output.Lines);
    }

    [Fact]
    public void SaysWhenTheHardwareClockIsntSet()
    {
        Run(new FakeSystemInfo { WallClock = new(() => DateTime.UnixEpoch) }, "local t = venture.time() print(t.set, t.text, t.zone)");
        Run(new FakeSystemInfo { WallClock = null }, "print(venture.time())");

        Assert.Equal(["false\tnil\tUTC", "nil"], output.Lines);
    }

    [Fact]
    public void SetsTheOffsetOrSaysWhyNot()
    {
        FakeSystemInfo system = new();

        Run(system, """
            print(venture.setUtcOffset("+5:30"))
            print(venture.setUtcOffset("EST"))
            print(venture.time().offset)
            """);
        Run(new FakeSystemInfo { WallClock = null }, """print(venture.setUtcOffset("-05:00"))""");

        Assert.Equal(TimeSpan.FromMinutes(330), system.WallClock!.UtcOffset);
        Assert.Equal(
            ["true", "nil\tan offset from UTC is written like -05:00 or +5:30, not \"EST\"", "+05:30", "nil\tthis system has no clock to set"],
            output.Lines);
    }

    [Theory]
    [InlineData("time")]
    [InlineData("setUtcOffset")]
    public void TheClockNeedsTheSystemScope(string function)
    {
        Run(new FakeSystemInfo(), $"print(pcall(venture.{function}, \"0\"))", ScriptScopes.All & ~ScriptScopes.System);

        Assert.Equal([$"false\t{VentureLibrary.UndeclaredMessage(function, ScriptScopes.System)}"], output.Lines);
    }

    [Fact]
    public void AlwaysGivesTheVersion()
    {
        Run(new FakeSystemInfo(), "print(venture.version().version)", ScriptScopes.None);

        Assert.Equal(["1.2.3"], output.Lines);
    }

    [Fact]
    public void GivesDeclaredScopesOnly()
    {
        Run(new FakeSystemInfo(), "print(pcall(venture.uptime)) print((pcall(venture.net)))", ScriptScopes.System);

        Assert.Equal(["true\t3725", "false"], output.Lines);
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

    private bool Run(ISystemInfo system, string code, ScriptScopes scopes = ScriptScopes.All) =>
        LuaRunner.Run(output, Path.GetTempPath(), lua => lua.DoString(code), system, scopes: scopes);
}
