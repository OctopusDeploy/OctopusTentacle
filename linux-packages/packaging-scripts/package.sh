#!/bin/bash
set -eux

# Get the directory of the cucrrently-executing script so that we can call its siblings later.
# fpm appears to be sensitive to working directories so we don't want to just cd to anywhere.
SCRIPT_DIR=$(dirname "${BASH_SOURCE[0]}")

# Package tentacle from INPUT_PATH, with executable permission into .deb and .rpm packages (with a /usr/bin symlink)
# and a .tar.gz archive, placed in OUTPUT_PATH.

echo Package versions will be $VERSION
echo Will pack content from $INPUT_PATH
echo Will write artifacts to $OUTPUT_PATH

find $INPUT_PATH

# Ensure that all the scripts in the input path have the correct attributes
find $INPUT_PATH -name "*.sh" -type f -exec chmod +x {} \;

# Ensure that the output path exists
mkdir -p "$OUTPUT_PATH"

set +ex

# Prepare arguments to invoke the package-creation script. The script is from our tool container
# and actually creates the packages for us.

architecture=$1
if [ $architecture == "linux-arm64" ] ; then
  DEB_PACKAGE_ARCHITECTURE="arm64";
  RPM_PACKAGE_ARCHITECTURE="aarch64";
elif [ $architecture == "linux-arm" ] ; then
  DEB_PACKAGE_ARCHITECTURE="armhf"
  RPM_PACKAGE_ARCHITECTURE="armv7hl"
elif [ $architecture == "linux-x64" ] ; then
  DEB_PACKAGE_ARCHITECTURE="x86_64";
  RPM_PACKAGE_ARCHITECTURE="x86_64";
elif [ $architecture == "linux-musl-x64" ] ; then
  DEB_PACKAGE_ARCHITECTURE="x86_64";
  RPM_PACKAGE_ARCHITECTURE="x86_64";
fi
COMMAND_FILE=Tentacle
INSTALL_PATH=/opt/octopus/tentacle
PACKAGE_NAME="tentacle"
PACKAGE_DESC='Octopus Tentacle package'
FPM_OPTS=(
  --after-install "$INPUT_PATH/after-install.sh"
  --before-remove "$INPUT_PATH/before-uninstall.sh"
)
FPM_DEB_OPTS=(
  --depends 'liblttng-ust0 | liblttng-ust1'
  --depends 'libssl1.0.0 | libssl1.0.2 | libssl1.1 | libssl3'
  --depends 'libicu52 | libicu55 | libicu57 | libicu60 | libicu63 | libicu66 | libicu67 | libicu70'
  --architecture "$DEB_PACKAGE_ARCHITECTURE"
)
FPM_RPM_OPTS=(
  --depends 'openssl-libs'
  --architecture "$RPM_PACKAGE_ARCHITECTURE"
)

# The script has interstitial errors so we can't use set -e here.
source $SCRIPT_DIR/create-linux-packages.sh || exit 1
