# vdb: encrypting the conversation

**Status:** proposed for 0.7.4xx, Seto, with one question open (see [Pairing](#pairing)). **Date:** 2026-11-19.

vdb/1, as built in 0.7.1xx, sends everything in plain text, the computer's identity included: anyone who sees one
conversation can copy the identity and act as that computer. This design keeps the protocol vdb/1 and its commands as
they are, and wraps every line after `hello` in authenticated encryption, keyed from the identity, which never
crosses the network again.

vdb is weeks old and has one tool and one VentureOS speaking it, both in this repository, so this replaces the
plain-text conversation outright rather than living beside it, by the owner's decision.

## Why

- The identity is the whole of trust, and today it's sent as the first thing in every conversation.
- What's sent is an app, which VentureOS then runs. A conversation someone can read can also be changed.
- Cosmos has no cryptography, which is why the [vdb design](./2026-11-18-vdb-design.md) settled for guarding against
  accidents. Writing the few pieces needed is a small, well-specified job, with published test vectors for each.

## Goals

- Nothing after `hello` can be read or changed on the way without the change being noticed and the connection
  closed, leaving nothing installed.
- The identity, a 128-bit secret, never crosses the network once a computer is trusted.
- Every command, answer, and rule in [docs/vdb](../vdb/README.md) stays as it is, inside the encryption.
- The cryptography VentureOS needs is in `Venture.OS.Common`, with no kernel and no packages, tested against the
  standards' own vectors.

## Non-goals

- **Forward secrecy.** Whoever later steals an identity can read conversations recorded earlier. Avoiding that needs
  a key exchange such as X25519, one more primitive to write by hand; not now.
- **Hiding that vdb is in use**, or how big an app is.
- **Protecting a computer's identity file.** Whoever has it is that computer, as before.
- A general-purpose cryptography library for apps or Lua.

## Anti-goals

- **Inventing cryptography.** Every piece is a published standard, used as its standard says: SHA-256 (FIPS 180-4),
  HMAC (RFC 2104), HKDF (RFC 5869), and ChaCha20-Poly1305 (RFC 8439). Only how they're combined is ours, and it
  follows how TLS 1.3 and Noise combine them.
- **A success-shaped failure.** A frame that fails its check is never acted on, and is never answered with anything
  but closing.
- **Claiming more than it gives.** The docs say what it doesn't protect against, as above.

## Approaches considered

1. **Hash the identity with a challenge, send everything else in the clear.** Keeps the identity off the wire, but
   anyone on the path can still read and change apps. Not enough, by the owner's decision.
2. **ChaCha20-Poly1305 with keys from HKDF** (chosen). ChaCha20 is only 32-bit adds, rotations, and XORs: no tables,
   no timing that depends on the key, and simple for an ahead-of-time compiler. swift-crypto has it as `ChaChaPoly`,
   so the tool writes none of it.
3. **AES-GCM.** As standard, but written by hand it needs tables whose timing leaks the key, or a slow bit-by-bit
   version, and GHASH is easy to get subtly wrong.
4. **A new version, vdb/2.** The forward-compatibility rule's answer when old readers can't follow, but there are no
   old readers worth keeping: the tool and VentureOS change together. Rejected by the owner; it stays vdb/1.

## How it works

### Hello, in the clear

```text
> hello vdb/1 <key-id> <client-nonce> [features…]
< hello vdb/1 <server-nonce>
```

- **`key-id`** is the first 16 hexadecimal characters of SHA-256 of the ASCII text `vdb key-id ` followed by the
  identity. VentureOS keeps the identities it trusts, works out each one's key-id, and finds the computer by it. The
  Trust alert shows its first eight characters, where it showed the identity's.
- **`client-nonce`** and **`server-nonce`** are 16 random bytes each, as 32 lowercase hexadecimal characters, new
  for every connection.
- A key-id VentureOS doesn't trust goes to [Pairing](#pairing). A bad version, key-id, or nonce is answered with a
  plain `error closing <reason>`, as today; these are the only plain answers.

### Keys

Both sides derive two 32-byte keys with HKDF-SHA256:

- the input key is the identity's 16 bytes, from its hexadecimal text,
- the salt is the client's nonce followed by the server's, as bytes,
- the info is `vdb/1 to venture` for the key the computer sends with, and `vdb/1 to computer` for VentureOS's.

A key per direction means the two sides can number their messages independently without ever using the same nonce
under the same key.

### Sealed lines

Everything after hello is a sealed line:

```text
sealed <base64>
```

- The base64 holds a ChaCha20-Poly1305 ciphertext and its 16-byte tag, of one line of today's protocol in UTF-8,
  without its line ending.
- The 12-byte nonce is four zero bytes and a 64-bit message counter, little-endian, starting at 0 in each direction,
  and isn't sent. A counter that would wrap ends the connection.
- There's no additional data: the counter already binds each line to its place.
- The first sealed line each way is the old hello's answer and question: VentureOS sends `ok vdb/1 install launch`,
  which proves it holds the identity; the computer's first command proves the same in return.
- A line that isn't `sealed`, base64 that doesn't decode, or a tag that doesn't check closes the connection at once,
  with an install in progress thrown away as a disconnect throws it away now. Nothing is sent back in the clear,
  since the reason can't be trusted to reach the right party.
- The 8,192-byte limit applies to the line inside. The outer line's limit is what an 8,192-byte line becomes once
  sealed and encoded.
- The keep-alive is a sealed empty line.

Files are base64 inside and base64 again outside, about 1.8 times their size rather than 1.33. On a local link, with
apps up to 16 MiB, that's accepted rather than adding a binary frame.

### Pairing

Today, the first time an untrusted identity connects, VentureOS shows the Trust alert. Without the identity,
VentureOS can't derive the keys, and with it sent in the clear, pairing is exactly as safe as today's vdb, once.

**Open, for the owner:**

1. **Send the identity once, at pairing** (proposed). An unknown key-id is answered `wait trust`; on **Trust**,
   VentureOS asks for it with a plain `pair`, the tool sends `pair <identity>`, and VentureOS checks it against the
   key-id before trusting it. Trust ahead of time, through `developer.local.lua` or `venture.developer.trust`, never
   sends it. The docs say pairing is the one moment it's exposed.
2. **Trust ahead of time only.** The alert goes; a computer is trusted by `venture.developer.trust`, or by pasting
   its identity in Settings' Developer pane. Nothing secret ever crosses, but trust is lost at every reboot until
   VentureOS can keep files, so it's pasted again each time on any machine without a `developer.local.lua`.

### Randomness

Each nonce needs 16 unpredictable bytes. Whether Cosmos 3.0.99 offers a good source on x64 and ARM64 is not yet
known. If it doesn't, VentureOS mixes the timer, the hardware clock, and a counter through SHA-256, which is enough
that two connections never share a nonce, and the docs say it's no better than that. The client's nonce comes from
the host's own generator, so the keys are fresh even if VentureOS's nonce is weak.

## Where the code goes

- **`Venture.OS.Common/Cryptography/`**: `Sha256`, `HmacSha256`, `HkdfSha256`, and `ChaCha20Poly1305`, over spans,
  with no allocations in the inner loops, built with the AOT analyzers as the rest of Common is. Each is tested
  against its standard's vectors, and comparisons of tags are made in constant time.
- **`Venture.OS.Common/Vdb/`**: the session gains the hello above, the keys, and a sealing layer between the line
  reader and today's commands, which don't change. A nonce source is handed in, so tests use fixed nonces.
- **`docs/vdb/`**: the reference gains hello, sealed lines, and pairing. The recorded conversations stay readable:
  they record the lines inside, with the nonces fixed in a header, and a few record the sealed lines too, so the
  C# and Swift sides are checked against the same bytes.
- **The kernel**: the random source, and the Trust alert showing the key-id.
- **`tools/vdb/`**: swift-crypto, approved by the owner on 2026-11-19, for SHA-256, HKDF, and `ChaChaPoly`. The
  identity file and `vdb identity` stay as they are.
- **Roundhouse**, which speaks vdb in the same band, uses Common's code directly.

## Risks

- **Hand-written cryptography.** Mitigated by using only standard constructions, the standards' vectors, recorded
  conversations both sides must reproduce byte for byte, and swift-crypto, an independent implementation, on the
  other end.
- **Speed under Cosmos.** ChaCha20 in plain C# is fast on ordinary .NET; under Cosmos's compiler it's unmeasured.
  A 16 MiB app should still install in seconds; if it doesn't, that's found by booting, not guessed.
- **Weak randomness** on VentureOS's side, as above.
- **Breaking change.** Every vdb tool built before this band stops working with VentureOS, and the reverse. Both are
  in this repository and change in the same commit.

## Left to whoever builds it

- The exact text of `pair` and its answers, once the owner chooses.
- How the reference shows sealed conversations, and which recordings keep the sealed bytes.
