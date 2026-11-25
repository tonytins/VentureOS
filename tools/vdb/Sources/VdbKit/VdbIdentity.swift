import Crypto
import Foundation

/// This computer's identity: 32 random lowercase hexadecimal characters, made once and kept in a file, which
/// VentureOS is asked to trust. It's a secret the connection is keyed from: the network sees only its key ID, and the
/// identity itself only once, when the person at VentureOS chooses Trust. Keep the file private.
public enum VdbIdentity {
    /// Where the identity is kept: `vdb/identity` in the configuration folder, which is `%APPDATA%` on Windows,
    /// and `$XDG_CONFIG_HOME`, or `~/.config`, elsewhere.
    public static func defaultFile(environment: [String: String] = ProcessInfo.processInfo.environment) -> URL {
        #if os(Windows)
        let base = environment["APPDATA"].map { URL(fileURLWithPath: $0, isDirectory: true) }
            ?? FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("AppData/Roaming", isDirectory: true)
        #else
        let base = environment["XDG_CONFIG_HOME"].flatMap { $0.isEmpty ? nil : URL(fileURLWithPath: $0, isDirectory: true) }
            ?? FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".config", isDirectory: true)
        #endif
        return base.appendingPathComponent("vdb", isDirectory: true).appendingPathComponent("identity")
    }

    /// Whether a text is an identity.
    public static func isIdentity(_ text: String) -> Bool {
        text.utf8.count == 32 && text.utf8.allSatisfy { (0x30...0x39).contains($0) || (0x61...0x66).contains($0) }
    }

    /// An identity's key ID: the first 16 hexadecimal characters of SHA-256 of `vdb key-id ` and the identity, which
    /// names this computer to VentureOS without giving the identity away.
    public static func keyID(for identity: String) -> String {
        String(Hex.encode(SHA256.hash(data: Data(("vdb key-id " + identity).utf8))).prefix(16))
    }

    /// Makes a new identity.
    public static func make() -> String {
        var generator = SystemRandomNumberGenerator()
        return (0..<16).map { _ in
            let byte = UInt8.random(in: 0...255, using: &generator)
            return String(byte, radix: 16).leftPadded(to: 2)
        }.joined()
    }

    /// Reads the identity from `file`, making and saving one first if there's none.
    public static func load(from file: URL) throws -> String {
        if FileManager.default.fileExists(atPath: file.path) {
            let text: String
            do {
                text = try String(contentsOf: file, encoding: .utf8).trimmingCharacters(in: .whitespacesAndNewlines)
            } catch {
                throw VdbError.identity("\(file.path) couldn't be read: \(error.localizedDescription)")
            }
            guard isIdentity(text) else {
                throw VdbError.identity("\(file.path) doesn't hold an identity. Delete it, and vdb makes a new one, which VentureOS will ask to trust.")
            }
            return text
        }

        let identity = make()
        do {
            try FileManager.default.createDirectory(at: file.deletingLastPathComponent(), withIntermediateDirectories: true)
            try (identity + "\n").write(to: file, atomically: true, encoding: .utf8)
            #if !os(Windows)
            try FileManager.default.setAttributes([.posixPermissions: 0o600], ofItemAtPath: file.path)
            #endif
        } catch {
            throw VdbError.identity("\(file.path) couldn't be written: \(error.localizedDescription)")
        }
        return identity
    }

    /// The lines for a build's `developer.local.lua`, so VentureOS trusts this computer at every boot, and boots to
    /// the desktop, where vdb is heard.
    public static func luaLines(for identity: String) -> String {
        """
        venture.developer.enable()
        venture.developer.trust("\(identity)")
        venture.startDesktop()
        """
    }
}

private extension String {
    func leftPadded(to length: Int) -> String {
        count >= length ? self : String(repeating: "0", count: length - count) + self
    }
}
