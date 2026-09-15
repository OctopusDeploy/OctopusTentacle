#!/bin/bash
set -eux

# Test that the tentacle*.deb or tentacle*.rpm package installs a Tentacle command that runs successfully.

PACKAGE_FILENAME="$1"
SCRIPT_DIR=$(dirname "${BASH_SOURCE[0]}")

stat "$PACKAGE_FILENAME"
. $SCRIPT_DIR/install-package.sh "$PACKAGE_FILENAME"

# Confirm that Tentacle is on the path
# We don't use `which Tentacle` here as, although Tentacle should have been installed, we can't trust that `which` exists.
Tentacle --version
echo "Tentacle binary lives in the path."
echo ""

# Confirm that the version reported by Tentacle is the expected one.
#
# Neither side can be compared raw, because the two are stamped from different
# OctoVersion fields:
#
#   a. Tentacle prints its AssemblyInformationalVersion (see VersionCommand.cs),
#      which build/Build.cs stamps from OctoVersionInfo.InformationalVersion,
#      i.e. '<FullSemVer>+Branch.<branch>.Sha.<sha>'. BUILD_NUMBER is the bare
#      FullSemVer, so the '+...' build metadata has to come off the reported
#      version before the two can match. This applies to CI builds too, not
#      just local ones.
#
#   b. A local NUKE build additionally appends '-<yyyyMMddHHmmss>' to FullSemVer
#      (build/Build.cs), and that timestamp is never stamped into the binary. On
#      TeamCity there is no such suffix and stripping it is a no-op, so this
#      makes the target runnable locally without weakening what CI asserts.
#
# testing/docker-linux/build-and-test-linux-docker-image.sh normalises the same
# two things for the same reasons - keep the two in step.
#
# The timestamp is stripped with a bash regex rather than sed, for the same
# reason `which` is avoided above: this runs on distributions as old as Ubuntu
# 16.04, and the fewer tools it assumes, the better.
TENTACLE_VERSION=$(Tentacle --version)
echo "Tentacle is reporting version $TENTACLE_VERSION."

REPORTED_VERSION="${TENTACLE_VERSION%%+*}"

if [[ "$BUILD_NUMBER" =~ ^(.*)-[0-9]{14}$ ]]; then
  EXPECTED_VERSION="${BASH_REMATCH[1]}"
else
  EXPECTED_VERSION="$BUILD_NUMBER"
fi

if [[ "$REPORTED_VERSION" != "$EXPECTED_VERSION" ]]; then
  echo "Tentacle version was $REPORTED_VERSION but expected version was $EXPECTED_VERSION."
  exit 1
fi
echo "The installed Tentacle version matches the expected one."
echo ""

echo "All tests passed."
echo ""
