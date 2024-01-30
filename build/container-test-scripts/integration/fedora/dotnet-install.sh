#!/bin/bash
set -o xtrace
set -e

# See https://github.com/OctopusDeploy/BuildAgentAutomation/blob/main/Common/shared/dotnet-install.sh
curl --silent --fail -L -O https://dot.net/v1/dotnet-install.sh || exit 1
chmod +x dotnet-install.sh || exit 1

./dotnet-install.sh --channel 6.0
