import Foundation
import Testing
@testable import VdbKit

/// Replays the recorded conversations in VentureOS's `docs/vdb/conversations`, which VentureOS's own tests replay
/// too, so the two sides can't drift apart. Here vdb plays the computer: VentureOS's lines (`<`) are its answers,
/// and what vdb sends must be the computer's lines (`>`), in order.
struct ConversationTests {
    static let folder = URL(fileURLWithPath: #filePath)
        .deletingLastPathComponent()
        .appendingPathComponent("../../../../docs/vdb/conversations")
        .standardizedFileURL

    /// The conversations a well-behaved vdb can have. The rest show VentureOS refusing what vdb never sends, such as
    /// a path outside the app, or two apps in one connection.
    static let playable = [
        "install-and-launch.txt",
        "ask-to-trust.txt",
        "trust-denied.txt",
        "trust-unanswered.txt",
        "refused-app.txt",
        "system-app.txt",
    ]

    @Test func everyConversationReads() throws {
        let names = try FileManager.default.contentsOfDirectory(atPath: Self.folder.path).filter { $0.hasSuffix(".txt") }
        #expect(names.count >= Self.playable.count)
        for name in names {
            _ = try Conversation.read(Self.folder.appendingPathComponent(name))
        }
    }

    @Test(arguments: playable)
    func replays(_ name: String) throws {
        let conversation = try Conversation.read(Self.folder.appendingPathComponent(name))
        let transport = ScriptedTransport(conversation.steps, identity: conversation.identity)
        var printed: [String] = []
        let client = VdbClient(transport: transport, output: { printed.append($0) }, nonce: { conversation.clientNonce })
        client.answerTimeout = 0
        client.trustTimeout = 0

        var thrown: VdbError?
        let bundle = try conversation.bundle()
        do {
            try client.session(identity: conversation.identity) {
                if let bundle {
                    _ = try client.install(bundle)
                }
                if let app = conversation.launched {
                    try client.launch(app)
                    try client.follow(for: 0.05)
                }
            }
        } catch let error as VdbError {
            thrown = error
        }

        #expect(transport.mismatches.isEmpty, "\(name): \(transport.mismatches)")
        #expect(!transport.heard.contains { $0.hasPrefix("hello ") && $0.contains(conversation.identity) },
                "\(name): the identity was sent in hello")
        #expect(transport.unplayed.isEmpty, "\(name) has more: \(transport.unplayed)")
        #expect(printed == conversation.output)
        switch conversation.firstError {
        case let answer? where answer.hasPrefix("error closing "):
            #expect(thrown == .closed(String(answer.dropFirst("error closing ".count))))
        case let answer? where answer.hasPrefix("error "):
            #expect(thrown == .refused(String(answer.dropFirst("error ".count))))
        default:
            #expect(thrown == nil)
        }
    }

    /// The lines Python's cryptography package sealed, which vdb must open, and seal the same, byte for byte.
    @Test func sealsAndOpensWhatAnotherImplementationSealed() throws {
        let conversation = try Conversation.read(Self.folder.appendingPathComponent("sealed-lines.txt"))
        let serverNonce = try #require(conversation.serverNonce)
        let channel = try VdbChannel.forComputer(
            identity: conversation.identity, clientNonce: conversation.clientNonce, serverNonce: serverNonce)
        let inside: [Conversation.Step] = [
            .answered("ok vdb/1 install launch"), .sent("launch Dice.app"), .answered("ok"), .answered("out Rolled a 4"),
            .sent(""), .sent("bye"), .answered("ok"),
        ]

        let raw = Array(conversation.steps.drop { $0 != .raw }.dropFirst())
        #expect(raw.count == inside.count)
        for (onTheNetwork, expected) in zip(raw, inside) {
            switch (onTheNetwork, expected) {
            case let (.answered(line), .answered(text)):
                #expect(channel.open(line) == text)
            case let (.sent(line), .sent(text)):
                #expect(try channel.seal(text) == line)
            default:
                Issue.record("\(onTheNetwork) and \(expected) go different ways")
            }
        }
    }
}

/// A recorded conversation: the lines both sides send, in order, with VentureOS's setup and checks left out.
struct Conversation {
    enum Step: Equatable {
        case sent(String)
        case answered(String)
        /// From here on, lines are written as they go on the network, already sealed.
        case raw
    }

    let steps: [Step]

    /// The computer's identity, from `! identity`, which the connection is keyed from.
    let identity: String

    static func read(_ file: URL) throws -> Conversation {
        var steps: [Step] = []
        var identity = "0123456789abcdef0123456789abcdef"
        for (number, line) in try String(contentsOf: file, encoding: .utf8).split(separator: "\n", omittingEmptySubsequences: false).enumerated() {
            if line.hasPrefix("! identity ") { identity = String(line.dropFirst("! identity ".count)) }
            if line == "! raw" { steps.append(.raw) }
            if line.isEmpty || line.hasPrefix("#") || line.hasPrefix("!") { continue }
            let text = line.count > 2 ? String(line.dropFirst(2)) : ""
            switch line.first {
            case ">": steps.append(.sent(text))
            case "<": steps.append(.answered(text))
            default: throw VdbError.unexpected("\(file.lastPathComponent), line \(number + 1): \(line)")
            }
        }
        return Conversation(steps: steps, identity: identity)
    }

