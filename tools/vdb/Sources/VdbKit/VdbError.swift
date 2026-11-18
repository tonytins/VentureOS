/// Why vdb couldn't do what it was asked.
public enum VdbError: Error, Equatable, CustomStringConvertible {
    /// The app can't be sent as it is, before anything is sent.
    case invalidBundle(String)

    /// VentureOS refused a command and said why; the connection may go on.
    case refused(String)

    /// VentureOS closed the connection and said why.
    case closed(String)

    /// VentureOS said something vdb doesn't understand.
    case unexpected(String)

    /// The connection couldn't be made, or broke.
    case connection(String)

    /// VentureOS said nothing for too long.
    case timedOut(String)

    /// The identity couldn't be read or made.
    case identity(String)

    public var description: String {
        switch self {
        case .invalidBundle(let reason): return reason
        case .refused(let reason): return "VentureOS refused: \(reason)"
        case .closed(let reason): return "VentureOS closed the connection: \(reason)"
        case .unexpected(let line): return "VentureOS said something vdb doesn't understand: \(line)"
        case .connection(let reason): return reason
        case .timedOut(let waiting): return "VentureOS didn't answer \(waiting) in time."
        case .identity(let reason): return reason
        }
    }
}
