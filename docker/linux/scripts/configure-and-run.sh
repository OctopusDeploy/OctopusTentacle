#!/bin/bash
set -eu

# A path on the same device as / lives in the container's own layer and is lost when the container is re-created.
is_on_volume() { [ "$(stat -c %d "$1")" != "$(stat -c %d /)" ]; }
is_persisted() { mkdir -p "$1" && is_on_volume "$1"; }
is_file_persisted() { [ -e "$1" ] && is_on_volume "$1"; }
if { is_persisted /etc/octopus || is_file_persisted /etc/octopus/tentacle.config; } && ! is_persisted /var/octopus/tentacle/keys; then
    echo "/etc/octopus or /etc/octopus/tentacle.config is persisted but /var/octopus/tentacle/keys is not. The configuration cannot be decrypted without its key once this container is re-created. Mount /var/octopus/tentacle/keys on a volume too." >&2
    exit 1
fi

/scripts/configure-tentacle.sh

# Clear sensitive env variables that should not be inherited by the tentacle process or any forked child processes
 unset ServerApiKey BearerToken ServerUsername ServerPassword

exec /scripts/run-tentacle.sh
