#!/bin/bash


./install-deps.sh
./dotnet-install.sh
source set-dotnet-envvars.sh
./run-integration-tests.sh
