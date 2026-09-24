#!/bin/bash
set -eux

# This script is adapted from https://github.com/docker-library/docker/blob/master/19.03/dind/Dockerfile

# Add the apt sources for Docker (they're not part of the stock Ubuntu distro).
apt-get update
apt-get install -y --no-install-recommends \
    ca-certificates \
    curl

# https://docs.docker.com/engine/install/ubuntu/
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc

cat > /etc/apt/sources.list.d/docker.sources <<EOF
Types: deb
URIs: https://download.docker.com/linux/ubuntu
Suites: $(. /etc/os-release && echo "${UBUNTU_CODENAME:-$VERSION_CODENAME}")
Components: stable
Architectures: $(dpkg --print-architecture)
Signed-By: /etc/apt/keyrings/docker.asc
EOF

# Install Docker and its runtime dependencies.
# https://github.com/docker/docker/blob/master/project/PACKAGERS.md#runtime-dependencies
#
# Everything after xz-utils used to arrive only as a Recommends, and is explicit now that
# --no-install-recommends drops them (LEV-1843). They are the image's published tool surface:
# customer deployment scripts call them, and nothing fails at build time if one goes missing.
#   docker-buildx-plugin, docker-compose-plugin - docker-ce-cli Recommends; without them
#       'docker buildx' / 'docker compose' fail with 'unknown command'.
#   docker-ce-rootless-extras, git, apparmor    - docker-ce Recommends. apparmor gives nested
#       containers the docker-default profile; keeping it avoids a silent confinement downgrade.
#   openssh-client, less, patch                 - git Recommends; ssh/scp and patch are
#       commonly called directly by deployment scripts.
# The Recommends deliberately left out are daemons and desktop leftovers with no job in a
# container: systemd-timesyncd, systemd-resolved, networkd-dispatcher, xauth, dmsetup and
# their GLib/X11/Python libraries. systemd itself stays: it is a hard Depends via
# docker-ce-rootless-extras -> dbus-user-session -> libpam-systemd.
apt-get update
apt-get install -y --no-install-recommends \
    btrfs-progs \
    containerd.io \
    docker-ce \
    docker-ce-cli \
    dos2unix \
    e2fsprogs \
    iproute2 \
    iptables \
    jq \
    openssl \
    pigz \
    sudo \
    uidmap \
    xfsprogs \
    xz-utils \
    docker-buildx-plugin \
    docker-compose-plugin \
    docker-ce-rootless-extras \
    git \
    apparmor \
    openssh-client \
    less \
    patch

# set up subuid/subgid so that "--userns-remap=default" works out-of-the-box
addgroup --system dockremap
adduser --system --group dockremap
echo 'dockremap:165536:65536' >> /etc/subuid
echo 'dockremap:165536:65536' >> /etc/subgid

# https://github.com/docker/docker/tree/master/hack/dind
export DIND_COMMIT=37498f009d8bf25fbb6199e8ccd34bed84f2874b

curl -o /usr/local/bin/dind "https://raw.githubusercontent.com/docker/docker/${DIND_COMMIT}/hack/dind"
chmod +x /usr/local/bin/dind
dos2unix /usr/local/bin/dind

chmod +x /usr/local/bin/dockerd-entrypoint.sh
dos2unix /usr/local/bin/dockerd-entrypoint.sh

update-alternatives --set ip6tables /usr/sbin/ip6tables-nft
update-alternatives --set iptables /usr/sbin/iptables-nft

# Remove the apt cache
apt-get clean
rm -rf /var/lib/apt/lists/*
