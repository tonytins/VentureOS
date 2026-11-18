import Foundation
import Testing
import VdbKit

struct VdbProtocolTests {
    @Test func theLargestDataLineFitsInALine() {
        let line = "data " + Data(count: VdbProtocol.maximumDataBytes).base64EncodedString()
        #expect(line.count <= VdbProtocol.maximumLineLength)
    }

    @Test func oneMoreDataByteDoesNotFit() {
        // Base64 grows four characters for each three bytes, so the next size that changes the line is three more.
        let line = "data " + Data(count: VdbProtocol.maximumDataBytes + 3).base64EncodedString()
        #expect(line.count > VdbProtocol.maximumLineLength)
    }

    @Test func theValuesMatchVentureOSs() {
        #expect(VdbProtocol.version == "vdb/1")
        #expect(VdbProtocol.defaultPort == 28455)
    }
}
