import Foundation

/// The words of vdb/1's `ui` lines: bare, or quoted in `"` with `\\`, `\"`, `\n`, `\r`, and `\t` escaped. Answers are
/// a kind of line, then `key=value` words, a bare key being a flag that's on; keys vdb doesn't know are skipped, so
/// VentureOS can say more without a new feature.
public enum VdbWords {
    /// A word as VentureOS reads it: bare when it's plain letters, digits, `.`, `_`, or `-`, otherwise quoted.
    public static func word(_ text: String) -> String {
        let plain = !text.isEmpty && text.unicodeScalars.allSatisfy {
            ("a"..."z").contains($0) || ("A"..."Z").contains($0) || ("0"..."9").contains($0) || $0 == "." || $0 == "_" || $0 == "-"
        }
        return plain ? text : quote(text)
    }

    /// `text` in quotes, with what a line can't hold escaped.
    public static func quote(_ text: String) -> String {
        var quoted = "\""
        for character in text.unicodeScalars {
            switch character {
            case "\\": quoted += "\\\\"
            case "\"": quoted += "\\\""
            case "\n": quoted += "\\n"
            case "\r": quoted += "\\r"
            case "\t": quoted += "\\t"
            default: quoted.unicodeScalars.append(character)
            }
        }
        return quoted + "\""
    }

    /// Splits a line into its words, unquoting quoted ones.
    public static func split(_ line: String) throws -> [String] {
        var words: [String] = []
        var scalars = Substring(line).unicodeScalars[...]
        while let first = scalars.first {
            if first == " " {
                scalars.removeFirst()
                continue
            }
            guard first == "\"" else {
                let word = scalars.prefix { $0 != " " }
                scalars.removeFirst(word.count)
                words.append(String(String.UnicodeScalarView(word)))
                continue
            }

            scalars.removeFirst()
            var word = String.UnicodeScalarView()
            var closed = false
            while let character = scalars.popFirst() {
                if character == "\"" {
                    closed = true
                    break
                }
                guard character == "\\" else {
                    word.append(character)
                    continue
                }
                switch scalars.popFirst() {
                case "\\": word.append("\\")
                case "\"": word.append("\"")
                case "n": word.append("\n")
                case "r": word.append("\r")
                case "t": word.append("\t")
                default: throw VdbError.unexpected("\(line.prefix(80)) has an escape vdb doesn't know")
                }
            }
            guard closed else {
                throw VdbError.unexpected("\(line.prefix(80)) has a quoted word with no closing quote")
            }
            words.append(String(word))
        }
        return words
    }
}

/// One of the `ui` feature's answer lines, such as `control name="ok" kind=button dimmed`.
struct VdbAnswerLine {
    /// The first word, such as `window`, `control`, or `row`.
    let kind: String

    /// The words after it that aren't `key=value`, unquoted, in order, such as a row's text.
    let words: [String]

    /// The `key=value` words.
    let values: [String: String]

    /// The bare words after the first, which are flags that are on.
    var flags: Set<String> { Set(words) }

    init(_ line: String) throws {
        // Each word is read as written, so a quoted value's spaces and `=` stay inside it, then unquoted.
        var raw = try Self.rawWords(line)[...]
        guard let kind = raw.popFirst() else {
            throw VdbError.unexpected("an empty answer")
        }
        var values: [String: String] = [:]
        var words: [String] = []
        for word in raw {
            if !word.hasPrefix("\""), let equals = word.firstIndex(of: "=") {
                values[String(word[..<equals])] = try VdbWords.split(String(word[word.index(after: equals)...])).first ?? ""
            } else {
                words.append(try VdbWords.split(word).first ?? "")
            }
        }
        self.kind = kind
        self.words = words
        self.values = values
    }

    func int(_ key: String) -> Int? { values[key].flatMap { Int($0) } }

    func double(_ key: String) -> Double? { values[key].flatMap { Double($0) } }

    /// The line's words as written, quotes and escapes kept, so `key="a b"` stays one word.
    private static func rawWords(_ line: String) throws -> [String] {
        var words: [String] = []
        var current = ""
        var quoted = false
        var escaped = false
        for character in line {
            if escaped {
                current.append(character)
                escaped = false
            } else if character == "\\" && quoted {
                current.append(character)
                escaped = true
            } else if character == "\"" {
                current.append(character)
                quoted.toggle()
            } else if character == " " && !quoted {
                if !current.isEmpty { words.append(current) }
                current = ""
            } else {
                current.append(character)
            }
        }
        guard !quoted else {
            throw VdbError.unexpected("\(line.prefix(80)) has a quoted word with no closing quote")
        }
        if !current.isEmpty { words.append(current) }
        return words
    }
}
