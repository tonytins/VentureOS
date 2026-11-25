#!/bin/sh
# Builds vdb in release mode and installs it in ~/.local/bin.
# Usage: ./install.sh [--rebuild]
# The build is incremental, so it's quick when nothing changed and never installs a stale vdb.
# --rebuild cleans the build first (swift package clean), for when an incremental one goes wrong.
# Stops at the first step that fails and exits with its status.

set -e

# Run from vdb's folder (this script's), so it works from anywhere.
cd "$(dirname "$0")"

PROGRAM=vdb
BIN_DIR="$HOME/.local/bin"

REBUILD=""
for argument in "$@"; do
    case "$argument" in
        --rebuild) REBUILD=yes ;;
        -h|--help)
            echo "Usage: $0 [--rebuild]"
            echo "Builds vdb in release mode and installs it in $BIN_DIR."
            echo "  --rebuild  clean the build first, rather than building on what's there"
            exit 0
            ;;
        *)
            echo "Usage: $0 [--rebuild]" >&2
            exit 2
            ;;
    esac
done

if ! command -v swift >/dev/null 2>&1; then
    echo "vdb needs Swift 6 to build, and there's no swift on the PATH." >&2
    exit 1
fi

if [ -n "$REBUILD" ]; then
    echo "Cleaning the build."
    swift package clean
fi

swift build -c release
BUILT="$(swift build -c release --show-bin-path)/$PROGRAM"
if [ ! -x "$BUILT" ]; then
    echo "The build finished, but there's no $BUILT to install." >&2
    exit 1
fi

mkdir -p "$BIN_DIR"
install -m 755 "$BUILT" "$BIN_DIR/$PROGRAM"
echo "Installed $("$BIN_DIR/$PROGRAM" --version) as $BIN_DIR/$PROGRAM."

case ":$PATH:" in
    *":$BIN_DIR:"*) ;;
    *) echo "$BIN_DIR isn't on your PATH; add it to run vdb by name." ;;
esac
