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

# Confirm that a freshly configured Tentacle protects its certificate with the key it generates for this machine,
# and not with one derived from /etc/machine-id, which every container started from the same image shares and
# which is world-readable on any host. See LEV-1171.
#
# Values written by the current scheme carry a version prefix; anything without it was written by the machine-id
# scheme. The text is LinuxMachineKeyEncryptor.ProtectedValuePrefix and must never change, so it is spelled out
# here rather than read from anywhere.
#
# grep -F throughout: the prefix contains '$', which a regular expression would treat as an anchor.
INSTANCE_NAME="package-test"
CONFIGURATION_FILE="/etc/octopus/$INSTANCE_NAME/tentacle.config"
MACHINE_KEY_FILE="/etc/octopus/machinekey"

mkdir -p "$(dirname "$CONFIGURATION_FILE")"
Tentacle create-instance --instance "$INSTANCE_NAME" --config "$CONFIGURATION_FILE"
Tentacle new-certificate --instance "$INSTANCE_NAME"

if ! grep -qF 'key="Tentacle.Certificate">$OctopusMachineKeyV1$' "$CONFIGURATION_FILE"; then
  echo "The Tentacle certificate in $CONFIGURATION_FILE was not encrypted with the versioned machine key scheme."
  exit 1
fi
echo "The Tentacle certificate is encrypted with the key generated for this machine."

if [[ ! -f "$MACHINE_KEY_FILE" ]]; then
  echo "Expected the generated machine key at $MACHINE_KEY_FILE."
  exit 1
fi

MACHINE_KEY_MODE=$(stat -c %a "$MACHINE_KEY_FILE")
if [[ "$MACHINE_KEY_MODE" != "600" ]]; then
  echo "$MACHINE_KEY_FILE has permissions $MACHINE_KEY_MODE but only its owner should be able to read or write it (600)."
  exit 1
fi
echo "The machine key file can only be read by its owner."

# A second process reads the certificate back with the same key, and it is the certificate that was written.
THUMBPRINT=$(Tentacle show-thumbprint --instance "$INSTANCE_NAME")
if [[ -z "$THUMBPRINT" ]] || ! grep -qF "key=\"Tentacle.CertificateThumbprint\">$THUMBPRINT<" "$CONFIGURATION_FILE"; then
  echo "'Tentacle show-thumbprint' reported '$THUMBPRINT', which is not the thumbprint recorded in $CONFIGURATION_FILE."
  exit 1
fi
echo "The certificate decrypts in a new process and matches the recorded thumbprint."
echo ""

echo "All tests passed."
echo ""
