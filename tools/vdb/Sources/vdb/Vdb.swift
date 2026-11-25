import ArgumentParser
import Foundation
import Logging
import VdbKit

/// The `vdb` command: sends apps to a running VentureOS, over vdb/1.
@main
struct Vdb: ParsableCommand {
    static let configuration = CommandConfiguration(
        commandName: "vdb",
        abstract: "Sends apps to a running VentureOS.",
        discussion: """
            VentureOS listens while its desktop runs and developer mode is on (Settings ▸ Developer). \
            Under QEMU, ./run.sh --vdb forwards its port, \(VdbProtocol.defaultPort), to this computer. \
            The first time, VentureOS asks whether to trust this computer.
            """,
        version: "\(GitVersion.version) (\(VdbProtocol.version))",
        subcommands: [Install.self, Launch.self, Mcp.self, Identity.self]
    )
}

/// Where VentureOS is, and how much to say.
struct ConnectionOptions: ParsableArguments {
    @Option(help: "The computer VentureOS runs on.")
    var host = "127.0.0.1"

    @Option(help: "The port it listens on.")
    var port = VdbProtocol.defaultPort

    @Flag(name: .shortAndLong, help: "Log every line sent and received, on standard error.")
    var verbose = false

    func validate() throws {
        guard (1...65_535).contains(port) else {
            throw ValidationError("--port is from 1 to 65535.")
        }
    }
}

/// How long to show what apps print.
struct FollowOptions: ParsableArguments {
    @Flag(help: "After opening the app, print what apps print until Ctrl+C.")
    var follow = false

    @Option(name: .customLong("for"), help: ArgumentHelp("After opening the app, print what apps print for this many seconds.", valueName: "seconds"))
    var seconds: Double?

    func validate() throws {
        if let seconds, !(seconds > 0) {
            throw ValidationError("--for takes a number of seconds above 0.")
        }
    }

    /// Whether to follow, and for how long; `nil` inside means until Ctrl+C.
    var duration: Double?? {
        if let seconds { return .some(seconds) }
        return follow ? .some(nil) : .none
    }
}

struct Install: ParsableCommand {
    static let configuration = CommandConfiguration(
        abstract: "Sends an app and installs it in /mnt/user/apps, replacing any copy there.")

    @Argument(help: "The app's folder, such as Dice.app.")
    var app: String

    @Flag(help: "Open the app once it's installed.")
    var launch = false

    @OptionGroup var follow: FollowOptions
    @OptionGroup var connection: ConnectionOptions

    func validate() throws {
        if follow.duration != nil && !launch {
            throw ValidationError("--follow and --for show an app's output, so they need --launch.")
        }
    }

    func run() throws {
        let bundle = try Session.reporting { try VdbBundle.read(from: URL(fileURLWithPath: app)) }
        try Session.run(connection) { client, log in
            let installed = try client.install(bundle)
            log.info("Installed \(installed)")
            Session.status("Installed \(installed).")
            if launch {
                try client.launch(bundle.name)
                Session.status("Opened \(bundle.name).")
                if let duration = follow.duration {
                    try client.follow(for: duration)
                }
            }
        }
    }
}

struct Launch: ParsableCommand {
    static let configuration = CommandConfiguration(
        abstract: "Opens an app on VentureOS, by its folder's name, the user's copy first.")

    @Argument(help: "The app's folder name, such as Dice.app.")
    var app: String

    @OptionGroup var follow: FollowOptions
    @OptionGroup var connection: ConnectionOptions

    func run() throws {
        try Session.run(connection) { client, _ in
            try client.launch(app)
            Session.status("Opened \(app).")
            if let duration = follow.duration {
                try client.follow(for: duration)
            }
        }
    }
}

struct Mcp: ParsableCommand {
    static let configuration = CommandConfiguration(
        abstract: "Serves VentureOS's app windows to an agent over MCP, on standard input and output.",
        discussion: """
            An agent sees app windows and uses their controls by name, as app tests do, presses keys, takes \
            screenshots, and installs and opens apps. It connects to VentureOS when a tool first needs it, and again \
            whenever the connection has closed or sat unused. Panels and VentureOS's own windows can't be reached. \
            Add it to an agent as a stdio server: vdb mcp [--host …] [--port …].
            """)

