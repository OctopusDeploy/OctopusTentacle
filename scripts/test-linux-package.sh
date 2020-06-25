#!/bin/bash
# Test that the tentacle*.deb or tentacle*.rpm package in the working directory installs a Tentacle command that runs successfully.

OSRELID="$(. /etc/os-release && echo $ID)"
if [[ "$OSRELID" == "rhel" && ( -z "$REDHAT_SUBSCRIPTION_USERNAME" || -z "$REDHAT_SUBSCRIPTION_PASSWORD" ) ]]; then
  echo -e 'This script requires the environment variables REDHAT_SUBSCRIPTION_USERNAME and REDHAT_SUBSCRIPTION_PASSWORD to register'\
    '\nthe test system to install packages.' >&2
  exit 1
fi

if [[ ! -e /opt/linux-package-feeds ]]; then
  echo "This script requires tools in '/opt/linux-package-feeds'. If running inside a container, check the volume mounts." >&2
  exit 1
fi


export PKG_PATH_PREFIX="tentacle"
bash /opt/linux-package-feeds/install-linux-package.sh || exit

echo Testing tentacle.
Tentacle version || exit
