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

# TODO This check doesn't account for multiple instances, or for reconfiguring an existing instance.
if [ -f "/usr/bin/tentacle" ]; then
    echo "Octopus Tentacle is already configured."
    return
fi

ln -s /opt/octopus/tentacle/Tentacle /usr/bin/tentacle

mkdir -p $OCTOPUS_TENTACLE_CONFIG_DIR
mkdir -p $OCTOPUS_TENTACLE_APPLICATIONS_DIR

tentacle create-instance --instance "$OCTOPUS_TENTACLE_INSTANCE_NAME" --config "$OCTOPUS_TENTACLE_CONFIG_DIR/tentacle/$OCTOPUS_TENTACLE_INSTANCE_NAME.config"
tentacle new-certificate --instance "$OCTOPUS_TENTACLE_INSTANCE_NAME" --if-blank
tentacle configure --instance "$OCTOPUS_TENTACLE_INSTANCE_NAME" --app "$OCTOPUS_TENTACLE_APPLICATIONS_DIR" --noListen "True" --reset-trust
tentacle register-worker --instance "$OCTOPUS_TENTACLE_INSTANCE_NAME" --server "$OCTOPUS_SERVER_URL" --name "$HOSTNAME" --comms-style "$OCTOPUS_TENTACLE_COMMS_STYLE" --server-comms-port $OCTOPUS_SERVER_PORT --apiKey $OCTOPUS_SERVER_API_KEY --space "$OCTOPUS_SERVER_SPACE" --workerpool="$OCTOPUS_SERVER_WORKER_POOL" --force
