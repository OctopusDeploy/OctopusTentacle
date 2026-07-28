#!/bin/bash
set -eu

/scripts/configure-tentacle.sh

# Clear sensitive env variables that should not be inherited by the tentacle process or any forked child processes
 unset ServerApiKey BearerToken ServerUsername ServerPassword

exec /scripts/run-tentacle.sh
