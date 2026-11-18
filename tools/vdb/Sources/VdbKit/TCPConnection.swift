import Foundation
#if canImport(Glibc)
import Glibc
#elseif canImport(Musl)
import Musl
#elseif canImport(Darwin)
import Darwin
#elseif os(Windows)
import WinSDK
#endif

/// A TCP connection carrying lines, through the platform's own sockets: POSIX on Linux and macOS, Winsock on
/// Windows. It waits for data with `poll`, so a read never blocks past its timeout.
public final class TCPConnection: LineTransport {
    #if os(Windows)
    private typealias Handle = SOCKET
    private static let invalid = INVALID_SOCKET
    #else
    private typealias Handle = Int32
    private static let invalid: Int32 = -1
    #endif

    private var handle: Handle
    private var buffer: [UInt8] = []

    /// Connects to `host` and `port`, trying each address the name has.
    public init(host: String, port: Int) throws {
        guard (1...65_535).contains(port) else {
            throw VdbError.connection("\(port) isn't a port, which is from 1 to 65535.")
        }
        #if os(Windows)
        var data = WSADATA()
        guard WSAStartup(0x0202, &data) == 0 else {
            throw VdbError.connection("Winsock couldn't start.")
        }
        #endif

        var hints = addrinfo()
        hints.ai_family = AF_UNSPEC
        #if os(Windows) || canImport(Darwin) || canImport(Musl)
        hints.ai_socktype = SOCK_STREAM
        #else
        hints.ai_socktype = Int32(SOCK_STREAM.rawValue)
        #endif
        var list: UnsafeMutablePointer<addrinfo>?
        let found = getaddrinfo(host, String(port), &hints, &list)
        guard found == 0, let first = list else {
            throw VdbError.connection("\(host) couldn't be found.")
        }
        defer { freeaddrinfo(first) }

        var connected: Handle = Self.invalid
        var entry: UnsafeMutablePointer<addrinfo>? = first
        while let current = entry {
            let candidate = socket(current.pointee.ai_family, current.pointee.ai_socktype, current.pointee.ai_protocol)
            if candidate != Self.invalid {
                if connect(candidate, current.pointee.ai_addr, socklen_t(current.pointee.ai_addrlen)) == 0 {
                    connected = candidate
                    break
                }
                Self.close(candidate)
            }
            entry = current.pointee.ai_next
        }

        guard connected != Self.invalid else {
            throw VdbError.connection("""
                Nothing answered at \(host):\(port). Is VentureOS's desktop running, with developer mode on, \
                and its port forwarded (./run.sh --vdb)?
                """)
        }
        handle = connected

        #if canImport(Darwin)
        // A write to a closed connection is an error here, not a signal that ends vdb.
        var on: Int32 = 1
        setsockopt(handle, SOL_SOCKET, SO_NOSIGPIPE, &on, socklen_t(MemoryLayout<Int32>.size))
        #endif
    }

    deinit {
        Self.close(handle)
        #if os(Windows)
        WSACleanup()
        #endif
    }

    public func send(_ line: String) throws {
        let bytes = Array((line + "\n").utf8)
        var sent = 0
        while sent < bytes.count {
            let count = bytes[sent...].withUnsafeBytes { raw -> Int in
                #if os(Windows)
                Int(WinSDK.send(handle, raw.baseAddress?.assumingMemoryBound(to: CChar.self), Int32(raw.count), 0))
                #elseif canImport(Darwin)
                Darwin.send(handle, raw.baseAddress, raw.count, 0)
                #elseif canImport(Musl)
                Musl.send(handle, raw.baseAddress, raw.count, Int32(MSG_NOSIGNAL))
                #else
                Glibc.send(handle, raw.baseAddress, raw.count, Int32(MSG_NOSIGNAL))
                #endif
            }
            guard count > 0 else {
                throw VdbError.connection("The connection to VentureOS broke while sending.")
            }
            sent += count
        }
    }

    public func receive(timeout: Double) throws -> String? {
        let deadline = Date().addingTimeInterval(max(0, timeout))
        while true {
            if let end = buffer.firstIndex(of: UInt8(ascii: "\n")) {
                var line = Array(buffer[..<end])
                buffer.removeSubrange(...end)
                if line.last == UInt8(ascii: "\r") { line.removeLast() }
                return String(decoding: line, as: UTF8.self)
            }

            let remaining = deadline.timeIntervalSinceNow
            guard remaining > 0, try wait(milliseconds: Int32(min(remaining * 1000, Double(Int32.max)))) else {
                return nil
            }

            var chunk = [UInt8](repeating: 0, count: 4096)
            let count = chunk.withUnsafeMutableBytes { raw -> Int in
                #if os(Windows)
                Int(recv(handle, raw.baseAddress?.assumingMemoryBound(to: CChar.self), Int32(raw.count), 0))
                #else
                recv(handle, raw.baseAddress, raw.count, 0)
                #endif
            }
            guard count > 0 else {
                throw VdbError.connection("VentureOS closed the connection.")
            }
            buffer.append(contentsOf: chunk[..<count])
        }
    }

    /// Whether data arrived within the time.
    private func wait(milliseconds: Int32) throws -> Bool {
        #if os(Windows)
        var entry = WSAPOLLFD(fd: handle, events: Int16(POLLRDNORM), revents: 0)
        let ready = WSAPoll(&entry, 1, milliseconds)
        #else
        var entry = pollfd(fd: handle, events: Int16(POLLIN), revents: 0)
        let ready = poll(&entry, 1, milliseconds)
        #endif
        guard ready >= 0 else {
            throw VdbError.connection("Waiting for VentureOS failed.")
        }
        return ready > 0
    }

    private static func close(_ handle: Handle) {
        #if os(Windows)
        closesocket(handle)
        #elseif canImport(Darwin)
        Darwin.close(handle)
        #elseif canImport(Musl)
        Musl.close(handle)
        #else
        Glibc.close(handle)
        #endif
    }
}
