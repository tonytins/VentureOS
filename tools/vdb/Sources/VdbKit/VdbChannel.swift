import Crypto
import Foundation

/// The sealed half of a vdb/1 connection, after hello: each line travels as `sealed <base64>`, ChaCha20-Poly1305
/// over its UTF-8, with keys derived from the computer's identity and both sides' nonces.
///
/// It matches VentureOS's `VdbChannel` in `Venture.OS.Common/Vdb`: each direction has its own key, from HKDF-SHA-256
/// with the identity's 16 bytes as the secret, the computer's nonce then VentureOS's as the salt, and
/// `vdb/1 to venture` or `vdb/1 to computer` as the info. A line's nonce is four zero bytes and the count of lines
/// sent that way before it, little-endian, and is never sent.
public final class VdbChannel {
    /// What starts every sealed line.
    public static let prefix = "sealed "

    /// Each side's nonce, in bytes; it's sent as twice as many hexadecimal characters.
    public static let nonceSize = 16

    /// The longest sealed line: the longest plain line and its tag, in base64, after the prefix.
    public static let maximumLineLength = prefix.utf8.count + (VdbProtocol.maximumLineLength + 16 + 2) / 3 * 4

    private let sendKey: SymmetricKey
    private let receiveKey: SymmetricKey
    private var sent: UInt64 = 0
    private var received: UInt64 = 0

    private init(identity: String, clientNonce: [UInt8], serverNonce: [UInt8], isVentureOS: Bool) throws {
        guard let secret = Hex.decode(identity), secret.count == 16 else {
            throw VdbError.identity("\"\(identity)\" isn't an identity.")
        }
        guard clientNonce.count == Self.nonceSize, serverNonce.count == Self.nonceSize else {
            throw VdbError.unexpected("a nonce that isn't \(Self.nonceSize) bytes")
        }

        let salt = Data(clientNonce + serverNonce)
        func key(_ info: String) -> SymmetricKey {
            HKDF<SHA256>.deriveKey(
                inputKeyMaterial: SymmetricKey(data: secret), salt: salt, info: Data(info.utf8), outputByteCount: 32)
        }
        let toVenture = key("vdb/1 to venture")
        let toComputer = key("vdb/1 to computer")
        sendKey = isVentureOS ? toComputer : toVenture
        receiveKey = isVentureOS ? toVenture : toComputer
    }

    /// The computer's side of a connection.
    public static func forComputer(identity: String, clientNonce: [UInt8], serverNonce: [UInt8]) throws -> VdbChannel {
        try VdbChannel(identity: identity, clientNonce: clientNonce, serverNonce: serverNonce, isVentureOS: false)
    }

    /// VentureOS's side, for tests that play it.
    public static func forVentureOS(identity: String, clientNonce: [UInt8], serverNonce: [UInt8]) throws -> VdbChannel {
        try VdbChannel(identity: identity, clientNonce: clientNonce, serverNonce: serverNonce, isVentureOS: true)
    }

    /// A new nonce for this side of a connection, from the system's generator.
    public static func randomNonce() -> [UInt8] {
        var generator = SystemRandomNumberGenerator()
        return (0..<nonceSize).map { _ in UInt8.random(in: 0...255, using: &generator) }
    }

    /// Seals a line, without its line break, as the next line this side sends.
    public func seal(_ line: String) throws -> String {
        guard line.utf8.count <= VdbProtocol.maximumLineLength, !line.contains("\n"), !line.contains("\r") else {
            throw VdbError.unexpected("a line too long, or holding a line break, to send")
        }
        let box = try ChaChaPoly.seal(Data(line.utf8), using: sendKey, nonce: try Self.nonce(sent))
        sent += 1
        return Self.prefix + (box.ciphertext + box.tag).base64EncodedString()
    }

    /// Opens the next line the other side sent, or `nil` when it isn't a sealed line, wasn't sealed with this
    /// connection's key, or isn't the next one. After `nil`, end the connection.
    public func open(_ line: String) -> String? {
        guard line.hasPrefix(Self.prefix), line.utf8.count <= Self.maximumLineLength,
              let bytes = Data(base64Encoded: String(line.dropFirst(Self.prefix.count))), bytes.count >= 16,
              let nonce = try? Self.nonce(received),
              let box = try? ChaChaPoly.SealedBox(nonce: nonce, ciphertext: Data(bytes.prefix(bytes.count - 16)), tag: Data(bytes.suffix(16))),
              let plain = try? ChaChaPoly.open(box, using: receiveKey),
              let text = String(data: plain, encoding: .utf8), !text.contains("\n"), !text.contains("\r")
        else { return nil }
        received += 1
        return text
    }

    private static func nonce(_ count: UInt64) throws -> ChaChaPoly.Nonce {
        var bytes = [UInt8](repeating: 0, count: 12)
        for index in 0..<8 {
            bytes[4 + index] = UInt8(truncatingIfNeeded: count >> (8 * UInt64(index)))
        }
        return try ChaChaPoly.Nonce(data: bytes)
    }
}

/// Lowercase hexadecimal, as vdb writes identities, key IDs, and nonces.
public enum Hex {
    public static func encode<Bytes: Sequence>(_ bytes: Bytes) -> String where Bytes.Element == UInt8 {
        bytes.map { String($0, radix: 16).count == 1 ? "0" + String($0, radix: 16) : String($0, radix: 16) }.joined()
    }

    /// The bytes of lowercase hexadecimal, or `nil` for anything else.
    public static func decode(_ text: String) -> [UInt8]? {
        let digits = Array(text.utf8)
        guard digits.count % 2 == 0 else { return nil }
        var bytes: [UInt8] = []
        bytes.reserveCapacity(digits.count / 2)
        var index = 0
        while index < digits.count {
            guard let high = value(digits[index]), let low = value(digits[index + 1]) else { return nil }
            bytes.append(high << 4 | low)
            index += 2
        }
        return bytes
    }

    private static func value(_ digit: UInt8) -> UInt8? {
        switch digit {
        case 0x30...0x39: return digit - 0x30
        case 0x61...0x66: return digit - 0x61 + 10
        default: return nil
        }
    }
}
