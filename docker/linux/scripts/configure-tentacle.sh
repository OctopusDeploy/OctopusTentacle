#!/bin/bash
set -eux

# This is very much a work in progress. Please do not view this as production-ready. It's in Octopus-internal use at
# the moment and will be made more robust in due course.
#
# This script only currently supports configuring polling tentacles.

if [[ "$ACCEPT_EULA" != "Y" ]]; then
    echo "ERROR: You must accept the EULA at https://octopus.com/company/legal by passing an environment variable 'ACCEPT_EULA=Y'"
    exit 1
fi

if [ -f "/usr/bin/tentacle" ]; then
    echo "Octopus Tentacle is already configured."
    return
fi

ln -s /opt/octopus/tentacle/Tentacle /usr/bin/tentacle

mkdir -p $OCTOPUS_TENTACLE_CONFIG_DIR
mkdir -p $OCTOPUS_TENTACLE_APPLICATIONS_DIR

# Tentacle Docker images only support once instance per container. Running multiple instances can be achieved by running multiple containers.
instanceName=Tentacle

tentacle create-instance --instance "$instanceName" --config "$OCTOPUS_TENTACLE_CONFIG_DIR/tentacle/$instanceName.config"
tentacle new-certificate --instance "$instanceName" --if-blank
tentacle configure --instance "$instanceName" --app "$OCTOPUS_TENTACLE_APPLICATIONS_DIR" --noListen "True" --reset-trust
tentacle register-worker --instance "$instanceName" --server "$OCTOPUS_SERVER_URL" --name "$HOSTNAME" --comms-style "$OCTOPUS_TENTACLE_COMMS_STYLE" --server-comms-port $OCTOPUS_SERVER_PORT --apiKey $OCTOPUS_SERVER_API_KEY --space "$OCTOPUS_SERVER_SPACE" --workerpool="$OCTOPUS_SERVER_WORKER_POOL" --force
