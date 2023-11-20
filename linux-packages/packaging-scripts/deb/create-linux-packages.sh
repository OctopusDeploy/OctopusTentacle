#!/bin/bash
# Package files from INPUT_PATH, with executable permission and a /usr/bin symlink, into a .deb package in OUTPUT_PATH.

# NOTE: This file has been lifted wholesale from
# https://github.com/OctopusDeploy/tool-containers/blob/main/tool-linux-packages/linux/scripts/create-linux-packages.sh
# with almost no modification. It still leeds a lot of re-work.

if [[ -z "$VERSION" ]]; then
  echo 'This script requires the environment variable VERSION - the version being packaged.' >&2
  exit 1
fi
if [[ -z "$INPUT_PATH" ]]; then
  echo 'This script requires the environment variable INPUT_PATH - the path containing binaries and related files to package.' >&2
  exit 1
fi
if [[ -z "$COMMAND_FILE" ]]; then
  echo 'This script requires the environment variable COMMAND_FILE - the path of a command (relative to INPUT_PATH) to symlink in /usr/bin/.' >&2
  exit 1
fi
if [[ -z "$INSTALL_PATH" ]]; then
  echo 'This script requires the environment variable INSTALL_PATH - the path the packages will install to.' >&2
  exit 1
fi
if [[ -z "$OUTPUT_PATH" ]]; then
  echo 'This script requires the environment variable OUTPUT_PATH - the path where packages should be written.' >&2
  exit 1
fi
# Set the array FPM_OPTS to supply additional options to fpm.
# Set the array FPM_DEB_OPTS to supply additional options to fpm when building the .deb package.

which fpm >/dev/null || {
  echo 'This script requires fpm and related tools, and is intended to be run in the container "octopusdeploy/package-linux-docker".' >&2
  exit 1
}
which gpg1 >/dev/null || {
  echo 'This script requires gpg1, and is intended to be run in the container "octopusdeploy/package-linux-docker".' >&2
  exit 1
}
list_descendent_pids() {
  local CHPIDS="$(ps --format pid= --ppid "$1")"
  echo -n "$1,"
  for PID in $CHPIDS; do
    list_descendent_pids "$PID"
  done
}


echo "Preparing files to package."

# Remove existing packages, fpm doesnt like to overwrite
rm -f *.deb || exit

# Remove build files
if [[ -d tmp_usr_bin ]]; then
  rm -rf tmp_usr_bin || exit
fi

# Create executable symlink to include in package
mkdir tmp_usr_bin && ln -s "$INSTALL_PATH/$COMMAND_FILE" tmp_usr_bin/ || exit

# Make sure the command has execute permissions
chmod a+x "$INPUT_PATH/$COMMAND_FILE" || exit

echo "Creating .deb package."
set -ex
fpm --version "$VERSION" \
  --name "$PACKAGE_NAME" \
  --input-type dir \
  --output-type deb \
  --maintainer '<support@octopus.com>' \
  --vendor 'Octopus Deploy' \
  --url 'https://octopus.com/' \
  --description "$PACKAGE_DESC" \
  --deb-no-default-config-files \
  "${FPM_DEB_OPTS[@]}" \
  "${FPM_OPTS[@]}" \
  "$INPUT_PATH/=$INSTALL_PATH/" \
  tmp_usr_bin/=/usr/bin/
set +ex

# Move to output path
mv -f *.deb "$OUTPUT_PATH" || exit
