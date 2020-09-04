#!/bin/bash
set -eux

if [[ "$ENABLE_DIND" == "Y" ]]; then
    echo "Starting Docker-in-Docker daemon. This requires that this container be run in privileged mode."
    nohup /usr/local/bin/dockerd-entrypoint.sh dockerd &
else
    echo Docker-in-Docker is disabled.
fi

tentacle agent --instance "$OCTOPUS_TENTACLE_INSTANCE_NAME"
