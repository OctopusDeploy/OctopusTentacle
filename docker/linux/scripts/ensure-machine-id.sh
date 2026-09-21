#!/bin/bash
#
# Give this container a machine-id of its own, kept with the Tentacle configuration.
#
# On Linux, Tentacle derives the key that protects sensitive values in tentacle.config (the certificate and its
# private key, proxy passwords) from /etc/machine-id. The Ubuntu base image ships a populated /etc/machine-id, so
# without this every container started from the same image shares one key, and anyone holding a copy of a
# container's tentacle.config can pull the public image and decrypt it (LEV-1171).
#
# The id is generated once, on the container's first start, into /etc/octopus/machine-id, right next to
# tentacle.config. That directory is what users persist to keep a Tentacle's identity, so the key travels with the
# configuration it protects and survives `docker rm` and re-creation on the same volume. On every start it is
# copied over /etc/machine-id before Tentacle runs. It is deliberately never regenerated: a fresh id on each start
# would be useless as a key.
set -eu

configurationDirectory=/etc/octopus
persistedMachineId="$configurationDirectory/machine-id"
tentacleConfiguration="$configurationDirectory/tentacle.config"

mkdir -p "$configurationDirectory"

# machine-id(5): exactly 32 lower-case hex characters, one line.
isValidMachineId() {
    [[ "$1" =~ ^[0-9a-f]{32}$ ]]
}

currentMachineId() {
    head -n 1 /etc/machine-id 2>/dev/null || true
}

generateMachineId() {
    # A v4 UUID from the kernel, minus the dashes, which is exactly what systemd-machine-id-setup would produce.
    tr -d '-' < /proc/sys/kernel/random/uuid
}

if [[ ! -s "$persistedMachineId" ]]; then
    imageMachineId=$(currentMachineId)

    if [[ -f "$tentacleConfiguration" ]] && isValidMachineId "$imageMachineId"; then
        # This volume was configured by an image from before machine-ids were persisted, so tentacle.config is
        # encrypted with that image's baked-in machine-id. That id cannot be recovered, but the current image's is
        # the best available guess: it is the same whenever the base layer has not changed. A freshly generated id
        # would be wrong for certain, and Tentacle would be unable to read its own certificate.
        echo "Persisting this image's machine-id for a Tentacle that was configured before machine-ids were persisted."
        newMachineId="$imageMachineId"
    else
        newMachineId=$(generateMachineId)
    fi

    # Owner-only: this is key material for tentacle.config.
    (umask 077 && printf '%s\n' "$newMachineId" > "$persistedMachineId")
fi

machineId=$(head -n 1 "$persistedMachineId")
if ! isValidMachineId "$machineId"; then
    echo "ERROR: $persistedMachineId does not contain a valid machine-id (32 hex characters). Remove the file to have a new one generated, but note that Tentacle will then be unable to decrypt the configuration that was encrypted with the old one." >&2
    exit 1
fi

if [[ "$(currentMachineId)" != "$machineId" ]]; then
    # It is the redirection that fails when the root filesystem is read-only, and bash reports that itself, so the
    # whole group is silenced and the warning below is the only message.
    if ! { printf '%s\n' "$machineId" > /etc/machine-id; } 2>/dev/null; then
        echo "WARNING: could not write /etc/machine-id (read-only root filesystem?). Tentacle will use the image's machine-id, which every container started from this image shares." >&2
    fi
fi
