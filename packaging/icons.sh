#!/usr/bin/env bash
# Makes Roundhouse's icons for Windows and macOS from its one picture, so new art means replacing one file:
#
#   src/Venture.Roundhouse/Assets/Roundhouse.png  the picture, 512x512 or larger, square (the source)
#   src/Venture.Roundhouse/Assets/Roundhouse.ico  Windows's, in Roundhouse.exe (made here)
#   packaging/macos/Roundhouse.icns               macOS's, in Roundhouse.app (made here)
#
# Usage: packaging/icons.sh
#
# Needs ImageMagick 7 (magick) and python3. ImageMagick can't write .icns, so python3 packs the PNGs into one.
# Commit what it makes; the packages and the build use the committed files.

set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SOURCE="$ROOT/src/Venture.Roundhouse/Assets/Roundhouse.png"
command -v magick >/dev/null || { echo "icons: ImageMagick 7 (magick) isn't installed." >&2; exit 1; }
command -v python3 >/dev/null || { echo "icons: python3 isn't installed." >&2; exit 1; }
[[ -f "$SOURCE" ]] || { echo "icons: $SOURCE isn't there." >&2; exit 1; }

read -r width height < <(magick identify -format '%w %h\n' "$SOURCE")
[[ "$width" = "$height" && "$width" -ge 512 ]] || { echo "icons: $SOURCE is ${width}x$height; it needs to be square and at least 512." >&2; exit 1; }

magick "$SOURCE" -define icon:auto-resize=256,128,64,48,32,16 "$ROOT/src/Venture.Roundhouse/Assets/Roundhouse.ico"
echo "wrote src/Venture.Roundhouse/Assets/Roundhouse.ico"

WORKDIR="$(mktemp -d)"
trap 'rm -rf "$WORKDIR"' EXIT
for size in 32 64 128 256 512; do
    magick "$SOURCE" -resize "${size}x$size" -strip "PNG32:$WORKDIR/$size.png"
done

# An .icns is "icns" and its length, then each picture as a four-letter type, its length, and a PNG. These types
# take PNGs at the sizes made above; the @2x ones are for Retina screens.
python3 - "$WORKDIR" "$ROOT/packaging/macos/Roundhouse.icns" <<'PY'
import struct, sys
folder, out = sys.argv[1], sys.argv[2]
entries = [("ic11", 32), ("ic12", 64), ("ic07", 128), ("ic13", 256), ("ic08", 256), ("ic14", 512), ("ic09", 512)]
body = b""
for kind, size in entries:
    data = open(f"{folder}/{size}.png", "rb").read()
    body += kind.encode("ascii") + struct.pack(">I", len(data) + 8) + data
open(out, "wb").write(b"icns" + struct.pack(">I", len(body) + 8) + body)
PY
echo "wrote packaging/macos/Roundhouse.icns"
