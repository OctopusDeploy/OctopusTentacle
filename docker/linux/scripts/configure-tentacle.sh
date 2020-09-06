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

mkdir -p $configurationDirectory
mkdir -p $applicationsDirectory

# Tentacle Docker images only support once instance per container. Running multiple instances can be achieved by running multiple containers.
instanceName=Tentacle
configurationDirectory=/etc/octopus
applicationsDirectory=/home/Octopus/Applications

tentacle create-instance --instance "$instanceName" --config "$configurationDirectory/tentacle.config"
tentacle new-certificate --instance "$instanceName" --if-blank
tentacle configure --instance "$instanceName" --app "$applicationsDirectory" --noListen "True" --reset-trust
tentacle register-worker --instance "$instanceName" --server "$ServerUrl" --name "$HOSTNAME" --comms-style "$CommunicationsStype" --server-comms-port $ServerPort --apiKey $ServerApiKey --space "$Space" --workerpool="$WorkerPool" --force
