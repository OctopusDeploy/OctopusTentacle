#!/bin/bash
# Test that octopuscli and tentacle can be installed from our APT and RPM feeds, and can run the `version` command.

if [[ -z "$PUBLISH_LINUX_EXTERNAL" ]]; then
  echo 'This script requires the environment variable PUBLISH_LINUX_EXTERNAL - specify "true" to test the external public feed.' >&2
  exit 1
fi
OSRELID="$(. /etc/os-release && echo $ID)"
if [[ "$OSRELID" == "rhel" && ( -z "$REDHAT_SUBSCRIPTION_USERNAME" || -z "$REDHAT_SUBSCRIPTION_PASSWORD" ) ]]; then
  echo -e 'This script requires the environment variables REDHAT_SUBSCRIPTION_USERNAME and REDHAT_SUBSCRIPTION_PASSWORD to register'\
    '\nthe test system to install packages.' >&2
  exit 1
fi

# Install the packages from our package feed (with any needed docker config, system registration) using a script from 'linux-package-feeds'.
export PKG_NAMES="octopuscli tentacle"
bash install-linux-feed-package.sh || exit

echo Testing tentacle.
Tentacle version || exit
echo

echo Testing octopuscli.
octo version || exit
