#!/bin/bash
# Sourced by configure-and-run.sh, before Tentacle is configured: a container that can't run Docker-in-Docker should
# stop before it registers with Octopus Server, not leave behind a target that never comes online.
#
# Starts the Docker-in-Docker daemon in the background, sets DIND_PID for run-tentacle.sh to watch, and waits until the
# daemon is ready. Exits the container if the daemon stops or does not become ready in time.

DIND_READY_TIMEOUT_SECONDS=120

function failDockerInDocker() {
    echo "Docker-in-Docker $1, so Tentacle has not been configured or registered." >&2
    echo "Docker-in-Docker lets deployments run container images. It usually needs the container to run with --privileged." >&2
    echo "Run the container with --privileged, or set DISABLE_DIND=Y if your deployments don't run container images." >&2
    exit "$2"
}

echo "Starting Docker-in-Docker daemon. This requires that this container be run in privileged mode."
nohup /usr/local/bin/dockerd-entrypoint.sh dockerd &
DIND_PID=$!

# Without this, a container stopped while the daemon starts would wait out Docker's stop timeout: the shell is PID 1,
# which ignores SIGTERM unless it is trapped. run-tentacle.sh replaces this trap once Tentacle is running.
trap 'kill -s TERM $DIND_PID; wait $DIND_PID; exit 143' SIGTERM

for ((elapsed = 0; elapsed < DIND_READY_TIMEOUT_SECONDS; elapsed++)); do
    if ! kill -0 "$DIND_PID" 2>/dev/null; then
        dind_exit_code=0
        wait "$DIND_PID" || dind_exit_code=$?
        # Exit non-zero even if the daemon did not, so restart policies treat it as a failure.
        failDockerInDocker "exited with code $dind_exit_code while starting" "$((dind_exit_code == 0 ? 1 : dind_exit_code))"
    fi

    if docker info >/dev/null 2>&1; then
        echo "Docker-in-Docker daemon is ready."
        trap - SIGTERM
        return 0
    fi

    sleep 1
done

kill -s TERM "$DIND_PID"
failDockerInDocker "did not become ready within $DIND_READY_TIMEOUT_SECONDS seconds" 1
