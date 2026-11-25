import Foundation

/// An app window, as `windows` lists it.
public struct VdbWindow: Codable, Equatable, Sendable {
    public var id: Int
    public var app: String?
    public var title: String?
    public var isFront: Bool
    public var isMinimized: Bool

    enum CodingKeys: String, CodingKey {
        case id, app, title
        case isFront = "front"
        case isMinimized = "minimized"
    }

    init(_ line: VdbAnswerLine) throws {
        guard line.kind == "window", let id = line.int("id") else {
            throw VdbError.unexpected("a window without its number")
        }
        self.id = id
        app = line.values["app"]
        title = line.values["title"]
        isFront = line.flags.contains("front")
        isMinimized = line.flags.contains("minimized")
    }
}

/// A notification in VentureOS's history, as `notifications` gives it.
public struct VdbNotification: Codable, Equatable, Sendable {
    /// The app it's from, or `nil` for VentureOS itself.
    public var app: String?
    public var title: String
    public var text: String
    /// How long ago it came.
    public var seconds: Int

    init(_ line: VdbAnswerLine) throws {
        guard line.kind == "notification", let title = line.values["title"], let text = line.values["text"] else {
            throw VdbError.unexpected("a notification without its title or text")
        }
        app = line.values["app"]
        self.title = title
        self.text = text
        seconds = line.int("seconds") ?? 0
    }
}

/// What an app window shows, as `snapshot` gives it.
public struct VdbSnapshot: Codable, Equatable, Sendable {
    public var window: Int
    public var title: String?
    public var subtitle: String?
    public var pane: String?
    public var focused: String?
    public var controls: [VdbControl]
}

/// One thing in a window a person could read or use.
public struct VdbControl: Codable, Equatable, Sendable {
    public var name: String
    /// The element, as a document writes it, such as `button` or `textField`, or `sidebar`, `titleField`,
    /// `toolbarItem`, or `menuItem`.
    public var kind: String
    public var text: String?
    public var value: Double?
    /// The chosen row, counting from 1.
    public var chosen: Int?
    /// How many rows a list, sidebar, or grid has; `rows` holds the first 100.
    public var rowCount: Int?
    public var rows: [String]?
    public var hidden: Bool
    public var dimmed: Bool
    public var checked: Bool
    public var focused: Bool
    /// Whether some of its text was cut to fit in a line.
    public var cut: Bool

    enum CodingKeys: String, CodingKey {
        case name, kind, text, value, chosen, rowCount, rows, hidden, dimmed, checked, focused, cut
    }

    /// Flags only when they're on, so an agent reads a short answer: a missing flag is off.
    public func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(name, forKey: .name)
        try container.encode(kind, forKey: .kind)
        try container.encodeIfPresent(text, forKey: .text)
        try container.encodeIfPresent(value, forKey: .value)
        try container.encodeIfPresent(chosen, forKey: .chosen)
        try container.encodeIfPresent(rowCount, forKey: .rowCount)
        try container.encodeIfPresent(rows, forKey: .rows)
        for (flag, key) in [(hidden, CodingKeys.hidden), (dimmed, .dimmed), (checked, .checked), (focused, .focused), (cut, .cut)] where flag {
            try container.encode(true, forKey: key)
        }
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        name = try container.decode(String.self, forKey: .name)
        kind = try container.decode(String.self, forKey: .kind)
        text = try container.decodeIfPresent(String.self, forKey: .text)
        value = try container.decodeIfPresent(Double.self, forKey: .value)
        chosen = try container.decodeIfPresent(Int.self, forKey: .chosen)
        rowCount = try container.decodeIfPresent(Int.self, forKey: .rowCount)
        rows = try container.decodeIfPresent([String].self, forKey: .rows)
        hidden = try container.decodeIfPresent(Bool.self, forKey: .hidden) ?? false
        dimmed = try container.decodeIfPresent(Bool.self, forKey: .dimmed) ?? false
        checked = try container.decodeIfPresent(Bool.self, forKey: .checked) ?? false
        focused = try container.decodeIfPresent(Bool.self, forKey: .focused) ?? false
        cut = try container.decodeIfPresent(Bool.self, forKey: .cut) ?? false
    }

    init(_ line: VdbAnswerLine) throws {
        guard let name = line.values["name"], let kind = line.values["kind"] else {
            throw VdbError.unexpected("a control without its name or kind")
        }
        self.name = name
        self.kind = kind
        text = line.values["text"]
        value = line.double("value")
        chosen = line.int("chosen")
        rowCount = line.int("rows")
        rows = rowCount == nil ? nil : []
        hidden = line.flags.contains("hidden")
        dimmed = line.flags.contains("dimmed")
        checked = line.flags.contains("checked")
        focused = line.flags.contains("focused")
        cut = line.flags.contains("cut")
    }
}

/// Something done to a control by name, as app tests do it.
public enum VdbAction: Equatable, Sendable {
    /// Presses a button, toolbar item, or menu item.
    case press(String)
    /// Puts text in a field, or the title bar's field, and presses Enter.
    case submit(String, text: String)
    /// Puts text in a field or text area, without Enter.
    case type(String, text: String)
    /// Chooses a list's or sidebar's row, or a grid's item, by its number, counting from 1.
    case chooseRow(String, row: Int)
    /// Chooses a row by its text, or a grid's item by its title or name.
    case chooseText(String, text: String)

    /// The words after `act <window>`.
    var words: String {
        switch self {
        case .press(let name): "press \(VdbWords.word(name))"
        case .submit(let name, let text): "submit \(VdbWords.word(name)) \(VdbWords.quote(text))"
        case .type(let name, let text): "type \(VdbWords.word(name)) \(VdbWords.quote(text))"
        case .chooseRow(let name, let row): "choose \(VdbWords.word(name)) \(row)"
        case .chooseText(let name, let text): "choose \(VdbWords.word(name)) \(VdbWords.quote(text))"
        }
    }
}

/// The screen's pixels, three bytes each, red, green, then blue, a row at a time from the top.
public struct VdbScreenshot: Equatable, Sendable {
    public var width: Int
    public var height: Int
    public var rgb: [UInt8]

    /// The screenshot as a PNG.
    public func png() -> [UInt8] {
        PNG.encode(width: width, height: height, rgb: rgb)
    }
}
