#!/bin/bash

# VMs can be out dated so install the latest dotnet to ensure the dll to run
# was not compiled with a newer version
SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )

source $SCRIPT_DIR/dotnet-install.sh
install_dotnet 6.0
