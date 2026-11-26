#!/usr/bin/env bash
#
# LOCAL ONLY, not used by CI. Runs Linux-only unit tests as root in a .NET SDK container for this host's architecture.
#
# Usage: ./testing/local/run-linux-unit-tests-in-docker.sh [dotnet test filter]
#
set -euo pipefail

REPO_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
FILTER=${1:-FullyQualifiedName~Octopus.Tentacle.Tests.Configuration}
case "$(uname -m)" in
    arm64|aarch64) PLATFORM=linux/arm64 ;;
    *) PLATFORM=linux/amd64 ;;
esac

docker run --rm --platform "$PLATFORM" \
    -v "$REPO_DIR:/src:ro" \
    -e FILTER="$FILTER" \
    -e NUGET_PACKAGES=/work/.nuget-packages \
    mcr.microsoft.com/dotnet/sdk:10.0 \
    bash -euc '
        mkdir /work
        tar -C /src --exclude=./.git --exclude="*/bin" --exclude="*/obj" --exclude=./_artifacts -cf - . | tar -C /work -xf -
        cd /work
        project=source/Octopus.Tentacle.Tests/Octopus.Tentacle.Tests.csproj
        dotnet test "$project" -f net10.0 --filter "$FILTER"
    '
