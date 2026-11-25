import Foundation
import Testing
@testable import VdbKit

/// A client over a recorded conversation, said hello on, and the transport that plays VentureOS.
private func replaying(_ name: String) throws -> (VdbClient, ScriptedTransport, Conversation) {
    let conversation = try Conversation.read(ConversationTests.folder.appendingPathComponent(name))
    let transport = ScriptedTransport(conversation.steps, identity: conversation.identity)
    let client = VdbClient(transport: transport, output: { _ in }, nonce: { conversation.clientNonce })
    client.answerTimeout = 0
    try client.hello(identity: conversation.identity)
    return (client, transport, conversation)
}

/// The `ui` conversations, which VentureOS's tests replay too, from vdb's side.
struct UIConversationTests {
    static let conversations = [
        "ui-windows-and-snapshot.txt", "ui-act.txt", "ui-keys.txt", "ui-screenshot.txt", "ui-notifications.txt",
    ]

    /// Every command, as written, is answered as recorded: the lines before `ok`, or the refusal.
    @Test(arguments: conversations)
    func replays(_ name: String) throws {
        let (client, transport, conversation) = try replaying(name)
        #expect(client.offers("ui"))

        let commands = conversation.steps.compactMap { step -> String? in
            guard case .sent(let line) = step, !line.hasPrefix("hello "), line != "bye" else { return nil }
            return line
        }
        for command in commands {
            do {
                _ = try client.uiCommand(command)
            } catch VdbError.refused {
                continue
            }
        }
        try client.bye()

        #expect(transport.mismatches.isEmpty, "\(name): \(transport.mismatches)")
        #expect(transport.unplayed.isEmpty, "\(name) has more: \(transport.unplayed)")
    }

    @Test func readsWindowsAndASnapshot() throws {
        let (client, _, _) = try replaying("ui-windows-and-snapshot.txt")

        let windows = try client.windows()
        #expect(windows.map(\.id) == [4, 3])
        #expect(windows[0].app == "Notes" && windows[0].title == "Groceries" && windows[0].isFront)
        #expect(!windows[1].isFront && !windows[1].isMinimized)

        let snapshot = try client.snapshot(window: 3)
        #expect(snapshot.title == "Notes" && snapshot.subtitle == "2 notes" && snapshot.pane == nil)
        #expect(snapshot.controls.map(\.name) == ["label", "ok", "name", "notes", "newNote"])
        #expect(snapshot.controls[0].text == "Say \"hi\"")
        #expect(snapshot.controls[2].kind == "textField" && snapshot.controls[2].text == "")
        #expect(snapshot.controls[3].rows == ["Milk", "Eggs"] && snapshot.controls[3].rowCount == 2)
        #expect(snapshot.controls[1].rows == nil)

        #expect(throws: VdbError.refused("there's no app window 1; windows lists them")) { try client.snapshot(window: 1) }
    }

