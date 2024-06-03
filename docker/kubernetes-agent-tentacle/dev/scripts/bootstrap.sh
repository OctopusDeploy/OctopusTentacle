#!/bin/bash

./dev-scripts/start-debugger.sh & ./scripts/configure-and-run.sh

wait -n

exit $?