    @OptionGroup var connection: ConnectionOptions

    func run() throws {
        // Standard output is the protocol's alone, so everything vdb says goes to standard error.
        Session.bootstrapLogging(verbose: connection.verbose)
        let log = Logger(label: "vdb")
        let identity = try Session.identity()
        let tools = VdbAgentTools { output in
            let transport = LoggedTransport(try TCPConnection(host: connection.host, port: connection.port), log: log)
            let client = VdbClient(transport: transport, output: output, status: { log.notice("\($0)") })
            try client.hello(identity: identity)
            return client
        }
        MCPServer(tools: tools, version: GitVersion.version).run(
            input: { readLine(strippingNewline: true) },
            output: { line in FileHandle.standardOutput.write(Data((line + "\n").utf8)) })
        tools.close()
    }
}

struct Identity: ParsableCommand {
    static let configuration = CommandConfiguration(
        abstract: "Prints this computer's identity, making one the first time.",
        discussion: """
            VentureOS asks to trust a computer by its identity. Put the lines --lua prints in \
            rootfs/system/developer.local.lua, which git ignores, and your builds trust this computer at every boot.
            """)

    @Flag(help: "Print the lines for developer.local.lua instead.")
    var lua = false

    func run() throws {
        let identity = try Session.identity()
        print(lua ? VdbIdentity.luaLines(for: identity) : identity)
    }
}

/// What every command that connects does: say hello, run, and say goodbye, with errors reported plainly.
enum Session {
    static func run(_ options: ConnectionOptions, _ body: (VdbClient, Logger) throws -> Void) throws {
        bootstrapLogging(verbose: options.verbose)
        let log = Logger(label: "vdb")

        do {
            let identity = try identity()
            let transport = LoggedTransport(try TCPConnection(host: options.host, port: options.port), log: log)
            let client = VdbClient(
                transport: transport,
                // Written straight away, unbuffered, so each line shows as the app prints it.
                output: { line in FileHandle.standardOutput.write(Data((line + "\n").utf8)) },
                status: status)
            try client.session(identity: identity) { try body(client, log) }
        } catch let error as VdbError {
            status("vdb: \(error)")
            throw ExitCode.failure
        }
    }

    /// Logs to standard error: every line both ways with --verbose, otherwise only warnings.
    static func bootstrapLogging(verbose: Bool) {
        LoggingSystem.bootstrap { label in
            var handler = StreamLogHandler.standardError(label: label)
            handler.logLevel = verbose ? .debug : .warning
            return handler
        }
    }

    /// This computer's identity, from its file.
    static func identity() throws -> String {
        try reporting { try VdbIdentity.load(from: VdbIdentity.defaultFile()) }
    }

    /// Runs `body`, reporting a vdb error on standard error and exiting with failure.
    static func reporting<Result>(_ body: () throws -> Result) throws -> Result {
        do {
            return try body()
        } catch let error as VdbError {
            status("vdb: \(error)")
            throw ExitCode.failure
        }
    }

    /// Says something for the person on standard error, leaving standard output to what apps print.
    static func status(_ text: String) {
        FileHandle.standardError.write(Data((text + "\n").utf8))
    }
}

/// Logs each line both ways at debug level, for --verbose.
final class LoggedTransport: LineTransport {
    private let inner: LineTransport
    private let log: Logger

    init(_ inner: LineTransport, log: Logger) {
        self.inner = inner
        self.log = log
    }

    func send(_ line: String) throws {
        log.debug("> \(Self.shorten(line))")
        try inner.send(line)
    }

    func receive(timeout: Double) throws -> String? {
        let line = try inner.receive(timeout: timeout)
        if let line { log.debug("< \(Self.shorten(line))") }
        return line
    }

    private static func shorten(_ line: String) -> String {
        line.count <= 120 ? line : String(line.prefix(120)) + "…"
    }
}
