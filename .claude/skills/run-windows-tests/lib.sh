#!/usr/bin/env bash
# Shared config + helpers for the QEMU Windows 11 ARM test VM.
# Sourced by setup.sh, start-vm.sh, and run.sh. All values overridable via env.

VM_HOME="${OCTO_WIN_HOME:-$HOME/.octopus-win}"
DISK="$VM_HOME/disk.qcow2"
VARS="$VM_HOME/edk2-vars.fd"
TPM_DIR="$VM_HOME/tpm"
TPM_SOCK="$TPM_DIR/swtpm-sock"
PIDFILE="$VM_HOME/qemu.pid"
ANSWER_ISO="$VM_HOME/answer.iso"

ISO_DIR="${OCTO_WIN_ISO_DIR:-$HOME/UTM-ISOs}"      # where the Windows ARM ISO lives
VIRTIO_ISO="$VM_HOME/virtio-win.iso"
VIRTIO_URL="https://fedorapeople.org/groups/virt/virtio-win/direct-downloads/stable-virtio/virtio-win.iso"

# UUP dump: build the Windows 11 ARM64 ISO from Microsoft's update servers (fetch-iso.sh).
UUP_API="${OCTO_WIN_UUP_API:-https://api.uupdump.net}"
UUP_GET="${OCTO_WIN_UUP_GET:-https://uupdump.net/get.php}"
WIN_LANG="${OCTO_WIN_LANG:-en-us}"
WIN_EDITION="${OCTO_WIN_EDITION:-professional}"
UUP_SEARCH="${OCTO_WIN_UUP_SEARCH:-Windows 11}"

SSH_PORT="${OCTO_WIN_SSH_PORT:-2222}"
SSH_USER="${OCTO_WIN_SSH_USER:-dev}"
SSH_PASS="${OCTO_WIN_SSH_PASS:-Octo-pass-1}"        # only used inside the VM for autologon
SSH_KEY="${OCTO_WIN_SSH_KEY:-$HOME/.ssh/id_ed25519}"
GUEST_REPO="${OCTO_WIN_GUEST_REPO:-C:/repo}"

MEM_MB="${OCTO_WIN_MEM_MB:-8192}"
CPUS="${OCTO_WIN_CPUS:-4}"
DISK_SIZE="${OCTO_WIN_DISK_SIZE:-64G}"

# edk2 UEFI firmware shipped by Homebrew's qemu.
FW_CODE="${OCTO_WIN_FW_CODE:-$(brew --prefix qemu 2>/dev/null)/share/qemu/edk2-aarch64-code.fd}"
[[ -f "$FW_CODE" ]] || FW_CODE="$(brew --prefix 2>/dev/null)/share/qemu/edk2-aarch64-code.fd"

SSH_OPTS=(-p "$SSH_PORT" -o StrictHostKeyChecking=accept-new -o ConnectTimeout=5 -i "$SSH_KEY")

step() { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }
note() { printf '    %s\n' "$*"; }
die()  { printf '\033[1;31mERROR:\033[0m %s\n' "$*" >&2; exit 1; }

ssh_up() { ssh "${SSH_OPTS[@]}" "$SSH_USER@127.0.0.1" "exit 0" 2>/dev/null; }

wait_for_ssh() { # $1 = seconds
  local deadline=$(( SECONDS + ${1:-120} ))
  while (( SECONDS < deadline )); do ssh_up && return 0; sleep 3; done
  return 1
}

vm_running() { [[ -f "$PIDFILE" ]] && kill -0 "$(cat "$PIDFILE")" 2>/dev/null; }

find_win_iso() { find "$ISO_DIR" -maxdepth 1 -iname '*.iso' 2>/dev/null | head -n1; }

# Common QEMU args (machine, accel, firmware, disk, NIC, TPM, headless display).
# Callers append install media for first boot. Storage = NVMe (inbox driver, no virtio
# needed at install). NIC = virtio-net (driver injected via autounattend from virtio ISO).
qemu_common_args() {
  echo \
    -name octopus-win \
    -machine virt,gic-version=3 -accel hvf -cpu host \
    -smp "$CPUS" -m "$MEM_MB" \
    -drive "if=pflash,format=raw,readonly=on,file=$FW_CODE" \
    -drive "if=pflash,format=raw,file=$VARS" \
    -device ramfb \
    -device qemu-xhci -device usb-kbd -device usb-tablet \
    -drive "file=$DISK,if=none,id=hd,format=qcow2,cache=writeback" \
    -device nvme,drive=hd,serial=octowin \
    -netdev "user,id=net0,hostfwd=tcp:127.0.0.1:$SSH_PORT-:22" \
    -device virtio-net-pci,netdev=net0 \
    -chardev "socket,id=chrtpm,path=$TPM_SOCK" \
    -tpmdev emulator,id=tpm0,chardev=chrtpm \
    -device tpm-tis-device,tpmdev=tpm0 \
    -rtc base=localtime \
    -display none
}

start_swtpm() {
  command -v swtpm >/dev/null || die "swtpm not installed (brew install swtpm)"
  mkdir -p "$TPM_DIR"
  [[ -S "$TPM_SOCK" ]] && return 0
  swtpm socket --tpmstate "dir=$TPM_DIR" \
    --ctrl "type=unixio,path=$TPM_SOCK" --tpm2 --daemon
  for _ in $(seq 1 20); do [[ -S "$TPM_SOCK" ]] && return 0; sleep 0.2; done
  die "swtpm socket did not appear at $TPM_SOCK"
}
