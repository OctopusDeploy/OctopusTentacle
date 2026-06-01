#!/usr/bin/env bash
# Boot the already-installed Windows VM headless and wait until SSH answers.
# Idempotent: a no-op if it's already up. Used by run.sh and after setup.sh.
set -euo pipefail
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"

[[ -f "$DISK" ]] || die "No VM disk at $DISK — run setup.sh first."

if ssh_up; then note "VM already up."; exit 0; fi

if ! vm_running; then
  step "Starting QEMU (headless)"
  start_swtpm
  # shellcheck disable=SC2046
  qemu-system-aarch64 $(qemu_common_args) -daemonize -pidfile "$PIDFILE"
fi

step "Waiting for SSH on 127.0.0.1:$SSH_PORT"
wait_for_ssh 180 || die "VM booted but SSH never answered (port forward / sshd?)."
note "Up."
