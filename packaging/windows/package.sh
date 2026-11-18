#!/usr/bin/env bash
# Builds Roundhouse for 64-bit Windows, on any machine with the .NET SDK, since .NET publishes for
# Windows from Linux and macOS too:
#
#   $DIST/roundhouse-<version>-windows-x64.zip  Roundhouse in a folder; unzip it anywhere and run
#                                               Roundhouse.exe
#
# Usage: packaging/windows/package.sh [--framework-dependent]
#
# Needs the .NET 10 SDK, zip, and python3, which checks the executable is for x64.

# shellcheck source=../env.sh
. "$(dirname "${BASH_SOURCE[0]}")/../env.sh"
parse_options "$@"
command -v zip >/dev/null || fail "zip isn't installed."
command -v python3 >/dev/null || fail "python3 isn't installed."
BASENAME="roundhouse-$VERSION-windows-x64"
echo "==> $APP $VERSION for Windows x64 (self-contained: $SELF_CONTAINED)"

STAGE="$WORK/windows/$BASENAME"
publish win-x64 "$SELF_CONTAINED" "$STAGE"
[[ -f "$STAGE/$APP.exe" && -f "$STAGE/libSkiaSharp.dll" ]] || fail "the published Roundhouse has no $APP.exe or libSkiaSharp.dll."
copy_docs "$STAGE"

# Machine 0x8664 in the executable's PE header is x64, so a build for another machine is never
# shipped labelled x64.
machine="$(python3 -c 'import struct, sys; d = open(sys.argv[1], "rb").read(); pe = struct.unpack_from("<I", d, 0x3C)[0]; print(hex(struct.unpack_from("<H", d, pe + 4)[0]))' "$STAGE/$APP.exe")"
[[ "$machine" = 0x8664 ]] || fail "$APP.exe is for machine $machine, not x64 (0x8664)."

OUT="$DIST/$BASENAME.zip"
rm -f "$OUT"
(cd "$WORK/windows" && zip -qr "$OUT" "$BASENAME")
echo "wrote $OUT"
echo "==> done"
ls -lh "$OUT"
