using Venture.OS.Cryptography;
using Venture.OS.Vdb;

namespace Venture.OS.Tests.Vdb;

/// <summary>The sealed channel, key IDs, VentureOS's nonces, and hexadecimal, apart from a session.</summary>
public sealed class VdbChannelTests
{
    private static readonly byte[] Client = Convert.FromHexString("00112233445566778899aabbccddeeff");
    private static readonly byte[] Server = Convert.FromHexString("f0f1f2f3f4f5f6f7f8f9fafbfcfdfeff");

    [Fact]
    public void AKeyIdIsTheHashOfTheIdentityAndGivesNothingElseAway()
    {
        // As docs/vdb/sealed-lines.py works it out, with Python's hashlib.
        Assert.Equal("0a30b19247f0d8d7", VdbTrust.KeyId(TestComputer.DefaultIdentity));
        Assert.True(VdbTrust.IsKeyId(VdbTrust.KeyId("fedcba9876543210fedcba9876543210")));
        Assert.Throws<ArgumentException>(() => VdbTrust.KeyId("not one"));
    }

    [Fact]
    public void EachSideOpensWhatTheOtherSeals()
    {
        var venture = VdbChannel.ForVentureOS(TestComputer.DefaultIdentity, Client, Server);
        var computer = VdbChannel.ForComputer(TestComputer.DefaultIdentity, Client, Server);

        Assert.Equal("install Dice.app", venture.Open(computer.Seal("install Dice.app")));
        Assert.Equal("ok", computer.Open(venture.Seal("ok")));
        Assert.Equal("", venture.Open(computer.Seal("")));
    }

    [Fact]
    public void ALineSealedOneWayDoesNotOpenTheOther()
    {
        var computer = VdbChannel.ForComputer(TestComputer.DefaultIdentity, Client, Server);

        Assert.Null(computer.Open(computer.Seal("ok")));
    }

    [Fact]
    public void TheLongestLineSealsAndOpens()
    {
        var venture = VdbChannel.ForVentureOS(TestComputer.DefaultIdentity, Client, Server);
        var computer = VdbChannel.ForComputer(TestComputer.DefaultIdentity, Client, Server);
        var longest = new string('a', VdbProtocol.MaximumLineLength);

        var line = computer.Seal(longest);

        Assert.Equal(VdbChannel.MaximumLineLength, line.Length);
        Assert.Equal(longest, venture.Open(line));
        Assert.Throws<ArgumentException>(() => computer.Seal(longest + "a"));
        Assert.Throws<ArgumentException>(() => computer.Seal("two\nlines"));
    }

    [Theory]
    [InlineData("install Dice.app")]
    [InlineData("sealed")]
    [InlineData("sealed !!!!")]
    [InlineData("sealed AAAA")]
    public void WhatIsNotASealedLineDoesNotOpen(string line)
    {
        Assert.Null(VdbChannel.ForVentureOS(TestComputer.DefaultIdentity, Client, Server).Open(line));
    }

    [Fact]
    public void VentureOSNoncesNeverRepeatEvenWithAStoppedClock()
    {
        VdbRandom random = new(() => 42, [1, 2, 3]);
        HashSet<string> seen = [];
        var nonce = new byte[16];
        for (var count = 0; count < 1000; count++)
        {
            random.Fill(nonce);
            Assert.True(seen.Add(Hex.Encode(nonce)));
        }
    }

    [Fact]
    public void VentureOSNoncesDifferFromBootToBoot()
    {
        var first = new byte[16];
        var second = new byte[16];
        new VdbRandom(() => 42, [1]).Fill(first);
        new VdbRandom(() => 42, [2]).Fill(second);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void HexGoesBothWaysAndRefusesWhatIsNotLowercase()
    {
        var bytes = new byte[] { 0x00, 0x7f, 0xab, 0xff };
        var decoded = new byte[4];

        Hex.Decode(Hex.Encode(bytes), decoded);

        Assert.Equal("007fabff", Hex.Encode(bytes));
        Assert.Equal(bytes, decoded);
        Assert.Throws<ArgumentException>(() => Hex.Decode("007FABFF", decoded));
        Assert.False(Hex.IsLowercase("00g0", 4));
    }
}
