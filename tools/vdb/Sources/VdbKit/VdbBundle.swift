import Foundation

/// An app bundle read from disk, ready to send: its folder's name and its files, by their paths inside it.
///
/// It's checked against the same limits VentureOS applies, so an app that can't be sent is refused before
/// connecting, with the reason. VentureOS still checks it again, and checks it as an app.
public struct VdbBundle: Equatable, Sendable {
    /// One file, by its path inside the bundle, such as `Contents/Lua/main.lua`.
    public struct File: Equatable, Sendable {
        public let path: String
        public let contents: Data

        public init(path: String, contents: Data) {
            self.path = path
            self.contents = contents
        }
    }

    /// The folder's name, such as `Dice.app`.
    public let name: String

    /// Its files, sorted by path, so the same app is always sent the same way.
    public let files: [File]

    /// Makes a bundle from its parts, checking them as `read(from:)` does.
    public init(name: String, files: [File]) throws {
        try Self.check(name: name, files: files)
        self.name = name
        self.files = files.sorted { $0.path < $1.path }
    }

    /// Reads a bundle's folder: every regular file under it. A symbolic link is refused, since it could name
    /// something outside the bundle.
    public static func read(from folder: URL) throws -> VdbBundle {
        let root = folder.standardizedFileURL
        var isDirectory: ObjCBool = false
        guard FileManager.default.fileExists(atPath: root.path, isDirectory: &isDirectory), isDirectory.boolValue else {
            throw VdbError.invalidBundle("There's no folder at \(folder.path).")
        }

        let keys: [URLResourceKey] = [.isRegularFileKey, .isDirectoryKey, .isSymbolicLinkKey]
        guard let walker = FileManager.default.enumerator(at: root, includingPropertiesForKeys: keys) else {
            throw VdbError.invalidBundle("\(folder.path) couldn't be read.")
        }

        var files: [File] = []
        for case let url as URL in walker {
            let values = try url.resourceValues(forKeys: Set(keys))
            let path = String(url.standardizedFileURL.path.dropFirst(root.path.count + 1))
            if values.isSymbolicLink == true {
                throw VdbError.invalidBundle("\(path) is a symbolic link, which vdb doesn't send.")
            }
            guard values.isRegularFile == true else { continue }
            do {
                files.append(File(path: path, contents: try Data(contentsOf: url)))
            } catch {
                throw VdbError.invalidBundle("\(path) couldn't be read: \(error.localizedDescription)")
            }
            if files.count > VdbProtocol.maximumFiles {
                throw VdbError.invalidBundle("An app sent over vdb has at most \(VdbProtocol.maximumFiles) files.")
            }
        }

        return try VdbBundle(name: root.lastPathComponent, files: files)
    }

    private static func check(name: String, files: [File]) throws {
        if let problem = problemWithApp(name) {
            throw VdbError.invalidBundle(problem)
        }
        if files.count > VdbProtocol.maximumFiles {
            throw VdbError.invalidBundle("An app sent over vdb has at most \(VdbProtocol.maximumFiles) files.")
        }

        var seen = Set<String>()
        var total = 0
        for file in files {
            if let problem = problemWithPath(file.path) {
                throw VdbError.invalidBundle(problem)
            }
            guard seen.insert(file.path.lowercased()).inserted else {
                throw VdbError.invalidBundle("\(file.path) is in the app twice, differing only in case.")
            }
            guard file.contents.count <= VdbProtocol.maximumFileSize else {
                throw VdbError.invalidBundle("\(file.path) is larger than the \(VdbProtocol.maximumFileSize / (1024 * 1024)) MiB a file may be.")
            }
            total += file.contents.count
        }
        guard total <= VdbProtocol.maximumBundleSize else {
            throw VdbError.invalidBundle("An app sent over vdb is at most \(VdbProtocol.maximumBundleSize / (1024 * 1024)) MiB.")
        }
    }

    /// Why a name can't be an app's folder, as VentureOS checks it, or `nil`.
    static func problemWithApp(_ name: String) -> String? {
        guard name.lowercased().hasSuffix(".app"), name.count > ".app".count else {
            return "\"\(name)\" isn't an app's folder, such as Dice.app."
        }
        guard name.count <= VdbProtocol.maximumPathLength else {
            return "An app's name is at most \(VdbProtocol.maximumPathLength) characters."
        }
        guard !name.hasPrefix(".") else { return "An app's name can't start with a dot." }
        guard !name.unicodeScalars.contains(where: { $0.properties.generalCategory == .control || "/\\:".unicodeScalars.contains($0) }) else {
            return "\"\(name)\" has a character an app's name can't have."
        }
        return nil
    }

    /// Why a path can't be one inside a bundle, as VentureOS checks it, or `nil`.
    static func problemWithPath(_ path: String) -> String? {
        guard !path.isEmpty, path.count <= VdbProtocol.maximumPathLength else {
            return "\(path) is longer than the \(VdbProtocol.maximumPathLength) characters a path may be."
        }
        guard !path.hasPrefix("/") else { return "\(path) starts at the root." }
        guard !path.unicodeScalars.contains(where: { $0.properties.generalCategory == .control || "\\:".unicodeScalars.contains($0) }) else {
            return "\(path) has a character VentureOS doesn't take in a path, such as \\ or :."
        }
        guard !path.split(separator: "/", omittingEmptySubsequences: false).contains(where: { $0.isEmpty || $0 == "." || $0 == ".." }) else {
            return "\(path) has an empty, . or .. part."
        }
        return nil
    }
}
