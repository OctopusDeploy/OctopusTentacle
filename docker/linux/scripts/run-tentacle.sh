#!/bin/bash
# Sourced by configure-and-run.sh, after start-docker-in-docker.sh has started the daemon and set DIND_PID.
# Not -e: the exit codes of the processes it waits on are handled below.
set +e -ux

if [[ "$DISABLE_DIND" == "Y" ]]; then
    echo Docker-in-Docker is disabled.
    echo "Starting Tentacle"
    exec tentacle agent --instance Tentacle --noninteractive
fi

echo "Starting Tentacle"
tentacle agent --instance Tentacle --noninteractive &
TENTACLE_PID=$!

EXTERNAL_SIGNAL=""

function handle_term() {
    EXTERNAL_SIGNAL="TERM"
    echo "Received SIGTERM. Forwarding..."
    kill -s TERM $TENTACLE_PID $DIND_PID
}

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
        kill -s TERM $TENTACLE_PID
        wait $TENTACLE_PID
        # Exit with the daemon's code, not that of Tentacle stopping, and non-zero so restart policies treat it as a failure.
        tentacle_exit_code=$(( waited_exit_code == 0 ? 1 : waited_exit_code ))
    else
        echo "Tentacle exited with code $waited_exit_code"
        echo "Terminating Docker-in-Docker..."
        kill -s TERM $DIND_PID
        wait $DIND_PID
        tentacle_exit_code=$waited_exit_code
    fi
fi

exit $tentacle_exit_code
