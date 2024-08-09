#!/bin/bash

# VMs can be out dated so install the latest dotnet to ensure the dll to run
# was not compiled with a newer version
SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )

source $SCRIPT_DIR/dotnet-install.sh
# We install the 8.0 sdk and also the 6.0 runtime as the tests require it
install_dotnet --runtime 6.0 --sdk 8.0
