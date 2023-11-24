#!/bin/bash

eval $(minikube docker-env --unset)

export BUILD_DATE=$(date -Iseconds)
./build.sh PackDebianPackage

fullDebFileName=`find _artifacts/deb/tentacle_*_arm64.deb`
if [[ $fullDebFileName =~ _.*_(.*)_arm64\.deb ]]; then
    export BUILD_ARCH="arm64"
    export BUILD_NUMBER=${BASH_REMATCH[1]}

    eval $(minikube docker-env)

    docker-compose -f docker-compose.build.yml build --pull octopusdeploy-tentacle-linux
fi
