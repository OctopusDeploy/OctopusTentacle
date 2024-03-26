#!/bin/bash
set -eu

/scripts/configure-tentacle.sh
CONFIGURE_RESULT=$?
if [ "$CONFIGURE_RESULT" != "0" ]; then
  exit $CONFIGURE_RESULT
fi

exec /scripts/run-tentacle.sh
