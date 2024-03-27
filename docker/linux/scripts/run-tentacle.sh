#!/bin/bash
set -ux

if [[ "$DISABLE_DIND" == "Y" ]]; then
    echo Docker-in-Docker is disabled.
    echo "Starting Tentacle"
    exec tentacle agent --instance Tentacle --noninteractive
fi

echo "Starting Docker-in-Docker daemon. This requires that this container be run in privileged mode."
nohup /usr/local/bin/dockerd-entrypoint.sh dockerd &
DIND_PID=$!

echo "Starting Tentacle"
tentacle agent --instance Tentacle --noninteractive &
TENTACLE_PID=$!

EXTERNAL_SIGNAL=""

function handle_interrupt() {
    EXTERNAL_SIGNAL="INT"
    echo "Received SIGINT. Forwarding..."
    kill -s INT $TENTACLE_PID $DIND_PID
}

function handle_term() {
    EXTERNAL_SIGNAL="TERM"
    echo "Received SIGTERM. Forwarding..."
    kill -s TERM $TENTACLE_PID $DIND_PID
}

trap handle_interrupt SIGINT
trap handle_term SIGTERM

exited_process=0

wait -n -p exited_process $TENTACLE_PID $DIND_PID
waited_exit_code=$?
if [ -n "$EXTERNAL_SIGNAL" ]; then
    echo "Exiting due to external signal: $EXTERNAL_SIGNAL"
    wait
    tentacle_exit_code=$waited_exit_code
else
    if [ "$exited_process" == "$DIND_PID" ]; then
        echo "Docker-in-Docker Daemon exited with code $waited_exit_code"
        echo "Terminating Tentacle..."
        kill -s INT $TENTACLE_PID
        wait $TENTACLE_PID
        tentacle_exit_code=$?
    else
        echo "Tentacle exited with code $waited_exit_code"
        echo "Terminating Docker-in-Docker..."
        kill -s INT $DIND_PID
        wait $DIND_PID
        tentacle_exit_code=$waited_exit_code
    fi
fi

exit $tentacle_exit_code
