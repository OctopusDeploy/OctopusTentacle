#!/bin/bash
set -eux

if [[ "$DISABLE_DIND" == "Y" ]]; then
    echo Docker-in-Docker is disabled.
    DIND_PID=""
else
    echo "Starting Docker-in-Docker daemon. This requires that this container be run in privileged mode."
    nohup /usr/local/bin/dockerd-entrypoint.sh dockerd &
    DIND_PID=$!
fi

function handle_interrupt() {
    echo "Received SIGINT. Forwarding..."
    kill -INT $TENTACLE_PID $DIND_PID
}

function handle_term() {
    echo "Received SIGTERM. Forwarding..."
    kill -TERM $TENTACLE_PID $DIND_PID
}

function handle_kill() {
    echo "Received SIGKILL. Forwarding..."
    kill -KILL $TENTACLE_PID $DIND_PID
}

tentacle agent --instance Tentacle --noninteractive &
TENTACLE_PID=$!

trap handle_interrupt SIGINT
trap handle_term SIGTERM
trap handle_kill SIGKILL

wait
