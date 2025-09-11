#!/bin/bash
set -eu

# Architecture detection and bootstrap runner selection script
# This script automatically detects the runtime architecture and selects 
# the appropriate bootstrapRunner binary for execution.

# Detect the current architecture
ARCH=$(uname -m)
OS=$(uname -s | tr '[:upper:]' '[:lower:]')

# Map architecture names to Go architecture naming
case "$ARCH" in
    x86_64)
        GO_ARCH="amd64"
        ;;
    aarch64|arm64)
        GO_ARCH="arm64"
        ;;
    i386|i686)
        GO_ARCH="386"
        ;;
    armv7l|armv6l)
        GO_ARCH="arm"
        ;;
    *)
        echo "Error: Unsupported architecture: $ARCH" >&2
        echo "Supported architectures: x86_64, aarch64, arm64, i386, i686, armv7l, armv6l" >&2
        exit 1
        ;;
esac

# Construct binary name
BINARY_NAME="bootstrapRunner-${OS}-${GO_ARCH}"
BINARY_PATH="$(dirname "$0")/$BINARY_NAME"

# Check if the binary exists
if [ ! -f "$BINARY_PATH" ]; then
    echo "Error: Bootstrap runner binary not found for architecture ${OS}-${GO_ARCH}" >&2
    echo "Looking for: $BINARY_PATH" >&2
    echo "Available binaries:" >&2
    ls -1 "$(dirname "$0")"/bootstrapRunner-* 2>/dev/null || echo "  No bootstrap binaries found" >&2
    exit 1
fi

# Make sure the binary is executable
chmod +x "$BINARY_PATH"

# Execute the appropriate binary with all arguments passed through
exec "$BINARY_PATH" "$@"