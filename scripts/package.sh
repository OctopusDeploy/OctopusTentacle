#!/bin/bash
# Package tentacle from BINARIES_PATH, with executable permission into .deb and .rpm packages (with a /usr/bin symlink)
# and a .tar.gz archive, placed in PACKAGES_PATH.

if [[ -z "$VERSION" ]]; then
  echo 'This script requires the environment variable VERSION - the version being packaged.' >&2
  exit 1
fi
if [[ -z "$BINARIES_PATH" ]]; then
  echo 'This script requires the environment variable BINARIES_PATH - the path containing binaries and related files to package.' >&2
  exit 1
fi
if [[ -z "$PACKAGES_PATH" ]]; then
  echo 'This script requires the environment variable PACKAGES_PATH - the path where packages should be written.' >&2
  exit 1
fi

which fpm >/dev/null || {
  echo 'This script requires fpm and related tools, and is intended to be run in the container "octopusdeploy/tool-linux-packages".' >&2
  exit 1
}

SCRIPT_DIR="$(dirname "${BASH_SOURCE[0]}")"


if [[ ! -d "$PACKAGES_PATH" ]]; then
  mkdir -p "$PACKAGES_PATH" || exit
fi

architecture=$1

if [ $architecture == "linux-arm64" ] ; then
  PACKAGE_ARCHITECTURE="arm64";
elif [ $architecture == "linux-x64" ] ; then
  PACKAGE_ARCHITECTURE="x86_64";
fi

# Create .deb and .rpm packages, with executable permission and a /usr/bin symlink, using a script from container 'octopusdeploy/tool-linux-packages'.
COMMAND_FILE=Tentacle
INSTALL_PATH=/opt/octopus/tentacle
PACKAGE_NAME="tentacle"
PACKAGE_DESC='Octopus Tentacle package'
FPM_OPTS=(
  --after-install "$SCRIPT_DIR/setup.sh"
  --before-remove "$SCRIPT_DIR/uninstall.sh"
  --architecture "$PACKAGE_ARCHITECTURE"
)
FPM_DEB_OPTS=(
  --depends 'libssl1.0.0 | libssl1.0.2 | libssl1.1'
)
FPM_RPM_OPTS=(
  --depends 'openssl-libs'
)

source create-linux-packages.sh || exit

# the archives have a different naming format for the architecture
archivename="tentacle-$VERSION-${architecture/-/_}.tar.gz"

# Create .tar.gz archive
rm -rf tentacle || exit
mkdir tentacle || exit
cp -a "$BINARIES_PATH/." tentacle/ || exit
tar czvf "$archivename" tentacle || exit
mv -f "$archivename" "$PACKAGES_PATH" || exit
