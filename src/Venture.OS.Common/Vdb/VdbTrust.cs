using System.Text;
using Venture.OS.Cryptography;

namespace Venture.OS.Vdb;

/// <summary>Where VentureOS stands with a computer that says hello over vdb, by its key ID.</summary>
public enum VdbTrustState
{
    /// <summary>It hasn't been asked about, or the last question was withdrawn.</summary>
    Unknown,

    /// <summary>The person at VentureOS is being asked whether to trust it.</summary>
    Asking,

    /// <summary>The person chose Trust; it's trusted once it sends the identity that gives its key ID.</summary>
    Approved,

    /// <summary>It may install and open apps until VentureOS restarts.</summary>
    Trusted,

    /// <summary>It was refused this time; it will be asked about again if it says hello again.</summary>
    Denied,
}

/// <summary>
/// The computers VentureOS trusts over vdb, kept in memory until VentureOS restarts. Each is known by the identity its
/// vdb tool made, a 128-bit secret its connections are keyed from, and named on the network only by its key ID, a
/// hash of it, so the identity crosses the network once at most: when the person at VentureOS chooses Trust.
/// </summary>
public sealed class VdbTrust
{
    /// <summary>A key ID's length, in hexadecimal characters.</summary>
    public const int KeyIdLength = 16;

    private readonly Dictionary<string, string> trusted = new(StringComparer.Ordinal);
    private readonly Dictionary<string, VdbTrustState> questions = new(StringComparer.Ordinal);
    private readonly HashSet<string> connected = new(StringComparer.Ordinal);

    /// <summary>
    /// Raised when a computer needs asking about, with its key ID, so the desktop can ask the person; they answer
    /// through <see cref="Answer"/>. Poll <see cref="State"/> to know when the question no longer stands.
    /// </summary>
    public event Action<string>? Asked;

    /// <summary>How many computers are trusted.</summary>
    public int TrustedCount => trusted.Count;

    /// <summary>Whether a text is an identity: 32 lowercase hexadecimal characters, as vdb makes them.</summary>
    public static bool IsIdentity(string? text) => Hex.IsLowercase(text, 32);

    /// <summary>Whether a text is a key ID: 16 lowercase hexadecimal characters.</summary>
    public static bool IsKeyId(string? text) => Hex.IsLowercase(text, KeyIdLength);

    /// <summary>
    /// An identity's key ID: the first 16 hexadecimal characters of SHA-256 of <c>vdb key-id </c> and the identity,
    /// which names the computer without giving the identity away.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="identity"/> isn't one.</exception>
    public static string KeyId(string identity)
    {
        Check(identity);
        var digest = Sha256.Hash(Encoding.ASCII.GetBytes("vdb key-id " + identity));
        return Hex.Encode(digest.AsSpan(0, KeyIdLength / 2));
    }

    /// <summary>The identity of the trusted computer with this key ID, or <see langword="null"/>.</summary>
    public string? Find(string keyId) => trusted.TryGetValue(keyId, out var identity) ? identity : null;

    /// <summary>Where VentureOS stands with a computer, by its key ID.</summary>
    public VdbTrustState State(string keyId) =>
        trusted.ContainsKey(keyId) ? VdbTrustState.Trusted
        : questions.TryGetValue(keyId, out var state) ? state
        : VdbTrustState.Unknown;

    /// <summary>
    /// Trusts a computer by its identity, without asking, as a developer's own build does from its init script, or
    /// once a computer the person approved sends it.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="identity"/> isn't one.</exception>
    public void Trust(string identity)
    {
        var keyId = KeyId(identity);
        trusted[keyId] = identity;
        questions.Remove(keyId);
    }

    /// <summary>Starts asking about a computer, unless it's trusted or already being asked about.</summary>
    /// <exception cref="ArgumentException"><paramref name="keyId"/> isn't one.</exception>
    public void Ask(string keyId)
    {
        if (!IsKeyId(keyId))
            throw new ArgumentException($"\"{keyId}\" isn't a vdb key ID, which is {KeyIdLength} lowercase hexadecimal characters.", nameof(keyId));
        if (State(keyId) is VdbTrustState.Trusted or VdbTrustState.Asking or VdbTrustState.Approved)
            return;

        questions[keyId] = VdbTrustState.Asking;
        Asked?.Invoke(keyId);
    }

    /// <summary>The person's answer about a computer being asked about; an answer to no question changes nothing.</summary>
    public void Answer(string keyId, bool trust)
    {
        if (State(keyId) == VdbTrustState.Asking)
            questions[keyId] = trust ? VdbTrustState.Approved : VdbTrustState.Denied;
    }

    /// <summary>
    /// Notes that a trusted computer connected, giving whether it's the first time since VentureOS started, so the
    /// desktop says so once rather than every time an agent's tool connects again.
    /// </summary>
    public bool FirstConnection(string keyId) => connected.Add(keyId);

    /// <summary>
    /// Drops the question about a computer, its approval, or its refusal, such as when it disconnects, so it's asked
    /// about afresh next time. A trusted computer stays trusted.
    /// </summary>
    public void Withdraw(string keyId) => questions.Remove(keyId);

    private static void Check(string identity)
    {
        if (!IsIdentity(identity))
            throw new ArgumentException($"\"{identity}\" isn't a vdb identity, which is 32 lowercase hexadecimal characters.", nameof(identity));
    }
}
