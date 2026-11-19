using System.Text;
using Venture.OS.Cryptography;
using Bcl = System.Security.Cryptography;

namespace Venture.OS.Tests.Cryptography;

/// <summary>
/// The cryptography vdb is sealed with, against each standard's own vectors (FIPS 180-4's examples, RFC 4231,
/// RFC 5869, and RFC 8439), then against .NET's implementations at every length around the block boundaries, since
/// the tests run on ordinary .NET even though VentureOS can't.
/// </summary>
public sealed class CryptographyTests
{
    private const string Sunscreen =
        "Ladies and Gentlemen of the class of '99: If I could offer you only one tip for the future, sunscreen would be it.";

    [Theory]
    [InlineData("", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [InlineData("abc", "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    [InlineData("abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq", "248d6a61d20638b8e5c026930c3e6039a33ce45964ff2167f6ecedd419db06c1")]
    public void Sha256MatchesTheStandardsExamples(string message, string digest)
    {
        Assert.Equal(digest, Hex(Sha256.Hash(Encoding.ASCII.GetBytes(message))));
    }

    [Fact]
    public void Sha256HashesAMillionAsInPieces()
    {
        Sha256 sha = new();
        var chunk = Encoding.ASCII.GetBytes(new string('a', 1000));
        for (var count = 0; count < 1000; count++)
            sha.Append(chunk);
        var digest = new byte[32];
        sha.Finish(digest);

        Assert.Equal("cdc76e5c9914fb9281a1c7e284d73e67f1809a48a497200e046d39ccc7112cd0", Hex(digest));
    }

    [Fact]
    public void Sha256AgreesWithDotNetAtEveryLengthAndInAnyPieces()
    {
        var data = Bytes(300, seed: 1);
        for (var length = 0; length <= data.Length; length++)
        {
            var message = data.AsSpan(0, length);
            var expected = Bcl.SHA256.HashData(message);
            Assert.Equal(expected, Sha256.Hash(message));

            // Fed in uneven pieces, the digest is the same.
            Sha256 sha = new();
            for (var at = 0; at < length; at += 1 + (at % 13))
                sha.Append(message[at..Math.Min(length, at + 1 + (at % 13))]);
            var pieces = new byte[32];
            sha.Finish(pieces);
            Assert.Equal(expected, pieces);
        }
    }

    [Fact]
    public void Sha256RefusesToBeFinishedTwiceUntilReset()
    {
        Sha256 sha = new();
        var digest = new byte[32];
        sha.Finish(digest);

        Assert.Throws<InvalidOperationException>(() => sha.Append([1]));
        Assert.Throws<InvalidOperationException>(() => sha.Finish(digest));

        sha.Reset();
        sha.Append(Encoding.ASCII.GetBytes("abc"));
        sha.Finish(digest);
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", Hex(digest));
    }

    [Theory]
    [InlineData("0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b", "4869205468657265", "b0344c61d8db38535ca8afceaf0bf12b881dc200c9833da726e9376c2e32cff7")]
    [InlineData("4a656665", "7768617420646f2079612077616e7420666f72206e6f7468696e673f", "5bdcc146bf60754e6a042426089575c75a003f089d2739839dec58b964ec3843")]
    public void HmacMatchesRfc4231(string key, string message, string tag)
    {
        var computed = new byte[32];
        HmacSha256.Compute(Convert.FromHexString(key), Convert.FromHexString(message), computed);

        Assert.Equal(tag, Hex(computed));
    }

    [Fact]
    public void HmacHashesAKeyLongerThanABlockFirst()
    {
        var key = Enumerable.Repeat((byte)0xaa, 131).ToArray();
        var computed = new byte[32];
        HmacSha256.Compute(key, Encoding.ASCII.GetBytes("Test Using Larger Than Block-Size Key - Hash Key First"), computed);

        Assert.Equal("60e431591ee0b67f0d8a26aacbf5b77f8e0bc6213728c5140546040f0ee37f54", Hex(computed));
    }

    [Fact]
    public void HmacAgreesWithDotNet()
    {
        var data = Bytes(200, seed: 2);
        foreach (var keyLength in (int[])[0, 1, 32, 63, 64, 65, 131])
        {
            var key = Bytes(keyLength, seed: keyLength);
            for (var length = 0; length <= data.Length; length += 7)
            {
                var computed = new byte[32];
                HmacSha256.Compute(key, data.AsSpan(0, length), computed);
                Assert.Equal(Bcl.HMACSHA256.HashData(key, data.AsSpan(0, length)), computed);
            }
        }
    }

    [Fact]
    public void HkdfMatchesRfc5869WithSaltAndInfo()
    {
        var secret = Enumerable.Repeat((byte)0x0b, 22).ToArray();
        var salt = Convert.FromHexString("000102030405060708090a0b0c");
        var info = Convert.FromHexString("f0f1f2f3f4f5f6f7f8f9");

        var key = new byte[32];
        HkdfSha256.Extract(salt, secret, key);
        var output = new byte[42];
        HkdfSha256.Expand(key, info, output);

        Assert.Equal("077709362c2e32df0ddc3f0dc47bba6390b6c73bb50f9c3122ec844ad7c2b3e5", Hex(key));
        Assert.Equal("3cb25f25faacd57a90434f64d0362f2a2d2d0a90cf1a5a4c5db02d56ecc4c5bf34007208d5b887185865", Hex(output));
    }

    [Fact]
    public void HkdfMatchesRfc5869WithNeitherSaltNorInfo()
    {
        var output = new byte[42];
        HkdfSha256.DeriveKey(Enumerable.Repeat((byte)0x0b, 22).ToArray(), [], [], output);

        Assert.Equal("8da4e775a563c18f715f802a063c5a31b8a11f5c5ee1879ec3454e5f3c738d2d9d201395faa4b61a96c8", Hex(output));
    }

    [Fact]
    public void HkdfAgreesWithDotNetAtEveryLength()
    {
        var secret = Bytes(16, seed: 3);
        var salt = Bytes(32, seed: 4);
        var info = Encoding.ASCII.GetBytes("vdb/1 to venture");
        foreach (var length in (int[])[1, 16, 31, 32, 33, 64, 65, 200])
        {
            var output = new byte[length];
            HkdfSha256.DeriveKey(secret, salt, info, output);
            Assert.Equal(Bcl.HKDF.DeriveKey(Bcl.HashAlgorithmName.SHA256, secret, length, salt, info), output);
        }
    }

    [Fact]
    public void HkdfRefusesMoreThanItCanDerive()
    {
        var key = new byte[32];
        Assert.Throws<ArgumentException>(() => HkdfSha256.Expand(key, [], new byte[HkdfSha256.MaximumLength + 1]));
        Assert.Throws<ArgumentException>(() => HkdfSha256.Expand(new byte[31], [], new byte[1]));
    }

    [Fact]
    public void ChaCha20MatchesRfc8439()
    {
        var key = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
        var nonce = Convert.FromHexString("000000000000004a00000000");
        var plaintext = Encoding.ASCII.GetBytes(Sunscreen);
        var ciphertext = new byte[plaintext.Length];

        ChaCha20Poly1305.Xor(key, nonce, 1, plaintext, ciphertext);

        Assert.Equal(
            "6e2e359a2568f98041ba0728dd0d6981e97e7aec1d4360c20a27afccfd9fae0bf91b65c5524733ab8f593dabcd62b3571639d624e65152ab"
            + "8f530c359f0861d807ca0dbf500d6a6156a38e088a22b65e52bc514d16ccf806818ce91ab77937365af90bbf74a35be6b40b8eedf2785e42874d",
            Hex(ciphertext));
    }

    [Fact]
    public void Poly1305MatchesRfc8439()
    {
        var tag = new byte[16];
        Poly1305.Compute(
            Convert.FromHexString("85d6be7857556d337f4452fe42d506a80103808afb0db2fd4abff6af4149f51b"),
            Encoding.ASCII.GetBytes("Cryptographic Forum Research Group"), tag);

        Assert.Equal("a8061dc1305136c6c22b8baf0c0127a9", Hex(tag));
    }

    [Fact]
    public void ChaCha20Poly1305MatchesRfc8439()
    {
        var key = Enumerable.Range(0x80, 32).Select(value => (byte)value).ToArray();
        var nonce = Convert.FromHexString("070000004041424344454647");
        var data = Convert.FromHexString("50515253c0c1c2c3c4c5c6c7");
        var plaintext = Encoding.ASCII.GetBytes(Sunscreen);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        ChaCha20Poly1305.Seal(key, nonce, plaintext, data, ciphertext, tag);

        Assert.Equal(
            "d31a8d34648e60db7b86afbc53ef7ec2a4aded51296e08fea9e2b5a736ee62d63dbea45e8ca9671282fafb69da92728b1a71de0a9e060b"
            + "2905d6a5b67ecd3b3692ddbd7f2d778b8c9803aee328091b58fab324e4fad675945585808b4831d7bc3ff4def08e4b7a9de576d26586cec64b6116",
            Hex(ciphertext));
        Assert.Equal("1ae10b594f09e26a7e902ecbd0600691", Hex(tag));

        var opened = new byte[ciphertext.Length];
        Assert.True(ChaCha20Poly1305.Open(key, nonce, ciphertext, data, tag, opened));
        Assert.Equal(plaintext, opened);
    }

    [Fact]
    public void ChaCha20Poly1305AgreesWithDotNetAtEveryLength()
    {
        if (!Bcl.ChaCha20Poly1305.IsSupported)
            Assert.Skip(".NET has no ChaCha20-Poly1305 on this machine.");

        var key = Bytes(32, seed: 5);
        var nonce = Bytes(12, seed: 6);
        var data = Bytes(40, seed: 7);
        var message = Bytes(300, seed: 8);
        using Bcl.ChaCha20Poly1305 reference = new(key);
        for (var length = 0; length <= message.Length; length++)
        {
            foreach (var dataLength in (int[])[0, 15, 16, 17, 40])
            {
                var plaintext = message.AsSpan(0, length);
                var associated = data.AsSpan(0, dataLength);
                var expected = new byte[length];
                var expectedTag = new byte[16];
                reference.Encrypt(nonce, plaintext, expected, expectedTag, associated);

                var ciphertext = new byte[length];
                var tag = new byte[16];
                ChaCha20Poly1305.Seal(key, nonce, plaintext, associated, ciphertext, tag);

                Assert.Equal(expected, ciphertext);
                Assert.Equal(expectedTag, tag);
            }
        }
    }

    [Theory]
    [InlineData("ciphertext")]
    [InlineData("tag")]
    [InlineData("data")]
    [InlineData("nonce")]
    public void ChaCha20Poly1305RefusesAnythingChangedAndRevealsNothing(string changed)
    {
        var key = Bytes(32, seed: 9);
        var nonce = Bytes(12, seed: 10);
        var data = Bytes(5, seed: 11);
        var plaintext = Encoding.ASCII.GetBytes("install Dice.app");
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        ChaCha20Poly1305.Seal(key, nonce, plaintext, data, ciphertext, tag);

        var target = changed switch
        {
            "ciphertext" => ciphertext,
            "tag" => tag,
            "data" => data,
            _ => nonce,
        };
        target[0] ^= 1;

        var opened = Enumerable.Repeat((byte)0xff, plaintext.Length).ToArray();
        Assert.False(ChaCha20Poly1305.Open(key, nonce, ciphertext, data, tag, opened));
        Assert.All(opened, value => Assert.Equal(0, value));
    }

    [Fact]
    public void ChaCha20Poly1305RefusesWrongSizes()
    {
        var output = new byte[1];
        var tag = new byte[16];
        Assert.Throws<ArgumentException>(() => ChaCha20Poly1305.Seal(new byte[31], new byte[12], [1], [], output, tag));
        Assert.Throws<ArgumentException>(() => ChaCha20Poly1305.Seal(new byte[32], new byte[8], [1], [], output, tag));
        Assert.Throws<ArgumentException>(() => ChaCha20Poly1305.Seal(new byte[32], new byte[12], [1], [], output, new byte[15]));
        Assert.Throws<ArgumentException>(() => ChaCha20Poly1305.Seal(new byte[32], new byte[12], [1, 2], [], output, tag));
    }

    private static string Hex(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(bytes);

    private static byte[] Bytes(int length, int seed)
    {
        var bytes = new byte[length];
        new Random(seed).NextBytes(bytes);
        return bytes;
    }
}
