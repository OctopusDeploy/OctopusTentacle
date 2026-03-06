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

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"

# Execute the appropriate binary with all arguments passed through
exec "$SCRIPT_DIR/$BINARY_NAME" "$@"