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
apt-get update
apt-get install -y \
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
    xz-utils

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
