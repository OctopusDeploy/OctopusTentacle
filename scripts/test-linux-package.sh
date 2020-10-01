#!/bin/bash
# Test that the tentacle*.deb or tentacle*.rpm package in the working directory installs a Tentacle command that runs successfully.

OSRELID="$(. /etc/os-release && echo $ID)"
if [[ "$OSRELID" == "rhel" && ( -z "$REDHAT_SUBSCRIPTION_USERNAME" || -z "$REDHAT_SUBSCRIPTION_PASSWORD" ) ]]; then
  echo -e 'This script requires the environment variables REDHAT_SUBSCRIPTION_USERNAME and REDHAT_SUBSCRIPTION_PASSWORD to register'\
    '\nthe test system to install packages.' >&2
  exit 1
fi

# Install the package (with any needed docker config, system registration, dependencies) using a script from container 'octopusdeploy/tool-linux-packages'.
export PKG_PATH_PREFIX="tentacle"
bash install-linux-package.sh || exit 1

echo Testing tentacle.
TENTACLE_VERSION=$(Tentacle version)
if [[ "$TENTACLE_VERSION" != "$BUILD_NUMBER" ]]; then
  echo "Tentacle version was $TENTACLE_VERSION but expected version was $BUILD_NUMBER."
  exit 1
fi

echo
