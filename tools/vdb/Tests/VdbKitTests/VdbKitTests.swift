import Foundation
import Testing
@testable import VdbKit

/// A folder that's removed when the test is done.
final class TemporaryFolder {
    let url = FileManager.default.temporaryDirectory.appendingPathComponent("vdb-tests-\(UUID().uuidString)", isDirectory: true)

    init() throws {
        try FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
    }

    deinit {
        try? FileManager.default.removeItem(at: url)
    }

    func write(_ path: String, _ text: String) throws {
        let file = url.appendingPathComponent(path)
        try FileManager.default.createDirectory(at: file.deletingLastPathComponent(), withIntermediateDirectories: true)
        try Data(text.utf8).write(to: file)
    }
}

struct BundleTests {
    @Test func readsEveryFileSortedByPath() throws {
        let folder = try TemporaryFolder()
        try folder.write("Dice.app/Contents/Lua/main.lua", "print(1)\n")
        try folder.write("Dice.app/Contents/Info.xml", "<App/>")
        try folder.write("Dice.app/Contents/Resources/main.uix", "<UIScript/>")

        let bundle = try VdbBundle.read(from: folder.url.appendingPathComponent("Dice.app"))

        #expect(bundle.name == "Dice.app")
        #expect(bundle.files.map(\.path) == ["Contents/Info.xml", "Contents/Lua/main.lua", "Contents/Resources/main.uix"])
        #expect(bundle.files[1].contents == Data("print(1)\n".utf8))
    }

    @Test func refusesASymbolicLink() throws {
        let folder = try TemporaryFolder()
        try folder.write("Dice.app/Contents/Info.xml", "<App/>")
        try FileManager.default.createSymbolicLink(
            at: folder.url.appendingPathComponent("Dice.app/Contents/secrets"), withDestinationURL: URL(fileURLWithPath: "/etc"))

        #expect(throws: VdbError.invalidBundle("Contents/secrets is a symbolic link, which vdb doesn't send.")) {
            try VdbBundle.read(from: folder.url.appendingPathComponent("Dice.app"))
        }
    }

    @Test func refusesAFolderThatIsNotThere() {
        #expect(throws: VdbError.self) { try VdbBundle.read(from: URL(fileURLWithPath: "/no/such/Dice.app")) }
    }

    @Test(arguments: ["Dice", ".app", ".Dice.app", "Di:ce.app", "Dice.theme"])
    func refusesWhatIsNotAnAppsName(_ name: String) {
        #expect(throws: VdbError.self) { try VdbBundle(name: name, files: []) }
    }

    @Test(arguments: ["../init.lua", "/etc/passwd", "Contents//Info.xml", "Contents/./Info.xml", "Contents\\Info.xml", ""])
    func refusesAPathOutsideTheApp(_ path: String) {
        #expect(throws: VdbError.self) { try VdbBundle(name: "Dice.app", files: [.init(path: path, contents: Data())]) }
    }

    @Test func refusesTwoPathsDifferingOnlyInCase() {
        #expect(throws: VdbError.self) {
            try VdbBundle(name: "Dice.app", files: [.init(path: "Info.xml", contents: Data()), .init(path: "info.xml", contents: Data())])
        }
    }

    @Test func refusesAFileTooLarge() {
        #expect(throws: VdbError.self) {
            try VdbBundle(name: "Dice.app", files: [.init(path: "big", contents: Data(count: VdbProtocol.maximumFileSize + 1))])
        }
    }

    @Test func refusesAnAppTooLarge() {
        let files = (0...4).map { VdbBundle.File(path: "f\($0)", contents: Data(count: VdbProtocol.maximumFileSize)) }
        #expect(throws: VdbError.self) { try VdbBundle(name: "Dice.app", files: files) }
    }

    @Test func refusesTooManyFiles() {
        let files = (0...VdbProtocol.maximumFiles).map { VdbBundle.File(path: "f\($0)", contents: Data()) }
        #expect(throws: VdbError.self) { try VdbBundle(name: "Dice.app", files: files) }
    }
}

