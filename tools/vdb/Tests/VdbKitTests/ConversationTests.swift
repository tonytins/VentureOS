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
        let transport = ScriptedTransport(conversation.steps)
        var printed: [String] = []
        let client = VdbClient(transport: transport, output: { printed.append($0) })
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
}

/// A recorded conversation: the lines both sides send, in order, with VentureOS's setup and checks left out.
struct Conversation {
    enum Step: Equatable {
        case sent(String)
        case answered(String)
    }

    let steps: [Step]

    static func read(_ file: URL) throws -> Conversation {
        var steps: [Step] = []
        for (number, line) in try String(contentsOf: file, encoding: .utf8).split(separator: "\n", omittingEmptySubsequences: false).enumerated() {
            if line.isEmpty || line.hasPrefix("#") || line.hasPrefix("!") { continue }
            let text = line.count > 2 ? String(line.dropFirst(2)) : ""
            switch line.first {
            case ">": steps.append(.sent(text))
            case "<": steps.append(.answered(text))
            default: throw VdbError.unexpected("\(file.lastPathComponent), line \(number + 1): \(line)")
            }
        }
        return Conversation(steps: steps)
    }

    private var sent: [String] { steps.compactMap { if case .sent(let line) = $0 { line } else { nil } } }

    var identity: String { sent.first.map { String($0.split(separator: " ")[2]) } ?? "" }

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

/// Plays VentureOS's side of a conversation: answers as it did, and notes any line sent that it didn't expect.
/// Empty lines, which only keep a connection open, aren't part of a conversation.
final class ScriptedTransport: LineTransport {
    private var steps: ArraySlice<Conversation.Step>
    private(set) var mismatches: [String] = []

    init(_ steps: [Conversation.Step]) {
        self.steps = steps[...]
    }

    var unplayed: [Conversation.Step] { Array(steps) }

    func send(_ line: String) throws {
        if line.isEmpty { return }
        guard case .sent(let expected)? = steps.first else {
            mismatches.append("sent \"\(line)\" where VentureOS didn't expect anything")
            return
        }
        if line != expected {
            mismatches.append("sent \"\(line.prefix(80))\", not \"\(expected.prefix(80))\"")
        }
        steps.removeFirst()
    }

    func receive(timeout: Double) throws -> String? {
        guard case .answered(let line)? = steps.first else { return nil }
        steps.removeFirst()
        return line
    }
}
