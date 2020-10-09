#!/bin/bash
set -eux

PACKAGE_FILENAME="$1"

# We don't use `which Tentacle` here as, although Tentacle should have been installed, we can't trust that `which` exists.
set +e
if apt-get --version 2> /dev/null; then
  set -e

  # Old versions of apt can't cope with absolute paths, so we use dpkg.
  # dpkg doesn't resolve dependencies.
  # We run dpkg to install the package, then run apt-get to restore missing dependencies.
  export DEBIAN_FRONTEND=noninteractive
  apt-get update
  dpkg --install --force-all $PACKAGE_FILENAME
  apt-get --no-install-recommends --yes --fix-broken install

elif yum --version 2> /dev/null; then
  set -e
  yum --quiet --assumeyes localinstall *
else
  set -e
  echo "No supported package management tools found."
  exit 1
fi
