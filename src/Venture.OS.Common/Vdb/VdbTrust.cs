namespace Venture.OS.Vdb;

/// <summary>Where VentureOS stands with a computer that says hello over vdb.</summary>
public enum VdbTrustState
{
    /// <summary>It hasn't been asked about, or the last question was withdrawn.</summary>
    Unknown,

    /// <summary>The person at VentureOS is being asked whether to trust it.</summary>
    Asking,

    /// <summary>It may install and open apps until VentureOS restarts.</summary>
    Trusted,

    /// <summary>It was refused this time; it will be asked about again if it says hello again.</summary>
    Denied,
}

/// <summary>
/// The computers VentureOS trusts over vdb, by the identity each one's vdb tool made for itself, kept in memory
/// until VentureOS restarts. It guards against accidents, such as the wrong machine connecting, not against
/// someone watching the network: the identity is sent as plain text.
/// </summary>
public sealed class VdbTrust
{
    private readonly Dictionary<string, VdbTrustState> states = new(StringComparer.Ordinal);

    /// <summary>
    /// Raised when a computer needs asking about, with its identity, so the desktop can ask the person; they answer
    /// through <see cref="Answer"/>. Poll <see cref="State"/> to know when the question no longer stands.
    /// </summary>
    public event Action<string>? Asked;

    /// <summary>How many computers are trusted.</summary>
    public int TrustedCount => states.Values.Count(state => state == VdbTrustState.Trusted);

    /// <summary>Whether a text is an identity: 32 lowercase hexadecimal characters, as vdb makes them.</summary>
    public static bool IsIdentity(string? text) =>
        text is { Length: 32 } && text.All(character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    /// <summary>Where VentureOS stands with a computer.</summary>
    public VdbTrustState State(string identity) =>
        states.TryGetValue(identity, out var state) ? state : VdbTrustState.Unknown;

    /// <summary>Trusts a computer without asking, as a developer's own build does from its init script.</summary>
    /// <exception cref="ArgumentException"><paramref name="identity"/> isn't one.</exception>
    public void Trust(string identity)
    {
        Check(identity);
        states[identity] = VdbTrustState.Trusted;
    }

    /// <summary>Starts asking about a computer, unless it's already trusted or being asked about.</summary>
    /// <exception cref="ArgumentException"><paramref name="identity"/> isn't one.</exception>
    public void Ask(string identity)
    {
        Check(identity);
        if (State(identity) is VdbTrustState.Trusted or VdbTrustState.Asking)
            return;

        states[identity] = VdbTrustState.Asking;
        Asked?.Invoke(identity);
    }

    /// <summary>The person's answer about a computer being asked about; an answer to no question changes nothing.</summary>
    public void Answer(string identity, bool trust)
    {
        if (State(identity) == VdbTrustState.Asking)
            states[identity] = trust ? VdbTrustState.Trusted : VdbTrustState.Denied;
    }

    /// <summary>
    /// Drops the question about a computer, or its refusal, such as when it disconnects, so it's asked about afresh
    /// next time. A trusted computer stays trusted.
    /// </summary>
    public void Withdraw(string identity)
    {
        if (State(identity) is VdbTrustState.Asking or VdbTrustState.Denied)
            states.Remove(identity);
    }

    private static void Check(string identity)
    {
        if (!IsIdentity(identity))
            throw new ArgumentException($"\"{identity}\" isn't a vdb identity, which is 32 lowercase hexadecimal characters.", nameof(identity));
    }
}
