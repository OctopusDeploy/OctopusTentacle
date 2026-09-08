#!/bin/bash
set -eux

# This script is adapted from https://github.com/docker-library/docker/blob/master/19.03/dind/Dockerfile


# Add the apt sources for Docker (they're not part of the stock Ubuntu distro).
apt-get update

# Only ca-certificates and curl are needed to add the repository. Three
# packages that used to be installed here are not:
#   apt-transport-https - a transitional stub on jammy ("transitional package
#                         for https support"); apt 2.4 ships
#                         /usr/lib/apt/methods/https itself.
#   gnupg               - was only needed for `gpg --dearmor`. apt reads the
#                         ASCII-armoured key directly via Signed-By and verifies
#                         it with gpgv, which apt itself depends on.
#   lsb-release         - replaced by /etc/os-release, which is always present.
apt-get install -y --no-install-recommends \
    ca-certificates \
    curl

# This follows Docker's documented setup for Ubuntu: the ASCII-armoured key
# under /etc/apt/keyrings, referenced from a deb822 .sources file rather than a
# one-line .list. deb822 is supported by apt 2.4 on jammy.
# https://docs.docker.com/engine/install/ubuntu/
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc

# UBUNTU_CODENAME first so this still resolves on Ubuntu derivatives, which set
# VERSION_CODENAME to their own release name. Deliberately unguarded: with
# `set -u` an unset codename fails the build rather than writing empty Suites.
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
# iproute2 is here for `ip`, which dockerd-entrypoint.sh uses in _tls_san to
# collect the container's addresses. dockerd itself does not need it, but
# without it a dind TLS certificate is issued with DNS SANs only - verified as
# DNS:docker,DNS:<hostname>,DNS:localhost and no IP: entries - so a client
# connecting to the daemon by IP address fails hostname verification. Upstream
# docker:dind ships iproute2 for the same reason.

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