    @Test func aControlsJSONLeavesOutWhatIsOff() throws {
        let control = try VdbControl(VdbAnswerLine(#"control name="ok" kind=button text="OK" dimmed"#))
        let encoder = JSONEncoder()
        encoder.outputFormatting = .sortedKeys

        let json = String(decoding: try encoder.encode(control), as: UTF8.self)

        #expect(json == #"{"dimmed":true,"kind":"button","name":"ok","text":"OK"}"#)
        #expect(try JSONDecoder().decode(VdbControl.self, from: Data(json.utf8)) == control)
    }

    @Test func readsTheNotificationHistory() throws {
        let (client, _, _) = try replaying("ui-notifications.txt")

        let notifications = try client.notifications()

        #expect(notifications.map(\.text) == ["A computer connected over vdb.", "Saved 3 notes."])
        #expect(notifications[0].app == nil && notifications[0].title == "VentureOS")
        #expect(notifications[1].app == "Notes" && notifications[1].seconds == 0)
    }

    @Test func actionsAreWrittenAsVentureOSReadsThem() {
        #expect(VdbAction.press("ok").words == "press ok")
        #expect(VdbAction.submit("name", text: "Ada Lovelace").words == "submit name \"Ada Lovelace\"")
        #expect(VdbAction.chooseText("notes", text: "Eggs").words == "choose notes \"Eggs\"")
        #expect(VdbAction.chooseRow("notes", row: 2).words == "choose notes 2")
        #expect(VdbAction.press("two words").words == "press \"two words\"")
    }

    @Test func takesAScreenshotAndRefusesOneThatCantBeRead() throws {
        let (client, _, _) = try replaying("ui-screenshot.txt")

        #expect(throws: VdbError.refused("the screen couldn't be read")) { try client.screenshot() }
        let shot = try client.screenshot()
        #expect(shot == VdbScreenshot(width: 2, height: 1, rgb: [0, 1, 2, 3, 4, 5]))
    }

    @Test func refusesUIWhenVentureOSDoesntOfferIt() throws {
        let transport = ScriptedTransport([ClientTests.hello, ClientTests.welcome, .answered("ok vdb/1 install launch")])
        let client = VdbClient(transport: transport, output: { _ in }, nonce: { ClientTests.nonce })
        try client.hello(identity: ClientTests.identity)

        #expect(throws: VdbError.refused("this VentureOS doesn't offer ui")) { try client.windows() }
    }
}

struct WordsTests {
    @Test(arguments: ["plain", "two words", "say \"hi\" \\ there", "one\ntwo\r\tthree", "", "é😀"])
    func whatIsQuotedSplitsBackToItself(_ text: String) throws {
        #expect(try VdbWords.split("3 \(VdbWords.quote(text)) x") == ["3", text, "x"])
    }

    @Test func plainWordsGoBare() {
        #expect(VdbWords.word("newNote") == "newNote")
        #expect(VdbWords.word("Ctrl+Z") == "\"Ctrl+Z\"")
        #expect(VdbWords.word("") == "\"\"")
    }

    @Test func readsKeysValuesAndFlags() throws {
        let line = try VdbAnswerLine(#"control name="a b=c" kind=button text="x \"y\"" value=0.5 dimmed later=7 cut"#)
        #expect(line.kind == "control")
        #expect(line.values["name"] == "a b=c")
        #expect(line.values["text"] == "x \"y\"")
        #expect(line.double("value") == 0.5)
        #expect(line.flags == ["dimmed", "cut"])
        #expect(line.int("later") == 7)
    }

    @Test func refusesAnUnclosedQuote() {
        #expect(throws: VdbError.self) { try VdbWords.split("\"open") }
        #expect(throws: VdbError.self) { try VdbAnswerLine("row \"open") }
    }
}

struct PNGTests {
    /// Reads the PNG back: its chunks, their checksums, and the stored blocks' bytes.
    @Test func writesAPNGThatReadsBack() throws {
        let rgb: [UInt8] = (0..<(3 * 2 * 3)).map { UInt8($0 * 13 % 256) }
        let png = PNG.encode(width: 3, height: 2, rgb: rgb)

        #expect(Array(png.prefix(8)) == [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])
        var chunks: [(String, [UInt8])] = []
        var at = 8
        while at < png.count {
            let length = png[at..<(at + 4)].reduce(0) { $0 << 8 | Int($1) }
            let typed = Array(png[(at + 4)..<(at + 8 + length)])
            let crc = png[(at + 8 + length)..<(at + 12 + length)].reduce(UInt32(0)) { $0 << 8 | UInt32($1) }
            #expect(PNG.crc32(typed) == crc)
            chunks.append((String(decoding: typed.prefix(4), as: UTF8.self), Array(typed.dropFirst(4))))
            at += 12 + length
        }
        #expect(chunks.map(\.0) == ["IHDR", "IDAT", "IEND"])
        #expect(chunks[0].1 == [0, 0, 0, 3, 0, 0, 0, 2, 8, 2, 0, 0, 0])

        // One stored block: zlib's header, then final, length, its complement, the rows, and Adler-32.
        let stream = chunks[1].1
        let rows: [UInt8] = [0] + rgb[0..<9] + [0] + rgb[9..<18]
        #expect(Array(stream.prefix(3)) == [0x78, 0x01, 1])
        #expect(Array(stream[3..<7]) == [UInt8(rows.count), 0, ~UInt8(rows.count), 0xFF])
        #expect(Array(stream[7..<(7 + rows.count)]) == rows)
        #expect(stream.suffix(4).reduce(UInt32(0)) { $0 << 8 | UInt32($1) } == PNG.adler32(rows))
    }

    @Test func splitsLongDataIntoBlocksOfAtMost65535Bytes() {
        let stream = PNG.zlib([UInt8](repeating: 7, count: 70_000))
        #expect(stream[2] == 0 && stream[3] == 0xFF && stream[4] == 0xFF)
        let second = 2 + 5 + 65_535
        #expect(stream[second] == 1)
        #expect(Int(stream[second + 1]) | Int(stream[second + 2]) << 8 == 70_000 - 65_535)
    }

    @Test func checksumsMatchTheStandardsExamples() {
        // CRC-32 and Adler-32 of "123456789" and "Wikipedia", their usual check values.
        #expect(PNG.crc32(Array("123456789".utf8)) == 0xCBF4_3926)
        #expect(PNG.adler32(Array("Wikipedia".utf8)) == 0x11E6_0398)
    }
}

/// `vdb mcp`'s side of MCP, over recorded conversations.
struct MCPTests {
    private func server(_ steps: [Conversation.Step]) -> (MCPServer, ScriptedTransport) {
        let transport = ScriptedTransport(steps)
        let tools = VdbAgentTools(connect: { output in
            let client = VdbClient(transport: transport, output: output, nonce: { ClientTests.nonce })
            client.answerTimeout = 0
            try client.hello(identity: ClientTests.identity)
            return client
        })
        return (MCPServer(tools: tools, version: "test"), transport)
    }

    private static let welcomed: [Conversation.Step] = [ClientTests.hello, ClientTests.welcome, .answered("ok vdb/1 install launch ui")]

    private func json(_ line: String?) throws -> [String: Any] {
        let data = Data(try #require(line).utf8)
        return try #require(try JSONSerialization.jsonObject(with: data) as? [String: Any])
    }

    private func call(_ server: MCPServer, _ tool: String, _ arguments: String = "{}", id: Int = 2) throws -> (text: String, isError: Bool) {
        let answer = try json(server.handle(#"{"jsonrpc":"2.0","id":\#(id),"method":"tools/call","params":{"name":"\#(tool)","arguments":\#(arguments)}}"#))
        let result = try #require(answer["result"] as? [String: Any])
        let content = try #require(result["content"] as? [[String: Any]])
        return (content.compactMap { $0["text"] as? String }.joined(), result["isError"] as? Bool ?? false)
    }

    @Test func introducesItselfInTheClientsVersionOrItsOwn() throws {
        let (server, _) = server([])

        let known = try json(server.handle(#"{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26"}}"#))
        let unknown = try json(server.handle(#"{"jsonrpc":"2.0","id":"a","method":"initialize","params":{"protocolVersion":"1999-01-01"}}"#))

        #expect((known["result"] as? [String: Any])?["protocolVersion"] as? String == "2025-03-26")
        #expect((unknown["result"] as? [String: Any])?["protocolVersion"] as? String == MCPServer.protocolVersions[0])
        #expect(unknown["id"] as? String == "a")
        #expect(server.handle(#"{"jsonrpc":"2.0","method":"notifications/initialized"}"#) == nil)
    }

    @Test func listsNineToolsEachWithASchema() throws {
        let (server, _) = server([])

        let answer = try json(server.handle(#"{"jsonrpc":"2.0","id":1,"method":"tools/list"}"#))
        let tools = try #require((answer["result"] as? [String: Any])?["tools"] as? [[String: Any]])

        #expect(Set(tools.compactMap { $0["name"] as? String })
            == ["windows", "snapshot", "act", "key", "screenshot", "install", "launch", "output", "notifications"])
        #expect(tools.allSatisfy { ($0["inputSchema"] as? [String: Any])?["type"] as? String == "object" })
    }

    @Test func answersWhatIsntARequestWithAnError() throws {
        let (server, _) = server([])

        #expect(((try json(server.handle("not json")))["error"] as? [String: Any])?["code"] as? Int == -32700)
        #expect(((try json(server.handle(#"{"jsonrpc":"2.0","id":1,"method":"resources/list"}"#)))["error"] as? [String: Any])?["code"] as? Int == -32601)
        #expect(((try json(server.handle(#"{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"fly"}}"#)))["error"] as? [String: Any])?["code"] as? Int == -32602)
    }

    @Test func actsOnTheWindowInFrontAndStopsAtTheFirstStepThatCantBeDone() throws {
        let (server, transport) = server(Self.welcomed + [
            .sent("windows"), .answered(#"window id=2 app="Notes" title="Notes" front"#), .answered("ok"),
            .sent("act 2 press ok"), .answered("ok"),
            .sent("act 2 press missing"), .answered(#"error there's no "missing" in Notes"#),
        ])

        let result = try call(server, "act", #"{"steps":[{"action":"press","name":"ok"},{"action":"press","name":"missing"},{"action":"type","name":"x","text":"y"}]}"#)

        #expect(result.isError)
        #expect(result.text == #"Step 2 of 3 wasn't done: there's no "missing" in Notes. The 1 before it were done."#)
        #expect(transport.mismatches.isEmpty && transport.unplayed.isEmpty)
    }

    @Test func checksEveryStepBeforeDoingAny() throws {
        let (server, transport) = server(Self.welcomed)

        let result = try call(server, "act", #"{"window":2,"steps":[{"action":"press","name":"ok"},{"action":"fly","name":"x"}]}"#)

        #expect(result.isError)
        #expect(result.text == "Step 2's action is press, submit, type, or choose, not fly.")
        #expect(transport.heard.isEmpty)
    }

    @Test func refusesAWindowThatIsntAWholeNumber() throws {
        let (server, _) = server(Self.welcomed)

        #expect(try call(server, "snapshot", #"{"window":true}"#).text == "window is a whole number.")
        #expect(try call(server, "snapshot", #"{"window":1.5}"#).text == "window is a whole number.")
    }

    @Test func connectsAgainOnceTheConnectionHasSatUnused() throws {
        var clock = Date(timeIntervalSince1970: 0)
        let transports = [
            ScriptedTransport(Self.welcomed + [.sent("windows"), .answered("ok"), .sent("bye"), .answered("ok")]),
            ScriptedTransport(Self.welcomed + [.sent("windows"), .answered("ok")]),
        ]
        var made = 0
        let tools = VdbAgentTools(connect: { output in
            let client = VdbClient(transport: transports[made], output: output, nonce: { ClientTests.nonce })
            made += 1
            client.answerTimeout = 0
            try client.hello(identity: ClientTests.identity)
            return client
        }, now: { clock })
        let server = MCPServer(tools: tools, version: "test")

        #expect(try call(server, "windows").text == "[]")
        clock += 60
        #expect(try call(server, "windows").text == "[]")

        #expect(made == 2)
        #expect(transports.allSatisfy { $0.mismatches.isEmpty && $0.unplayed.isEmpty })
    }

    @Test func saysGoodbyeWhenTheAgentGoes() throws {
        let transport = ScriptedTransport(Self.welcomed + [.sent("windows"), .answered("ok"), .sent("bye"), .answered("ok")])
        let tools = VdbAgentTools(connect: { output in
            let client = VdbClient(transport: transport, output: output, nonce: { ClientTests.nonce })
            client.answerTimeout = 0
            try client.hello(identity: ClientTests.identity)
            return client
        })
        _ = tools.call("windows", arguments: [:])

        tools.close()

        #expect(transport.mismatches.isEmpty && transport.unplayed.isEmpty)
    }

    @Test func saysWhenVentureOSIsTooOldForIt() throws {
        let (server, _) = server([ClientTests.hello, ClientTests.welcome, .answered("ok vdb/1 install launch"), .sent("bye"), .answered("ok")])

        let result = try call(server, "windows")

        #expect(result.isError)
        #expect(result.text == "VentureOS refused: this VentureOS doesn't offer ui, so it's older than vdb mcp needs")
    }

    @Test func givesAScreenshotAsAPNG() throws {
        let (server, _) = server(Self.welcomed + [.sent("screenshot"), .answered("screen width=1 height=1"), .answered("data AAEC"), .answered("ok")])

        let answer = try json(server.handle(#"{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"screenshot"}}"#))
        let content = try #require(((answer["result"] as? [String: Any])?["content"] as? [[String: Any]])?.first)

        #expect(content["type"] as? String == "image" && content["mimeType"] as? String == "image/png")
        let data = try #require(content["data"] as? String)
        let png = try #require(Data(base64Encoded: data))
        #expect(Array(png) == PNG.encode(width: 1, height: 1, rgb: [0, 1, 2]))
    }
}
