#!/bin/bash
set -eu

# Before anything reads or writes tentacle.config: the key that protects it comes from /etc/machine-id.
/scripts/ensure-machine-id.sh

/scripts/configure-tentacle.sh

# Clear sensitive env variables that should not be inherited by the tentacle process or any forked child processes
 unset ServerApiKey BearerToken ServerUsername ServerPassword

exec /scripts/run-tentacle.sh
