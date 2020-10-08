#!/bin/bash
set -eux

# Package tentacle from INPUT_PATH, with executable permission into .deb and .rpm packages (with a /usr/bin symlink)
# and a .tar.gz archive, placed in OUTPUT_PATH.

echo Package versions will be $VERSION
echo Will pack content from $INPUT_PATH
echo Will write artifacts to $OUTPUT_PATH

# Ensure that all the scripts in the input path have the correct attributes
find $INPUT_PATH -name "*.sh" -type f -exec chmod +x {} \;

# Ensure that the output path exists
mkdir -p "$OUTPUT_PATH"

set +ex

# Prepare arguments to invoke the package-creation script. The script is from our tool container
# and actually creates the packages for us.

architecture=$1
if [ $architecture == "linux-arm64" ] ; then
  PACKAGE_ARCHITECTURE="arm64";
elif [ $architecture == "linux-x64" ] ; then
  PACKAGE_ARCHITECTURE="x86_64";
elif [ $architecture == "linux-musl-x64" ] ; then
  PACKAGE_ARCHITECTURE="x86_64";
fi

# Trailing slashes here are *important* because the script we're calling is very, very particular.
BINARIES_PATH="$INPUT_PATH/"
PACKAGES_PATH="$OUTPUT_PATH/"
COMMAND_FILE=Tentacle
INSTALL_PATH=/opt/octopus
PACKAGE_NAME="tentacle"
PACKAGE_DESC='Octopus Tentacle package'
FPM_OPTS=(
  --after-install "$INPUT_PATH/tentacle/after-install.sh"
  --before-remove "$INPUT_PATH/tentacle/before-uninstall.sh"
  --architecture "$PACKAGE_ARCHITECTURE"
)
FPM_DEB_OPTS=(
  --depends 'libssl1.0.0 | libssl1.0.2 | libssl1.1'
)
FPM_RPM_OPTS=(
  --depends 'openssl-libs'
)

# The script has interstitial errors so we can't use set -e here.
source create-linux-packages.sh || exit 1
