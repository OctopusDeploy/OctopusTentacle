#!/bin/bash
# Test that the tentacle*.deb or tentacle*.rpm package in the working directory installs a Tentacle command that runs successfully.

OSRELID="$(. /etc/os-release && echo $ID)"
if [[ "$OSRELID" == "rhel" && ( -z "$REDHAT_SUBSCRIPTION_USERNAME" || -z "$REDHAT_SUBSCRIPTION_PASSWORD" ) ]]; then
  echo -e 'This script requires the environment variables REDHAT_SUBSCRIPTION_USERNAME and REDHAT_SUBSCRIPTION_PASSWORD to register'\
    '\nthe test system to install packages.' >&2
  exit 1
fi

if [[ ! -e /opt/linux-package-feeds ]]; then
  echo "This script requires 'linux-package-feeds' scripts, installed in '/opt/linux-package-feeds'." >&2
  echo "They come from https://github.com/OctopusDeploy/linux-package-feeds, distributed in TeamCity" >&2
  echo "  via 'Infrastructure / Linux Package Feeds'. If running inside a Docker container, supply them using a volume mount." >&2
  exit 1
fi


# Install the package (with any needed docker config, system registration, dependencies) using a script from 'linux-package-feeds'.
export PKG_PATH_PREFIX="tentacle"
bash /opt/linux-package-feeds/install-linux-package.sh || exit 1

echo Testing tentacle.
Tentacle version || exit 1
echo