    private var sent: [String] { steps.compactMap { if case .sent(let line) = $0 { line } else { nil } } }

    /// The computer's nonce, from its hello.
    var clientNonce: [UInt8] {
        sent.first.flatMap { line -> [UInt8]? in
            let words = line.split(separator: " ")
            return words.count >= 4 ? Hex.decode(String(words[3])) : nil
        } ?? Array(repeating: 0, count: VdbChannel.nonceSize)
    }

    /// VentureOS's nonce, from its hello.
    var serverNonce: [UInt8]? {
        steps.lazy.compactMap { step -> [UInt8]? in
            guard case .answered(let line) = step, line.hasPrefix("hello ") else { return nil }
            return line.split(separator: " ").last.flatMap { Hex.decode(String($0)) }
        }.first
    }

    var launched: String? {
        sent.first { $0.hasPrefix("launch ") }.map { String($0.dropFirst("launch ".count)) }
    }

    var output: [String] {
        steps.compactMap { if case .answered(let line) = $0, line.hasPrefix("out ") { String(line.dropFirst(4)) } else { nil } }
    }

    var firstError: String? {
        steps.lazy.compactMap { if case .answered(let line) = $0, line.hasPrefix("error ") { line } else { nil } }.first
    }

    /// The app the computer sends, rebuilt from its `file` and `data` lines.
    func bundle() throws -> VdbBundle? {
        guard let install = sent.first(where: { $0.hasPrefix("install ") }) else { return nil }
        var files: [VdbBundle.File] = []
        var path: String?
        var contents = Data()
        for line in sent {
            if line.hasPrefix("file ") {
                path = String(line.dropFirst(5).split(separator: " ").dropLast().joined(separator: " "))
                contents = Data()
            } else if line.hasPrefix("data "), let decoded = Data(base64Encoded: String(line.dropFirst(5))) {
                contents.append(decoded)
            } else if line == "end", let finished = path {
                files.append(.init(path: finished, contents: contents))
                path = nil
            }
        }
        return try VdbBundle(name: String(install.dropFirst("install ".count)), files: files)
    }
}

/// VentureOS's side of the seal, for tests that play it: reads what the computer sends and seals what it answers,
/// keying the connection from the two hellos, as VentureOS does.
final class SealingVentureOS {
    private let identity: String
    private var clientNonce: [UInt8]?
    private var channel: VdbChannel?

    init(identity: String) {
        self.identity = identity
    }

    /// A line the computer sent, as VentureOS reads it, or a note when it doesn't open.
    func read(_ line: String) -> String {
        if let channel {
            return channel.open(line) ?? "(a line that didn't open: \(line.prefix(40)))"
        }
        let words = line.split(separator: " ")
        if words.first == "hello", words.count >= 4 {
            clientNonce = Hex.decode(String(words[3]))
        }
        return line
    }

    /// A line VentureOS answers with, as it goes on the network.
    func write(_ line: String) -> String {
        if let channel {
            return (try? channel.seal(line)) ?? line
        }
        if line.hasPrefix("hello "), let server = line.split(separator: " ").last.flatMap({ Hex.decode(String($0)) }),
           let clientNonce {
            channel = try? VdbChannel.forVentureOS(identity: identity, clientNonce: clientNonce, serverNonce: server)
        }
        return line
    }
}

/// Plays VentureOS's side of a conversation: answers as it did, sealed as VentureOS seals them, and notes any line
/// sent that it didn't expect. Empty lines, which only keep a connection open, aren't part of a conversation.
final class ScriptedTransport: LineTransport {
    private var steps: ArraySlice<Conversation.Step>
    private let ventureOS: SealingVentureOS
    private var isRaw = false
    private(set) var mismatches: [String] = []

    /// Every line sent, as VentureOS read it.
    private(set) var heard: [String] = []

    init(_ steps: [Conversation.Step], identity: String = ClientTests.identity) {
        self.steps = steps[...]
        self.ventureOS = SealingVentureOS(identity: identity)
    }

    var unplayed: [Conversation.Step] { Array(steps) }

    func send(_ line: String) throws {
        skipMarkers()
        let read = isRaw ? line : ventureOS.read(line)
        heard.append(read)
        if read.isEmpty && !isRaw { return }
        guard case .sent(let expected)? = steps.first else {
            mismatches.append("sent \"\(read)\" where VentureOS didn't expect anything")
            return
        }
        if read != expected {
            mismatches.append("sent \"\(read.prefix(80))\", not \"\(expected.prefix(80))\"")
        }
        steps.removeFirst()
    }

    func receive(timeout: Double) throws -> String? {
        skipMarkers()
        guard case .answered(let line)? = steps.first else { return nil }
        steps.removeFirst()
        return isRaw ? line : ventureOS.write(line)
    }

    private func skipMarkers() {
        while steps.first == .raw {
            isRaw = true
            steps.removeFirst()
        }
    }
}
