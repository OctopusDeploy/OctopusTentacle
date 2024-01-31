#!/bin/bash
set -o xtrace
set -e

# Runs integration tests on VM backed agents.
# Currently VM agents don't supply sudo, so tests that need that are not run.


SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )


$SCRIPT_DIR/setup-vm-agent.sh

source $SCRIPT_DIR/set-dotnet-envvars.sh

itdll=`pwd`/build/outputs/integrationtests/net6.0/linux-x64/Octopus.Tentacle.Tests.Integration.dll
dotnet vstest $itdll /testcasefilter:TestCategory!=RequiresSudoOnLinux /logger:logger://teamcity /TestAdapterPath:/opt/TeamCity/BuildAgent/plugins/dotnet/tools/vstest15 /logger:console;verbosity=detailed
