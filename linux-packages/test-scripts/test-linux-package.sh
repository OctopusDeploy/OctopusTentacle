#!/bin/bash
set -eux

# Test that the tentacle*.deb or tentacle*.rpm package installs a Tentacle command that runs successfully.

PACKAGE_FILENAME="$1"
SCRIPT_DIR=$(dirname "${BASH_SOURCE[0]}")

stat "$PACKAGE_FILENAME"
. $SCRIPT_DIR/install-package.sh "$PACKAGE_FILENAME"

# Confirm that Tentacle is on the path
# We don't use `which Tentacle` here as, although Tentacle should have been installed, we can't trust that `which` exists.
dotnet --version
Tentacle --version
echo "Tentacle binary lives in the path."
echo ""

# Confirm that the version reported by Tentacle is the expected one
TENTACLE_VERSION=$(Tentacle --version)
echo "Tentacle is reporting version $TENTACLE_VERSION."
if [[ "$TENTACLE_VERSION" != "$BUILD_NUMBER" ]]; then
  echo "Tentacle version was $TENTACLE_VERSION but expected version was $BUILD_NUMBER."
  exit 1
fi
echo "The installed Tentacle version matches the expected one."
echo ""

echo "All tests passed."
echo ""
