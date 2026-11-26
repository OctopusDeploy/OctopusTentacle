#!/usr/bin/env bash
#
# LOCAL ONLY, not used by CI. Runs Linux-only tests in a .NET SDK container for this host's architecture, with systemd running.
#
# Usage: ./testing/local/run-linux-unit-tests-in-docker.sh [dotnet test filter]
#
# Unit tests run as root. Integration tests run as a non-root user with passwordless sudo, as [RequiresSudoOnLinux] tests expect.
#
set -euo pipefail

REPO_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
FILTER=${1:-FullyQualifiedName~.Configuration.}
case "$(uname -m)" in
    arm64|aarch64) PLATFORM=linux/arm64 ;;
    *) PLATFORM=linux/amd64 ;;
esac
IMAGE=tentacle-local-linux-tests
CONTAINER=tentacle-local-linux-tests-run

docker build --quiet --platform "$PLATFORM" -t "$IMAGE" - >/dev/null <<'EOF'
FROM mcr.microsoft.com/dotnet/sdk:10.0
RUN apt-get update && apt-get install -y --no-install-recommends systemd systemd-sysv sudo && rm -rf /var/lib/apt/lists/*
RUN useradd -m -s /bin/bash builder && echo 'builder ALL=(ALL) NOPASSWD:ALL' > /etc/sudoers.d/builder && chmod 440 /etc/sudoers.d/builder
RUN mkdir /work && chown builder /work
STOPSIGNAL SIGRTMIN+3
CMD ["/lib/systemd/systemd"]
EOF

# systemd must be the container's first process, so the tests run through docker exec.
docker rm -f "$CONTAINER" >/dev/null 2>&1 || true
docker run -d --name "$CONTAINER" --platform "$PLATFORM" \
    --privileged --cgroupns=host -v /sys/fs/cgroup:/sys/fs/cgroup:rw --tmpfs /run --tmpfs /run/lock \
    -v "$REPO_DIR:/src:ro" \
    "$IMAGE" >/dev/null
trap 'docker rm -f "$CONTAINER" >/dev/null' EXIT

for _ in $(seq 1 30); do
    state=$(docker exec "$CONTAINER" systemctl is-system-running 2>/dev/null || true)
    [[ "$state" == running || "$state" == degraded ]] && break
    sleep 1
done

docker exec "$CONTAINER" bash -euc 'tar -C /src --exclude=./.git --exclude="*/bin" --exclude="*/obj" --exclude=./_artifacts -cf - . | tar -C /work -xf -'

run_tests() {
    # Both runs share /work, so the build outputs must belong to whichever user runs next.
    docker exec "$CONTAINER" chown -R "$1" /work
    docker exec -u "$1" -e FILTER="$FILTER" -e NUGET_PACKAGES=/work/.nuget-packages -w /work "$CONTAINER" \
        dotnet test "$2" -f net10.0 --filter "$FILTER"
}
run_tests root source/Octopus.Tentacle.Tests/Octopus.Tentacle.Tests.csproj
run_tests builder source/Octopus.Tentacle.Tests.Integration/Octopus.Tentacle.Tests.Integration.csproj
