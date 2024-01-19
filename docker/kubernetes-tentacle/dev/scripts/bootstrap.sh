#!/bin/bash

./dev-scripts/start-debugger.sh &

(./scripts/configure-tentacle.sh && /scripts/run-tentacle.sh)

wait -n

exit $?