/// Writes PNGs of RGB pixels with no compression: deflate's stored blocks, so vdb needs no image or zlib library on
/// any platform. A 1024 by 768 screen is about 2.3 MB.
enum PNG {
    /// The largest a stored deflate block holds.
    private static let maximumBlock = 65_535

    static func encode(width: Int, height: Int, rgb: [UInt8]) -> [UInt8] {
        precondition(width > 0 && height > 0 && rgb.count == width * height * 3, "the pixels aren't width × height × 3 bytes")

        // Each row starts with its filter, 0 for none.
        let stride = width * 3
        var raw: [UInt8] = []
        raw.reserveCapacity((stride + 1) * height)
        for row in 0..<height {
            raw.append(0)
            raw.append(contentsOf: rgb[(row * stride)..<((row + 1) * stride)])
        }

        var header: [UInt8] = []
        header += bigEndian(UInt32(width))
        header += bigEndian(UInt32(height))
        header += [8, 2, 0, 0, 0]  // 8 bits a channel, RGB, deflate, no filter method, not interlaced.

        var png: [UInt8] = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]
        png += chunk("IHDR", header)
        png += chunk("IDAT", zlib(raw))
        png += chunk("IEND", [])
        return png
    }

    /// A zlib stream of stored blocks.
    static func zlib(_ data: [UInt8]) -> [UInt8] {
        var stream: [UInt8] = [0x78, 0x01]
        var start = 0
        repeat {
            let count = min(maximumBlock, data.count - start)
            let isLast = start + count == data.count
            stream.append(isLast ? 1 : 0)
            stream += [UInt8(count & 0xFF), UInt8(count >> 8), UInt8(~count & 0xFF), UInt8((~count >> 8) & 0xFF)]
            stream += data[start..<(start + count)]
            start += count
        } while start < data.count
        return stream + bigEndian(adler32(data))
    }

    static func chunk(_ type: String, _ data: [UInt8]) -> [UInt8] {
        let typed = Array(type.utf8) + data
        return bigEndian(UInt32(data.count)) + typed + bigEndian(crc32(typed))
    }

    static func crc32(_ data: [UInt8]) -> UInt32 {
        var crc: UInt32 = 0xFFFF_FFFF
        for byte in data {
            crc = crcTable[Int((crc ^ UInt32(byte)) & 0xFF)] ^ (crc >> 8)
        }
        return ~crc
    }

    // CRC-32's remainders for every byte, as the PNG specification's sample code makes them.
    private static let crcTable: [UInt32] = (0..<256).map { index in
        var remainder = UInt32(index)
        for _ in 0..<8 {
            remainder = remainder & 1 == 1 ? (remainder >> 1) ^ 0xEDB8_8320 : remainder >> 1
        }
        return remainder
    }

    static func adler32(_ data: [UInt8]) -> UInt32 {
        var (a, b): (UInt32, UInt32) = (1, 0)
        for byte in data {
            a = (a + UInt32(byte)) % 65_521
            b = (b + a) % 65_521
        }
        return (b << 16) | a
    }

    private static func bigEndian(_ value: UInt32) -> [UInt8] {
        [UInt8(value >> 24), UInt8((value >> 16) & 0xFF), UInt8((value >> 8) & 0xFF), UInt8(value & 0xFF)]
    }
}