struct IdentityTests {
    @Test func makesAnIdentityOnceAndKeepsIt() throws {
        let folder = try TemporaryFolder()
        let file = folder.url.appendingPathComponent("vdb/identity")

        let first = try VdbIdentity.load(from: file)
        let second = try VdbIdentity.load(from: file)

        #expect(VdbIdentity.isIdentity(first))
        #expect(first == second)
        #if !os(Windows)
        let permissions = try FileManager.default.attributesOfItem(atPath: file.path)[.posixPermissions] as? Int
        #expect(permissions == 0o600)
        #endif
    }

    @Test func makesADifferentIdentityEachTime() {
        #expect(VdbIdentity.make() != VdbIdentity.make())
    }

    @Test func refusesAFileThatIsNotAnIdentity() throws {
        let folder = try TemporaryFolder()
        try folder.write("identity", "my laptop\n")

        #expect(throws: VdbError.self) { try VdbIdentity.load(from: folder.url.appendingPathComponent("identity")) }
    }

    @Test func usesTheConfigurationFolder() {
        #if !os(Windows)
        let file = VdbIdentity.defaultFile(environment: ["XDG_CONFIG_HOME": "/home/someone/.cfg"])
        #expect(file.path == "/home/someone/.cfg/vdb/identity")
        #endif
    }

