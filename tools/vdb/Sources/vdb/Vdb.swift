import ArgumentParser
import VdbKit

/// The `vdb` command. Its subcommands, `identity`, `install`, and `launch`, arrive with the protocol.
@main
struct Vdb: ParsableCommand {
    static let configuration = CommandConfiguration(
        commandName: "vdb",
        abstract: "Sends apps to a running VentureOS.",
        discussion: "Speaks \(VdbProtocol.version), on port \(VdbProtocol.defaultPort) by default.",
        version: "\(GitVersion.version) (\(VdbProtocol.version))"
    )
}
