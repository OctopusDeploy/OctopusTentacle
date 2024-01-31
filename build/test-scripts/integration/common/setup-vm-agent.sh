#!/bin/bash

# VMs can be out dated so install the latest dotnet to ensure the dll to run
# was not compiled with a newer version
source /tmp/dotnet-install.sh
install_dotnet 6.0
