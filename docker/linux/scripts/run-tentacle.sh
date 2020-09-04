#!/bin/bash
set -eux

if [[ "$ENABLE_DIND" -eq "Y" ]]; then
    echo "Starting Docker-in-Docker daemon. This requires that this container be run in privileged mode."
    nohup /usr/local/bin/dockerd-entrypoint.sh dockerd &
else
    echo Docker-in-Docker is disabled.
fi

if [[ "$ACCEPT_EULA" -ne "Y" ]]; then
    echo "ERROR: You must accept the EULA at https://octopus.com/company/legal by passing an environment variable 'ACCEPT_EULA=Y'"
    exit 1
fi

tentacle agent --instance "$OCTOPUS_TENTACLE_INSTANCE_NAME"
