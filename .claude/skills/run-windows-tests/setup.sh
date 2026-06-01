#!/usr/bin/env bash
# One-time, (nearly) clickless build of the Windows 11 ARM test VM using QEMU directly.
# Run from anywhere:  .claude/skills/run-windows-tests/setup.sh
#
# Fully scripted EXCEPT acquiring the Windows ARM ISO, which Microsoft gates behind a
# download form — drop it in $ISO_DIR and re-run. Everything else (firmware, disk, TPM,
# drivers, unattended install, provisioning, SSH) is automated and re-runnable.
#
# FIRST-RUN-VALIDATE: not yet exercised end-to-end. Most likely to need a second pass:
# the autounattend edition name, the virtio-net ARM64 driver path, and the rsync dest path.
set -euo pipefail
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

step "Installing QEMU + swtpm"
command -v qemu-system-aarch64 >/dev/null || brew install qemu
command -v swtpm >/dev/null || brew install swtpm
[[ -f "$FW_CODE" ]] || die "edk2 firmware not found at $FW_CODE"

mkdir -p "$VM_HOME"

step "Preparing UEFI vars + disk"
[[ -f "$VARS" ]] || dd if=/dev/zero of="$VARS" bs=1m count=64 2>/dev/null
[[ -f "$DISK" ]] || qemu-img create -f qcow2 "$DISK" "$DISK_SIZE"

step "Ensuring SSH keypair ($SSH_KEY)"
[[ -f "$SSH_KEY" ]] || ssh-keygen -t ed25519 -N "" -f "$SSH_KEY"

step "Locating Windows 11 ARM64 ISO in $ISO_DIR"
mkdir -p "$ISO_DIR"
WIN_ISO="$(find_win_iso)"
if [[ -z "$WIN_ISO" ]]; then
  note "No ISO found — building one via UUP dump (fetch-iso.sh)."
  bash "$HERE/fetch-iso.sh"
  WIN_ISO="$(find_win_iso)"
  [[ -n "$WIN_ISO" ]] || die "fetch-iso.sh did not produce an ISO in $ISO_DIR."
fi
note "Using $WIN_ISO"

step "Fetching virtio-win drivers"
[[ -f "$VIRTIO_ISO" ]] || curl -fL "$VIRTIO_URL" -o "$VIRTIO_ISO"

step "Building the answer ISO (autounattend + provisioning + your key)"
STAGE="$(mktemp -d)"; trap 'rm -rf "$STAGE"' EXIT
cp "$HERE/autounattend.xml" "$STAGE/autounattend.xml"
cp "$HERE/provision.ps1"    "$STAGE/provision.ps1"
cp "$SSH_KEY.pub"           "$STAGE/pubkey.txt"
rm -f "$ANSWER_ISO"
hdiutil makehybrid -iso -joliet -default-volume-name OCTO -o "$ANSWER_ISO" "$STAGE" >/dev/null

step "Booting the unattended installer (headless, ~20-40 min)"
note "Watch it if you like: change '-display none' to '-display cocoa' in lib.sh."
start_swtpm
# shellcheck disable=SC2046
qemu-system-aarch64 $(qemu_common_args) \
  -drive "file=$WIN_ISO,if=none,id=cd0,media=cdrom" -device usb-storage,drive=cd0,bootindex=0 \
  -drive "file=$VIRTIO_ISO,if=none,id=cd1,media=cdrom" -device usb-storage,drive=cd1 \
  -drive "file=$ANSWER_ISO,if=none,id=cd2,media=cdrom" -device usb-storage,drive=cd2 \
  -daemonize -pidfile "$PIDFILE"

step "Waiting for the VM to finish installing + provisioning, then SSH"
if ! wait_for_ssh 2700; then
  die "No SSH after 45 min. Re-run with '-display cocoa' to watch; check C:\\provision.log in the VM."
fi

step "Verifying toolchain over SSH"
ssh "${SSH_OPTS[@]}" "$SSH_USER@127.0.0.1" "dotnet --version & rsync --version"

step "Done"
note "Run a Windows test:  .claude/skills/run-windows-tests/run.sh \"Name~CancelThenAbandon_WhenGrandchild\""
