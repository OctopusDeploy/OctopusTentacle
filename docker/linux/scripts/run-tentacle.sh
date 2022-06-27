#!/bin/bash
set -eux

if [[ "$DISABLE_DIND" == "Y" ]]; then
    echo Docker-in-Docker is disabled.
else
    echo "Starting Docker-in-Docker daemon. This requires that this container be run in privileged mode."
    nohup /usr/local/bin/dockerd-entrypoint.sh dockerd &
fi

tentacle agent --instance Tentacle --noninteractive
