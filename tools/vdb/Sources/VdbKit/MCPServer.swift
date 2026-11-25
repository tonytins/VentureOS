import Foundation

/// A Model Context Protocol server on newline-delimited JSON-RPC, as MCP's stdio transport has it, answering
/// `initialize`, `ping`, `tools/list`, and `tools/call`, and nothing more: vdb offers tools, not resources or prompts.
///
/// It's written by hand rather than with an MCP library, to keep vdb's dependencies to the approved few; three
/// methods and one protocol version are little to follow.
public final class MCPServer {
    /// The protocol versions it speaks, newest first; it answers in the client's when it's one of these.
    public static let protocolVersions = ["2025-06-18", "2025-03-26", "2024-11-05"]

    private let tools: VdbAgentTools
    private let version: String

    public init(tools: VdbAgentTools, version: String) {
        self.tools = tools
        self.version = version
    }

    /// Reads requests from `input` until it ends, writing each answer to `output` as one line.
    public func run(input: () -> String?, output: (String) -> Void) {
        while let line = input() {
            if let answer = handle(line) {
                output(answer)
            }
        }
    }

    /// The answer to one line, or `nil` for a notification, which is never answered.
    public func handle(_ line: String) -> String? {
        guard !line.trimmingCharacters(in: .whitespaces).isEmpty else { return nil }
        guard let message = try? JSONDecoder().decode(JSONValue.self, from: Data(line.utf8)) else {
            return encode(error: -32700, "That isn't JSON.", id: .null)
        }
        guard let request = message.object, let method = request["method"]?.string else {
            return encode(error: -32600, "A request is a JSON object with a method.", id: .null)
        }
        guard let id = request["id"], id != .null else {
            // notifications/initialized, notifications/cancelled, and the rest ask for nothing back.
            return nil
        }
        let params = request["params"]?.object ?? [:]

        switch method {
        case "initialize":
            let asked = params["protocolVersion"]?.string
            return encode(result: [
                "protocolVersion": asked.flatMap { Self.protocolVersions.contains($0) ? $0 : nil } ?? Self.protocolVersions[0],
                "capabilities": ["tools": [String: Any]()],
                "serverInfo": ["name": "vdb", "version": version],
                "instructions": VdbAgentTools.instructions,
            ], id: id)
        case "ping":
            return encode(result: [String: Any](), id: id)
        case "tools/list":
            return encode(result: ["tools": VdbAgentTools.definitions], id: id)
        case "tools/call":
            guard let name = params["name"]?.string, VdbAgentTools.names.contains(name) else {
                return encode(error: -32602, "There's no tool \(params["name"]?.string ?? "without a name"); tools/list lists them.", id: id)
            }
            let result = tools.call(name, arguments: params["arguments"]?.object ?? [:])
            return encode(result: result.json, id: id)
        default:
            return encode(error: -32601, "vdb doesn't answer \(method).", id: id)
        }
    }

    private func encode(result: [String: Any], id: JSONValue) -> String {
        line(["jsonrpc": "2.0", "id": id.any, "result": result])
    }

    private func encode(error code: Int, _ message: String, id: JSONValue) -> String {
        line(["jsonrpc": "2.0", "id": id.any, "error": ["code": code, "message": message]])
    }

    private func line(_ object: [String: Any]) -> String {
        // JSONSerialization never breaks a line without .prettyPrinted, so each answer is one line.
        guard let data = try? JSONSerialization.data(withJSONObject: object, options: [.sortedKeys, .withoutEscapingSlashes]),
              let text = String(data: data, encoding: .utf8) else {
            return #"{"jsonrpc":"2.0","id":null,"error":{"code":-32603,"message":"vdb couldn't write its answer as JSON."}}"#
        }
        return text
    }
}

/// A JSON value as a request carries it. Decoded with `JSONDecoder`, which keeps `true` and `1` apart on every
/// platform, where `JSONSerialization`'s numbers can pass for booleans.
enum JSONValue: Decodable, Equatable {
    case null
    case bool(Bool)
    case number(Double)
    case string(String)
    case array([JSONValue])
    case object([String: JSONValue])

    init(from decoder: Decoder) throws {
        let container = try decoder.singleValueContainer()
        if container.decodeNil() {
            self = .null
        } else if let value = try? container.decode(Bool.self) {
            self = .bool(value)
        } else if let value = try? container.decode(Double.self) {
            self = .number(value)
        } else if let value = try? container.decode(String.self) {
            self = .string(value)
        } else if let value = try? container.decode([JSONValue].self) {
            self = .array(value)
        } else {
            self = .object(try container.decode([String: JSONValue].self))
        }
    }

    /// As `JSONSerialization` writes it, for echoing a request's ID.
    var any: Any {
        switch self {
        case .null: NSNull()
        case .bool(let value): value
        case .number(let value): value == value.rounded() && abs(value) < 9e15 ? Int(value) as Any : value
        case .string(let value): value
        case .array(let values): values.map(\.any)
        case .object(let values): values.mapValues(\.any)
        }
    }

    var string: String? { if case .string(let value) = self { value } else { nil } }

    var object: [String: JSONValue]? { if case .object(let value) = self { value } else { nil } }
}
