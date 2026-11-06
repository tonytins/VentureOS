#!/bin/sh
# Checks the Cosmos tools, builds the kernel, and boots it in QEMU.
# Usage: ./run.sh [-h|--headless]
# Stops at the first step that fails and exits with its status.

set -e

# Run from the repository root (this script's folder), so it works from anywhere.
cd "$(dirname "$0")"

ARCHITECTURE="x64" # Bit of a workaround for macOS on Apple Silicon
PROJECT="src/Venture.OS"

case "${1:-}" in
    "") HEADLESS="" ;;
    -h|--headless) HEADLESS="--headless" ;;
    *)
        echo "Usage: $0 [-h|--headless]" >&2
        exit 2
        ;;
esac

cosmos check
cosmos build -a "$ARCHITECTURE" -p "$PROJECT"

if [ -n "$HEADLESS" ]; then
    echo "Running in headless mode..."
fi

# exec hands the exit status of cosmos run back to the caller.
exec cosmos run -a "$ARCHITECTURE" -p "$PROJECT" $HEADLESS
