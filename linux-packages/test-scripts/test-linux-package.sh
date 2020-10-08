#!/bin/bash
set -eux

# Test that the tentacle*.deb or tentacle*.rpm package installs a Tentacle command that runs successfully.

PACKAGE_FILENAME="$1"

# Install Tentacle
set +e
if apt-get --version 2> /dev/null; then
  set -e
  apt-get update
  apt install -y --no-install-recommends "$PACKAGE_FILENAME"
elif yum --version 2> /dev/null; then
  set -e
  yum --quiet --assumeyes localinstall "$PACKAGE_FILENAME"
else
  set -e
  echo "No supported package management tools found."
  exit 1
fi
echo "Tentacle package was successfully installed."
echo ""

set +x

# Confirm that Tentacle is on the path
# We don't use `which Tentacle` here as, although Tentacle should have been installed, we can't trust that `which` exists.
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
