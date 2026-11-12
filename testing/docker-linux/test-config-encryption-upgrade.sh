#!/usr/bin/env bash
#
# Upgrades a Tentacle configuration written by a previous image, whose key came from its /etc/machine-id, to a new
# image on the same /etc/octopus volume, following the recovery steps in docker/readme.md.
#
# Usage: ./testing/docker-linux/test-config-encryption-upgrade.sh <new image> [previous image]
#
set -euo pipefail

NEW_IMAGE=$1
PREVIOUS_IMAGE=${2:-octopusdeploy/tentacle:latest}
VOLUME="tentacle-config-upgrade-$$"
WORK_DIR=$(mktemp -d)
trap 'docker volume rm -f "$VOLUME" >/dev/null; rm -rf "$WORK_DIR"' EXIT

FAILURES=0
check() {
    if [[ "$2" == "$3" ]]; then echo "PASS  $1"; else echo "FAIL  $1: expected '$3', got '$2'"; FAILURES=$((FAILURES + 1)); fi
}

in_container() {
    local image=$1 command=$2
    shift 2
    docker run --rm -v "$VOLUME:/etc/octopus" "$@" --entrypoint bash "$image" -c "$command" 2>&1
}

in_container "$PREVIOUS_IMAGE" 'tentacle create-instance --instance Tentacle --config /etc/octopus/tentacle.config >/dev/null && tentacle new-certificate --instance Tentacle >/dev/null'
THUMBPRINT=$(in_container "$PREVIOUS_IMAGE" 'tentacle show-thumbprint --instance Tentacle')
in_container "$PREVIOUS_IMAGE" 'cat /etc/machine-id' > "$WORK_DIR/machine-id"

OUTPUT=$(in_container "$NEW_IMAGE" 'tentacle show-thumbprint --instance Tentacle' || true)
check "the new image cannot decrypt without the previous machine-id" \
    "$(grep -c 'docker run --rm --entrypoint cat <previous image> /etc/machine-id' <<<"$OUTPUT")" "1"

OUTPUT=$(in_container "$NEW_IMAGE" 'timeout 30 tentacle run --instance Tentacle' -v "$WORK_DIR/machine-id:/etc/machine-id:ro" || true)
check "the agent re-encrypts with the previous machine-id mounted" \
    "$(grep -c "Re-encrypted the protected setting 'Tentacle.Certificate'" <<<"$OUTPUT")" "1"

check "the certificate carries the versioned prefix" \
    "$(in_container "$NEW_IMAGE" 'grep -cF "key=\"Tentacle.Certificate\">\$OctopusMachineKeyV1\$" /etc/octopus/tentacle.config')" "1"
check "the key is owner-only" "$(in_container "$NEW_IMAGE" 'stat -c %a /etc/octopus/config-encryption.key')" "600"
check "the thumbprint is unchanged without the previous machine-id" "$(in_container "$NEW_IMAGE" 'tentacle show-thumbprint --instance Tentacle')" "$THUMBPRINT"

OUTPUT=$(in_container "$NEW_IMAGE" 'timeout 30 tentacle run --instance Tentacle' || true)
check "the agent starts again without the previous machine-id" "$(grep -c 'Unable to decrypt' <<<"$OUTPUT")" "0"

exit $((FAILURES > 0))
