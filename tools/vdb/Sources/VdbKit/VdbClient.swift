import Foundation

/// Something that carries vdb's lines both ways: a TCP connection, or a test's script.
public protocol LineTransport: AnyObject {
    /// Sends a line; the line break is added.
    func send(_ line: String) throws

    /// The next line, without its line break, or `nil` when none arrives within `timeout` seconds.
    func receive(timeout: Double) throws -> String?
}

/// vdb's side of vdb/1: says hello, sends an app, opens it, and passes on what apps print.
///
/// Lines starting `out ` can arrive at any time once VentureOS trusts the computer; they're handed to `output`
/// wherever they come, and never taken for an answer.
public final class VdbClient {
    /// How long to wait for an answer, in seconds. VentureOS answers at once, except while it's checking an app.
    public var answerTimeout: Double = 30

    /// How long to wait to be trusted, a little longer than VentureOS waits for the person.
    public var trustTimeout: Double = VdbProtocol.trustLimitSeconds + 10

    /// How often to send an empty line while only listening, which keeps VentureOS from closing an idle
    /// connection, in seconds.
    public var keepAlive: Double = VdbProtocol.idleLimitSeconds / 3

    private let transport: LineTransport
    private let output: (String) -> Void
    private let status: (String) -> Void
    private var features: Set<String> = []

    /// - Parameters:
    ///   - output: Given each line an app prints.
    ///   - status: Given what vdb is waiting for, such as to be trusted, for the person to read.
    public init(transport: LineTransport, output: @escaping (String) -> Void, status: @escaping (String) -> Void = { _ in }) {
        self.transport = transport
        self.output = output
        self.status = status
    }

    /// Says hello, waiting while VentureOS asks whether to trust this computer.
    public func hello(identity: String) throws {
        guard VdbIdentity.isIdentity(identity) else {
            throw VdbError.identity("\"\(identity)\" isn't an identity.")
        }
        try transport.send("hello \(VdbProtocol.version) \(identity)")

        var answer = try answer(waiting: "hello")
        if answer == "wait trust" {
            status("Waiting to be trusted: choose Trust on VentureOS.")
            answer = try self.answer(waiting: "whether to trust this computer", timeout: trustTimeout)
        }

        let words = answer.split(separator: " ").map(String.init)
        guard words.first == "ok", words.count >= 2 else {
            throw VdbError.unexpected(answer)
        }
        guard words[1] == VdbProtocol.version else {
            throw VdbError.unexpected("VentureOS speaks \(words[1]), and this vdb speaks \(VdbProtocol.version).")
        }
        features = Set(words.dropFirst(2))
    }

    /// Sends an app and installs it, returning where VentureOS put it.
    public func install(_ bundle: VdbBundle) throws -> String {
        try require("install")
        try command("install \(bundle.name)")

        for file in bundle.files {
            try transport.send("file \(file.path) \(file.contents.count)")
            var start = file.contents.startIndex
            while start < file.contents.endIndex {
                let end = file.contents.index(start, offsetBy: VdbProtocol.maximumDataBytes, limitedBy: file.contents.endIndex)
                    ?? file.contents.endIndex
                try transport.send("data " + file.contents[start..<end].base64EncodedString())
                start = end
            }
            try command("end", waiting: "\(file.path)'s end")
        }

        let answer = try command("commit", waiting: "the app's check")
        guard answer.hasPrefix("ok installed ") else {
            throw VdbError.unexpected(answer)
        }
        return String(answer.dropFirst("ok installed ".count))
    }

    /// Opens an installed app, by its folder's name, such as `Dice.app`.
    public func launch(_ app: String) throws {
        try require("launch")
        try command("launch \(app)")
    }

    /// Passes on what apps print for `seconds`, or until the connection ends when it's `nil`, keeping the
    /// connection open meanwhile.
    public func follow(for seconds: Double?) throws {
        let deadline = seconds.map { Date().addingTimeInterval($0) }
        while true {
            var wait = keepAlive
            if let deadline {
                wait = min(wait, deadline.timeIntervalSinceNow)
                if wait <= 0 { return }
            }

            if let line = try transport.receive(timeout: wait) {
                guard try isOutput(line) else {
                    throw VdbError.unexpected(line)
                }
            } else if deadline.map({ $0.timeIntervalSinceNow > 0 }) ?? true {
                try transport.send("")
            }
        }
    }

    /// Says goodbye.
    public func bye() throws {
        try command("bye")
    }

    /// Says hello, runs `body`, and says goodbye, also when VentureOS refuses something along the way, so the
    /// connection ends cleanly; the refusal is then thrown.
    public func session(identity: String, _ body: () throws -> Void) throws {
        try hello(identity: identity)
        do {
            try body()
        } catch VdbError.refused(let reason) {
            try? bye()
            throw VdbError.refused(reason)
        }
        try bye()
    }

    private func require(_ feature: String) throws {
        guard features.contains(feature) else {
            throw VdbError.refused("this VentureOS doesn't offer \(feature)")
        }
    }

    /// Sends a command and takes its answer: `ok …`, or a refusal.
    @discardableResult
    private func command(_ line: String, waiting: String? = nil) throws -> String {
        try transport.send(line)
        let answer = try answer(waiting: waiting ?? line.split(separator: " ").first.map(String.init) ?? line)
        guard answer == "ok" || answer.hasPrefix("ok ") else {
            throw VdbError.unexpected(answer)
        }
        return answer
    }

    /// The next line that isn't an app's output, with refusals and closings thrown.
    private func answer(waiting: String, timeout: Double? = nil) throws -> String {
        while true {
            guard let line = try transport.receive(timeout: timeout ?? answerTimeout) else {
                throw VdbError.timedOut(waiting)
            }
            if try isOutput(line) {
                continue
            }
            return line
        }
    }

    /// Passes on an app's output, or throws an error line; anything else is left to the caller.
    private func isOutput(_ line: String) throws -> Bool {
        if line.hasPrefix("out ") {
            output(String(line.dropFirst("out ".count)))
            return true
        }
        if line.hasPrefix("error closing ") {
            throw VdbError.closed(String(line.dropFirst("error closing ".count)))
        }
        if line.hasPrefix("error ") {
            throw VdbError.refused(String(line.dropFirst("error ".count)))
        }
        return false
    }
}
