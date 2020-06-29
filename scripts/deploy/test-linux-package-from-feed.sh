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

if [[ ! -e /opt/linux-package-feeds ]]; then
  echo "This script requires 'linux-package-feeds' scripts, installed in '/opt/linux-package-feeds'." >&2
  echo "They come from https://github.com/OctopusDeploy/linux-package-feeds/tree/master/source, distributed in TeamCity" >&2
  echo "  via 'Infrastructure / Linux Package Feeds'. If running inside a Docker container, supply them using a volume mount." >&2
  exit 1
fi


export PKG_NAMES="octopuscli tentacle"
bash /opt/linux-package-feeds/install-linux-feed-package.sh || exit

echo Testing tentacle.
Tentacle version || exit

echo Testing octopuscli.
octo version || exit
echo
