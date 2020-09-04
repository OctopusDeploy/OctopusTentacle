#!/bin/bash
set -eux

# This script is adapted from https://github.com/docker-library/docker/blob/master/19.03/dind/Dockerfile

apt-get update

# https://github.com/docker/docker/blob/master/project/PACKAGERS.md#runtime-dependencies
apt-get install -y \
    btrfs-progs \
    e2fsprogs \
    iptables \
    openssl \
    uidmap \
    xfsprogs \
    xz-utils \
    pigz \
    dos2unix

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

curl -sSL https://get.docker.com/ | sh

# https://forums.docker.com/t/failing-to-start-dockerd-failed-to-create-nat-chain-docker/78269
update-alternatives --set ip6tables /usr/sbin/ip6tables-legacy
update-alternatives --set iptables /usr/sbin/iptables-legacy

# Remove the apt cache
apt-get clean
rm -rf /var/lib/apt/lists/*
