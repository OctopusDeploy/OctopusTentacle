#!/bin/bash
set -o xtrace
set -e

# Runs integration tests on VM backed agents.
# Currently VM agents don't supply sudo, so tests that need that are not run.


SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )


$SCRIPT_DIR/setup-vm-agent.sh

source $SCRIPT_DIR/set-dotnet-envvars.sh

itdll=`pwd`/build/outputs/integrationtests/net6.0/linux-x64/Octopus.Tentacle.Tests.Integration.dll


# We don't care about the exit code of dotnet test and instead depend on tests passing.
set +e
if [ -n "$TENTACLE_IT_WITH_SUDO" ]; then
  TEST_CASE_FILTER="TestCategory!=TentacleBackwardsCompatibility"
  if [ "$TENTACLE_IT_WITH_SUDO" = "NO_SYSTEMD" ]; then
    TEST_CASE_FILTER="$TEST_CASE_FILTER&TestCategory!=RequiresSystemd"
  fi
  sudo -E env PATH=$PATH dotnet vstest $itdll "/testcasefilter:$TEST_CASE_FILTER" /logger:logger://teamcity /TestAdapterPath:/opt/TeamCity/BuildAgent/plugins/dotnet/tools/vstest15 /logger:console;verbosity=detailed
else
  dotnet vstest $itdll "/testcasefilter:TestCategory!=RequiresSudoOnLinux&TestCategory!=TentacleBackwardsCompatibility" /logger:logger://teamcity /TestAdapterPath:/opt/TeamCity/BuildAgent/plugins/dotnet/tools/vstest15 /logger:console;verbosity=detailed
fi

