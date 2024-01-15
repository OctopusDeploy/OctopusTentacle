#!/bin/bash
set -eux

if [[ -n "$InstanceName" ]]; then
    instanceName="$InstanceName"
else
    instanceName=Tentacle
fi

tentacle agent --instance $instanceName --noninteractive
