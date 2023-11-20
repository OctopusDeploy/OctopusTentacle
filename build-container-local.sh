#!/bin/bash

eval $(minikube docker-env --unset)

export BUILD_DATE=$(date -Iseconds)
./build.sh PackDebianPackage

#change to amd64 or arm64
export BUILD_ARCH="arm64"
fullDebFileName=`find _artifacts/deb/tentacle_*_$BUILD_ARCH.deb`
if [[ $fullDebFileName =~ _.*_(.*)_$BUILD_ARCH\.deb ]]; then
    export BUILD_NUMBER=${BASH_REMATCH[1]}

    eval $(minikube docker-env)

    docker-compose -f docker-compose.build.yml build --pull octopusdeploy-tentacle-linux
fi