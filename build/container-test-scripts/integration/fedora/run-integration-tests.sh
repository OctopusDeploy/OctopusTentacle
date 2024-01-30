#!/bin/bash
set -o xtrace
set -e

itdll=`pwd`/build/outputs/integrationtests/net6.0/linux-x64/Octopus.Tentacle.Tests.Integration.dll
dotnet vstest $itdll /testcasefilter:TestCategory!=RequiresSudoOnLinux /logger:logger://teamcity /TestAdapterPath:/opt/TeamCity/BuildAgent/plugins/dotnet/tools/vstest15 /logger:console;verbosity=detailed
