#!/bin/bash
set -eux

# This script is adapted from https://github.com/docker-library/docker/blob/master/19.03/dind/Dockerfile


# Add the apt sources for Docker (they're not part of the stock Ubuntu distro).
apt-get update

apt-get install -y --no-install-recommends \
    apt-transport-https \
    ca-certificates \
    curl \
    gnupg \
    lsb-release

curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /usr/share/keyrings/docker-archive-keyring.gpg

echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/docker-archive-keyring.gpg] https://download.docker.com/linux/ubuntu \
  $(lsb_release -cs) stable" | tee /etc/apt/sources.list.d/docker.list > /dev/null


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

# Newer operating systems (RHEL in particular) have moved from iptables to
# nftables, and dockerd fails to create its NAT chain against the wrong
# backend. See
# https://octopusdeploy.slack.com/archives/CNHBHV2BX/p1677028476905509 and
# https://forums.docker.com/t/failing-to-start-dockerd-failed-to-create-nat-chain-docker/78269
#
# This selection used to be wrapped in `if [ iptables -nL > /dev/null 2>&1 ]`,
# intended as "use legacy if iptables works, else nft". That condition was
# never true: `[` is the `test` builtin rather than a subshell, so `iptables`
# never ran and test failed with "unary operator expected" (exit 2). nft has
# therefore always been selected, and the legacy branch was dead code, so
# removing the conditional and keeping nft is a no-op at runtime.
#
# Selecting legacy instead would be a genuine behavioural change for
# docker-in-docker and needs its own testing - deliberately not done here.
update-alternatives --set ip6tables /usr/sbin/ip6tables-nft
update-alternatives --set iptables /usr/sbin/iptables-nft

# Remove the apt cache
apt-get clean
rm -rf /var/lib/apt/lists/*
