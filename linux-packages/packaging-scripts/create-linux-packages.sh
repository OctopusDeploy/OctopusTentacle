#!/bin/bash
# Package files from INPUT_PATH, with executable permission and a /usr/bin symlink, into .deb and .rpm packages in OUTPUT_PATH.

# NOTE: This file has been lifted wholesale from
# https://github.com/OctopusDeploy/tool-containers/blob/main/tool-linux-packages/linux/scripts/create-linux-packages.sh
# with almost no modification. It still leeds a lot of re-work.

SIGN_TIMEOUT_SECONDS=240 # Signing aborts after this long. Healthy operation takes 15-45 seconds
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
if [[ -z "$SIGN_PRIVATE_KEY" ]]; then
  echo 'This script requires the environment variable SIGN_PRIVATE_KEY - the package signing private key in a format gpg1 can import.' >&2
  echo 'The usual key is in LastPass in the notes of "Linux Packaging GPG private key (Tentacle/OctopusCLI/APT/RPM)".' >&2
  exit 1
fi
if [[ -z "$SIGN_PASSPHRASE" ]]; then
  echo 'This script requires the environment variable SIGN_PASSPHRASE - the package signing private key passphrase.' >&2
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
# Set the array FPM_RPM_OPTS to supply additional options to fpm when building the .rpm package.

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
rm -f *.{deb,rpm} || exit

# Remove build files
if [[ -d tmp_usr_bin ]]; then
  rm -rf tmp_usr_bin || exit
fi

# Create executable symlink to include in package
mkdir tmp_usr_bin && ln -s "$INSTALL_PATH/$COMMAND_FILE" tmp_usr_bin/ || exit

# Make sure the command has execute permissions
chmod a+x "$INPUT_PATH/$COMMAND_FILE" || exit

echo "Configuring RPM signing."
if [[ -e "$HOME/.rpmmacros" ]]; then
  echo 'This script needs to control ~/.rpmmacros, but it already exists. Aborting.' >&2
  echo 'To provide an isolated clean environment, it is recommended to run this inside a fresh docker container.' >&2
  exit 1
fi
GPG_OUT="$(gpg1 --import <(echo "$SIGN_PRIVATE_KEY") 2>&1)" # Checking output instead of exit code because it may already be imported
echo "$GPG_OUT"
GPG_NAME="$(echo "$GPG_OUT" | grep --perl --only-matching --no-messages --max-count=1 '^gpg: key \K\w+')" \
  || { echo 'Unable to identify imported private key.' >&2; exit 1; }
PASSPHRASE_SHM="$(mktemp /dev/shm/clp.XXXXXXXX)" # Use private file in shared memory to avoid writing to disk
echo "$SIGN_PASSPHRASE" >> "$PASSPHRASE_SHM"
echo "%_signature gpg
%_gpg_path /root/.gnupg
%_gpg_name $GPG_NAME
%__gpg $(which gpg1)
%_gpg_sign_cmd_extra_args --batch --passphrase-file '$PASSPHRASE_SHM'
" > "$HOME/.rpmmacros"

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

# Prevent gpg1 and rpmsign from hanging forever after a bad passphrase
{
  sleep "$SIGN_TIMEOUT_SECONDS"
  echo "Checking for hanging signing processes." >&2
  pgrep --parent "$(list_descendent_pids $$)" --list-full '^(gpg1|rpmsign)$' && {
    echo "Detected hanging signing processes. This is usually caused by an incorrect SIGN_PASSPHRASE. Sending SIGINT to get unstuck." >&2
    pkill --parent "$(list_descendent_pids $$)" -INT '^(gpg1|rpmsign)$'
  }
} &
ANTIHANG_PID=$!

echo "Creating .rpm package."
set -ex
export EDITOR="sed -i -e '/Requires: placeholder/c%if 0%{?suse_version} >= 15000\nRequires: openssl-3\n%else\nRequires: openssl-libs\n%endif'"
fpm --version "$VERSION" \
  --name "$PACKAGE_NAME" \
  --input-type dir \
  --output-type rpm \
  --maintainer '<support@octopus.com>' \
  --vendor 'Octopus Deploy' \
  --url 'https://octopus.com/' \
  --description "$PACKAGE_DESC" \
  --verbose \
  --rpm-rpmbuild-define "_build_id_links none" \
  "${FPM_RPM_OPTS[@]}" \
  "${FPM_OPTS[@]}" \
  "$INPUT_PATH/=$INSTALL_PATH/" \
  tmp_usr_bin/=/usr/bin/
set +ex

echo "Signing .rpm package."
set -ex
rpmsign --addsign *.rpm
set +ex

  # "$INPUT_PATH/=$INSTALL_PATH/" \

kill "$ANTIHANG_PID" 2>/dev/null && wait "$ANTIHANG_PID" 2>/dev/null

# Remove build files
rm -f "$PASSPHRASE_SHM" "$HOME/.rpmmacros" || exit
if [[ -d tmp_usr_bin ]]; then
  rm -rf tmp_usr_bin || exit
fi

# Move to output path
mv -f *.{deb,rpm} "$OUTPUT_PATH" || exit
