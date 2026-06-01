#!/usr/bin/env bash
# Run a Windows-only test on the local UTM Windows 11 ARM VM and stream results back.
#
# Usage: run.sh "<nunit-filter>"
#   e.g. run.sh "Name~CancelThenAbandon_WhenGrandchild"
#
# Exit codes:
#   3  VM not created       -> caller should present setup-vm.md
#   4  VM up but no SSH      -> caller should point at setup-vm.md SSH section
#   2  bad usage
#   *  the dotnet test exit code (0 = pass)
set -euo pipefail

FILTER="${1:-}"
if [[ -z "$FILTER" ]]; then
  echo "usage: run.sh \"<nunit-filter>\"" >&2
  exit 2
fi

VM_NAME="${OCTO_WIN_VM:-octopus-win}"
SSH_HOST="${OCTO_WIN_SSH_HOST:-127.0.0.1}"
SSH_PORT="${OCTO_WIN_SSH_PORT:-2222}"
SSH_USER="${OCTO_WIN_SSH_USER:-dev}"
GUEST_REPO="${OCTO_WIN_GUEST_REPO:-C:/repo}"
TEST_PROJECT="${OCTO_WIN_TEST_PROJECT:-source/Octopus.Tentacle.Tests.Integration}"

# utmctl ships inside the app bundle and is usually not on PATH.
UTMCTL="$(command -v utmctl || true)"
[[ -z "$UTMCTL" && -x /Applications/UTM.app/Contents/MacOS/utmctl ]] && \
  UTMCTL=/Applications/UTM.app/Contents/MacOS/utmctl
if [[ -z "$UTMCTL" ]]; then
  echo "utmctl not found. Install UTM and build the VM first (see setup-vm.md)." >&2
  exit 3
fi

REPO_ROOT="$(git rev-parse --show-toplevel)"

SSH_OPTS=(-p "$SSH_PORT" -o StrictHostKeyChecking=accept-new -o ConnectTimeout=5)

# --- VM state ---------------------------------------------------------------
# `utmctl status <name>` prints the state and fails if the VM doesn't exist.
if ! STATE="$("$UTMCTL" status "$VM_NAME" 2>/dev/null)"; then
  echo "VM '$VM_NAME' not found in UTM. See setup-vm.md to build it once." >&2
  exit 3
fi

if [[ "$STATE" != "started" ]]; then
  echo "Starting VM '$VM_NAME' (state: $STATE)..." >&2
  "$UTMCTL" start "$VM_NAME" >&2
fi

# --- wait for SSH -----------------------------------------------------------
echo "Waiting for SSH on $SSH_HOST:$SSH_PORT..." >&2
for _ in $(seq 1 60); do
  if ssh "${SSH_OPTS[@]}" "$SSH_USER@$SSH_HOST" "exit 0" 2>/dev/null; then
    SSH_UP=1; break
  fi
  sleep 2
done
if [[ "${SSH_UP:-0}" != "1" ]]; then
  echo "VM is running but SSH never came up. Check the 127.0.0.1:$SSH_PORT->22 port" >&2
  echo "forward and that OpenSSH Server is running (see setup-vm.md)." >&2
  exit 4
fi

# --- sync source (no build artifacts) --------------------------------------
echo "Syncing source to $SSH_USER@$SSH_HOST:$GUEST_REPO ..." >&2
rsync -az --delete \
  --exclude '.git/' --exclude 'bin/' --exclude 'obj/' \
  --exclude 'artifacts/' --exclude 'TestResults/' --exclude '.vs/' \
  -e "ssh ${SSH_OPTS[*]}" \
  "$REPO_ROOT/" "$SSH_USER@$SSH_HOST:$GUEST_REPO/"

# --- run the test -----------------------------------------------------------
echo "Running: dotnet test $TEST_PROJECT --filter \"$FILTER\"" >&2
ssh "${SSH_OPTS[@]}" "$SSH_USER@$SSH_HOST" \
  "cd $GUEST_REPO && dotnet test $TEST_PROJECT --filter \"$FILTER\""