    @Test func printsTheLinesForDeveloperLocalLua() {
        #expect(VdbIdentity.luaLines(for: "0123456789abcdef0123456789abcdef") == """
            venture.developer.enable()
            venture.developer.trust("0123456789abcdef0123456789abcdef")
            venture.startDesktop()
            """)
    }
}

struct ClientTests {
    static let identity = "0123456789abcdef0123456789abcdef"
    static let keyID = "0a30b19247f0d8d7"
    static let nonce: [UInt8] = Array(0..<16)
    static let hello = Conversation.Step.sent("hello vdb/1 \(keyID) 000102030405060708090a0b0c0d0e0f")
    static let welcome = Conversation.Step.answered("hello vdb/1 f0f1f2f3f4f5f6f7f8f9fafbfcfdfeff")

    private func client(_ steps: [Conversation.Step], printed: @escaping (String) -> Void = { _ in }) -> (VdbClient, ScriptedTransport) {
        let transport = ScriptedTransport([Self.hello, Self.welcome, .answered("ok vdb/1 install launch")] + steps)
        let client = VdbClient(transport: transport, output: printed, nonce: { Self.nonce })
        client.answerTimeout = 0
        client.trustTimeout = 0
        return (client, transport)
    }

    @Test func aKeyIDIsTheHashOfTheIdentity() {
        // As VentureOS and docs/vdb/sealed-lines.py work it out.
        #expect(VdbIdentity.keyID(for: Self.identity) == Self.keyID)
    }

    @Test func sendsTheIdentityOnlyWhenVentureOSAsksToPair() throws {
        let transport = ScriptedTransport([
            Self.hello, .answered("wait trust"), .answered("pair"), .sent("pair \(Self.identity)"), Self.welcome,
            .answered("ok vdb/1 install launch"), .sent("bye"), .answered("ok"),
        ])
        let client = VdbClient(transport: transport, output: { _ in }, nonce: { Self.nonce })
        client.answerTimeout = 0
        client.trustTimeout = 0

        try client.session(identity: Self.identity) {}

        #expect(transport.mismatches.isEmpty, "\(transport.mismatches)")
        #expect(transport.heard.filter { $0.contains(Self.identity) } == ["pair \(Self.identity)"])
    }

    @Test func endsOnALineThatDoesNotOpen() throws {
        let transport = ScriptedTransport([Self.hello, Self.welcome, .answered("ok vdb/1 install launch"), .sent("launch Dice.app"), .raw,
                                           .answered("sealed AAAAAAAAAAAAAAAAAAAAAA==")])
        let client = VdbClient(transport: transport, output: { _ in }, nonce: { Self.nonce })
        client.answerTimeout = 0
        try client.hello(identity: Self.identity)

        #expect(throws: VdbError.self) { try client.launch("Dice.app") }
    }

    @Test func splitsAFileIntoDataLinesThatFit() throws {
        let contents = Data((0..<(VdbProtocol.maximumDataBytes + 10)).map { UInt8($0 % 251) })
        let bundle = try VdbBundle(name: "Big.app", files: [.init(path: "big", contents: contents)])

        // Answers to install, the file's end, and commit.
        let recorder = RecordingTransport(answers: ["ok", "ok", "ok installed /mnt/user/apps/Big.app"])
        let sender = VdbClient(transport: recorder, output: { _ in })
        sender.answerTimeout = 0
        try recorder.welcome(sender)
        #expect(try sender.install(bundle) == "/mnt/user/apps/Big.app")

        let data = recorder.sent.filter { $0.hasPrefix("data ") }
        #expect(data.count == 2)
        #expect(data.allSatisfy { $0.utf8.count <= VdbProtocol.maximumLineLength })
        let joined = data.reduce(into: Data()) { $0.append(Data(base64Encoded: String($1.dropFirst(5)))!) }
        #expect(joined == contents)
        #expect(recorder.sent.first == "install Big.app")
        #expect(recorder.sent.last == "commit")
    }

    @Test func passesOnOutputThatArrivesBeforeAnAnswer() throws {
        var printed: [String] = []
        let (client, transport) = client([
            .sent("launch Dice.app"), .answered("out first"), .answered("out second"), .answered("ok"),
        ], printed: { printed.append($0) })
        try client.hello(identity: Self.identity)

        try client.launch("Dice.app")

        #expect(printed == ["first", "second"])
        #expect(transport.mismatches.isEmpty)
    }

    @Test func refusesAFeatureVentureOSDoesNotOffer() throws {
        let transport = ScriptedTransport([Self.hello, Self.welcome, .answered("ok vdb/1 install")])
        let client = VdbClient(transport: transport, output: { _ in }, nonce: { Self.nonce })
        client.answerTimeout = 0
        try client.hello(identity: Self.identity)

        #expect(throws: VdbError.refused("this VentureOS doesn't offer launch")) { try client.launch("Dice.app") }
    }

    @Test func refusesAVersionItDoesNotSpeak() throws {
        let transport = ScriptedTransport([Self.hello, .answered("hello vdb/2 f0f1f2f3f4f5f6f7f8f9fafbfcfdfeff")])
        let client = VdbClient(transport: transport, output: { _ in }, nonce: { Self.nonce })
        client.answerTimeout = 0

        #expect(throws: VdbError.self) { try client.hello(identity: Self.identity) }
    }

    @Test func timesOutWhenNothingAnswers() throws {
        let transport = ScriptedTransport([Self.hello])
        let client = VdbClient(transport: transport, output: { _ in }, nonce: { Self.nonce })
        client.answerTimeout = 0

        #expect(throws: VdbError.timedOut("hello")) { try client.hello(identity: Self.identity) }
    }

    @Test func keepsTheConnectionOpenWhileFollowing() throws {
        let recorder = RecordingTransport(answers: [])
        let client = VdbClient(transport: recorder, output: { _ in })
        client.keepAlive = 0.01

        try client.follow(for: 0.1)

        #expect(recorder.sent.contains(""))
    }
}

/// Answers with the given lines in turn, whatever is sent, sealed as VentureOS would once welcomed, and records
/// what is sent, as VentureOS reads it.
final class RecordingTransport: LineTransport {
    private var answers: [String]
    private let ventureOS = SealingVentureOS(identity: ClientTests.identity)
    private(set) var sent: [String] = []

    init(answers: [String]) {
        self.answers = answers
    }

    /// Says hello for `client`, answered as VentureOS would, then forgets it.
    func welcome(_ client: VdbClient) throws {
        answers.insert(contentsOf: ["hello vdb/1 f0f1f2f3f4f5f6f7f8f9fafbfcfdfeff", "ok vdb/1 install launch"], at: 0)
        try client.hello(identity: ClientTests.identity)
        sent.removeAll()
    }

    func send(_ line: String) throws {
        sent.append(ventureOS.read(line))
    }

    func receive(timeout: Double) throws -> String? {
        if answers.isEmpty {
            if timeout > 0 { Thread.sleep(forTimeInterval: timeout) }
            return nil
        }
        return ventureOS.write(answers.removeFirst())
    }
}
