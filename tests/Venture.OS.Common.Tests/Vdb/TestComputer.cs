using System.Text;
using Venture.OS.Cryptography;
using Venture.OS.Vdb;

namespace Venture.OS.Tests.Vdb;

/// <summary>
/// The computer's side of a vdb connection, for tests: it says hello with its key ID and a nonce, sends its identity
/// when asked to pair, and seals and opens lines once VentureOS answers hello, as the vdb tool does. What VentureOS
/// sends is kept as the computer reads it, opened, in <see cref="Heard"/>.
/// </summary>
internal sealed class TestComputer
{
    /// <summary>The identity the tests use, unless they say otherwise.</summary>
    public const string DefaultIdentity = "0123456789abcdef0123456789abcdef";

    /// <summary>The computer's nonce the tests use, unless they say otherwise.</summary>
    public const string DefaultNonce = "00112233445566778899aabbccddeeff";

    private VdbChannel? channel;

    public TestComputer(string identity = DefaultIdentity, string nonce = DefaultNonce)
    {
        Identity = identity;
        Nonce = nonce;
        KeyId = VdbTrust.KeyId(identity);
    }

    public string Identity { get; }

    public string KeyId { get; }

    public string Nonce { get; }

    /// <summary>What VentureOS sent, as the computer reads it: plain before hello's answer, opened after.</summary>
    public List<string> Heard { get; } = [];

    /// <summary>Lines VentureOS sent that didn't open, which should never happen.</summary>
    public List<string> Unopened { get; } = [];

    /// <summary>Whether the connection is sealed yet.</summary>
    public bool IsSealed => channel is not null;

    /// <summary>The hello this computer says.</summary>
    public string Hello(string features = "") =>
        $"hello {VdbProtocol.Version} {KeyId} {Nonce}{(features.Length > 0 ? " " + features : "")}";

    /// <summary>Takes a line as VentureOS sent it on the network, keying the channel from its hello.</summary>
    public void Hear(string line)
    {
        if (channel is null)
        {
            if (line.StartsWith($"hello {VdbProtocol.Version} ", StringComparison.Ordinal))
            {
                Span<byte> client = stackalloc byte[VdbChannel.NonceSize];
                Span<byte> server = stackalloc byte[VdbChannel.NonceSize];
                Hex.Decode(Nonce, client);
                Hex.Decode(line[(line.LastIndexOf(' ') + 1)..], server);
                channel = VdbChannel.ForComputer(Identity, client, server);
            }

            Heard.Add(line);
            return;
        }

        if (channel.Open(line) is { } opened)
            Heard.Add(opened);
        else
            Unopened.Add(line);
    }

    /// <summary>A line as the computer puts it on the network: sealed once the channel is.</summary>
    public string Say(string line) => channel?.Seal(line) ?? line;

    /// <summary>The bytes of a line, with its break, as the computer puts it on the network.</summary>
    public byte[] Bytes(string line) => Encoding.UTF8.GetBytes(Say(line) + "\n");
}
