#!/bin/bash
set -o xtrace
set -e

SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )

echo Script directory is: $SCRIPT_DIR

$SCRIPT_DIR/install-deps.sh
$SCRIPT_DIR/dotnet-install.sh
source $SCRIPT_DIR/set-dotnet-envvars.sh
$SCRIPT_DIR/run-integration-tests.sh
