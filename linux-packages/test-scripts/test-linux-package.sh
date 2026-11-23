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

# A new certificate must be encrypted with the instance's generated key, not one derived from /etc/machine-id.
INSTANCE_NAME="package-test"
CONFIGURATION_FILE="/etc/octopus/$INSTANCE_NAME/tentacle.config"
MACHINE_KEY_FILE="/var/lib/octopus/tentacle/$INSTANCE_NAME/keys/config-encryption.key"

mkdir -p "$(dirname "$CONFIGURATION_FILE")"
Tentacle create-instance --instance "$INSTANCE_NAME" --config "$CONFIGURATION_FILE"
Tentacle new-certificate --instance "$INSTANCE_NAME"

if ! grep -qF 'key="Tentacle.Certificate">AESGCM-V1$' "$CONFIGURATION_FILE"; then
  echo "The Tentacle certificate in $CONFIGURATION_FILE was not encrypted with the versioned machine key scheme."
  exit 1
fi
echo "The Tentacle certificate is encrypted with the key generated for this machine."

MACHINE_KEY_MODE=$(stat -c %a "$MACHINE_KEY_FILE")
if [[ "$MACHINE_KEY_MODE" != "600" ]]; then
  echo "$MACHINE_KEY_FILE has permissions $MACHINE_KEY_MODE but only its owner should be able to read or write it (600)."
  exit 1
fi
echo "The machine key file can only be read by its owner."

# A numeric uid stands in for the service user, as minimal images may not have useradd.
SERVICE_USER_ID=4321
chown "$SERVICE_USER_ID" "$CONFIGURATION_FILE"
Tentacle upgrade-configuration-encryption --instance "$INSTANCE_NAME"

for KEY_PATH in "$MACHINE_KEY_FILE" "$(dirname "$MACHINE_KEY_FILE")"; do
  KEY_PATH_OWNER=$(stat -c %u "$KEY_PATH")
  if [[ "$KEY_PATH_OWNER" != "$SERVICE_USER_ID" ]]; then
    echo "$KEY_PATH is owned by uid $KEY_PATH_OWNER but the configuration is owned by uid $SERVICE_USER_ID, so the service could not read its key."
    exit 1
  fi
done
echo "The machine key was given to the owner of the configuration."

THUMBPRINT=$(Tentacle show-thumbprint --instance "$INSTANCE_NAME")
if [[ -z "$THUMBPRINT" ]] || ! grep -qF "key=\"Tentacle.CertificateThumbprint\">$THUMBPRINT<" "$CONFIGURATION_FILE"; then
  echo "'Tentacle show-thumbprint' reported '$THUMBPRINT', which is not the thumbprint recorded in $CONFIGURATION_FILE."
  exit 1
fi
echo "The certificate decrypts in a new process and matches the recorded thumbprint."
echo ""

# A non-root user that cannot create its key must be told the exact commands that fix it, and those commands must work.
NON_ROOT_USER="tentacle-test"
NON_ROOT_INSTANCE="non-root-test"
NON_ROOT_CONFIGURATION_FILE="/etc/octopus/$NON_ROOT_INSTANCE/tentacle.config"
# Minimal images may not have useradd.
echo "$NON_ROOT_USER:x:4322:4322::/tmp:/bin/sh" >> /etc/passwd
mkdir -p "$(dirname "$NON_ROOT_CONFIGURATION_FILE")" /etc/octopus/Tentacle/Instances
chown "$NON_ROOT_USER" "$(dirname "$NON_ROOT_CONFIGURATION_FILE")" /etc/octopus/Tentacle/Instances
TENTACLE=$(command -v Tentacle)

# Amazon Linux's minimal image has neither setpriv nor su.
if ! command -v setpriv > /dev/null && ! command -v su > /dev/null; then
  yum --quiet --assumeyes install util-linux
fi

run_as_non_root() {
  if command -v setpriv > /dev/null; then
    HOME=/tmp setpriv --reuid=4322 --regid=4322 --clear-groups "$TENTACLE" "$@"
  else
    su -s /bin/sh "$NON_ROOT_USER" -c "$TENTACLE $*"
  fi
}

if CREATE_OUTPUT=$(run_as_non_root create-instance --instance "$NON_ROOT_INSTANCE" --config "$NON_ROOT_CONFIGURATION_FILE" 2>&1); then
  echo "create-instance succeeded for a user that cannot create the key under /var/lib/octopus/tentacle."
  exit 1
fi
echo "$CREATE_OUTPUT"

FIX_COMMAND=$(echo "$CREATE_OUTPUT" | grep -o 'sudo mkdir[^`]*' | head -n 1)
if [[ -z "$FIX_COMMAND" ]]; then
  echo "The create-instance error did not tell the user which command fixes it."
  exit 1
fi

# This script already runs as root, and minimal images may not have sudo.
sudo() { "$@"; }
eval "$FIX_COMMAND"

run_as_non_root create-instance --instance "$NON_ROOT_INSTANCE" --config "$NON_ROOT_CONFIGURATION_FILE"
echo "Running the command from the error message let the non-root user create its instance."
echo ""

echo "All tests passed."
echo ""
