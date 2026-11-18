#!/bin/sh
# Checks the Cosmos tools, builds the kernel, and boots it in QEMU.
# Usage: ./run.sh [-h|--headless] [--vdb]
# --vdb forwards vdb's port, 28455, from this computer to VentureOS, so the
# vdb tool can reach it at 127.0.0.1.
# Stops at the first step that fails and exits with its status.

set -e

# Run from the repository root (this script's folder), so it works from anywhere.
cd "$(dirname "$0")"

ARCHITECTURE="x64" # Bit of a workaround for macOS on Apple Silicon
PROJECT="src/Venture.OS.Kernel"

HEADLESS=""
VDB=""
for argument in "$@"; do
    case "$argument" in
        -h|--headless) HEADLESS="--headless" ;;
        --vdb) VDB="--hostfwd tcp::28455-:28455" ;;
        *)
            echo "Usage: $0 [-h|--headless] [--vdb]" >&2
            exit 2
            ;;
    esac
done

cosmos check
cosmos build -a "$ARCHITECTURE" -p "$PROJECT"

if [ -n "$HEADLESS" ]; then
    echo "Running in headless mode..."
fi

# exec hands the exit status of cosmos run back to the caller.
exec cosmos run -a "$ARCHITECTURE" -p "$PROJECT" $HEADLESS $VDB
