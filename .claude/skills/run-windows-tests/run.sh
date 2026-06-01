#!/usr/bin/env bash
# Run a Windows-only test on the local QEMU Windows 11 ARM VM and stream results back.
#
# Usage: run.sh "<nunit-filter>"
#   e.g. run.sh "Name~CancelThenAbandon_WhenGrandchild"
#
# Exit codes:
#   2  bad usage
#   3  VM not built       -> caller should tell the user to run setup.sh
#   4  VM up but no SSH
#   *  the dotnet test exit code (0 = pass)
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$HERE/lib.sh"

FILTER="${1:-}"
[[ -n "$FILTER" ]] || { echo "usage: run.sh \"<nunit-filter>\"" >&2; exit 2; }

TEST_PROJECT="${OCTO_WIN_TEST_PROJECT:-source/Octopus.Tentacle.Tests.Integration}"
# cwRsync (choco) is cygwin-based, so the Windows dest is a /cygdrive path.
RSYNC_DEST="${OCTO_WIN_RSYNC_DEST:-/cygdrive/c/repo}"

[[ -f "$DISK" ]] || { echo "VM not built. Tell the user to run setup.sh once." >&2; exit 3; }

# Boot if needed and wait for SSH.
if ! bash "$HERE/start-vm.sh"; then exit 4; fi

REPO_ROOT="$(git rev-parse --show-toplevel)"

step "Syncing source -> $SSH_USER@127.0.0.1:$RSYNC_DEST (no build artifacts)"
rsync -az --delete \
  --exclude '.git/' --exclude 'bin/' --exclude 'obj/' \
  --exclude 'artifacts/' --exclude 'TestResults/' --exclude '.vs/' \
  -e "ssh ${SSH_OPTS[*]}" \
  "$REPO_ROOT/" "$SSH_USER@127.0.0.1:$RSYNC_DEST/"

step "dotnet test $TEST_PROJECT --filter \"$FILTER\""
# Explicit powershell invocation so we don't depend on the SSH default shell (kept as cmd
# for rsync's sake). Set-Location + ';' work in Windows PowerShell 5.1.
ssh "${SSH_OPTS[@]}" "$SSH_USER@127.0.0.1" \
  "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Set-Location '$GUEST_REPO'; dotnet test '$TEST_PROJECT' --filter '$FILTER'\""
