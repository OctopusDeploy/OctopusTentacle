#!/usr/bin/env bash
# One-time setup orchestrator for the UTM Windows 11 ARM test VM.
#
# Run from the repo root:  .claude/skills/run-windows-tests/setup.sh
#
# This automates everything that has a CLI. UTM has NO API to create a VM or run the
# Windows installer, so this script PAUSES at exactly two GUI moments and tells you what
# to click, then takes back over. Everything else (tooling, key, provisioning, port
# forward check, verification) is scripted.
#
# FIRST-RUN-VALIDATE: not yet exercised against a real VM. Treat snags as expected on
# the first pass; each stage prints what it's doing so failures are localizable.
set -euo pipefail

VM_NAME="${OCTO_WIN_VM:-octopus-win}"
SSH_PORT="${OCTO_WIN_SSH_PORT:-2222}"
SSH_USER="${OCTO_WIN_SSH_USER:-dev}"
ISO_DIR="${OCTO_WIN_ISO_DIR:-$HOME/UTM-ISOs}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
KEY="${OCTO_WIN_SSH_KEY:-$HOME/.ssh/id_ed25519}"

step() { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }
note() { printf '    %s\n' "$*"; }
pause() { printf '\n\033[1;33m[manual]\033[0m %s\n' "$*"; read -r -p "    Press Enter when done... " _; }

# --- 1. UTM -----------------------------------------------------------------
step "Ensuring UTM is installed"
if [[ ! -d /Applications/UTM.app ]]; then
  brew install --cask utm
else
  note "UTM already installed."
fi
UTMCTL=/Applications/UTM.app/Contents/MacOS/utmctl

# --- 2. SSH key -------------------------------------------------------------
step "Ensuring an SSH keypair exists ($KEY)"
if [[ ! -f "$KEY" ]]; then
  ssh-keygen -t ed25519 -N "" -f "$KEY"
else
  note "Key already present."
fi
PUBKEY="$(cat "$KEY.pub")"

# --- 3. Windows 11 ARM ISO --------------------------------------------------
step "Checking for a Windows 11 ARM64 ISO in $ISO_DIR"
mkdir -p "$ISO_DIR"
ISO="$(find "$ISO_DIR" -maxdepth 1 -iname '*.iso' | head -n1 || true)"
if [[ -z "$ISO" ]]; then
  note "No ISO found. CrystalFetch is the easiest way to download the ARM64 ISO."
  if ! brew list --cask crystalfetch >/dev/null 2>&1; then brew install --cask crystalfetch; fi
  open -a CrystalFetch || true
  pause "Download a Windows 11 **ARM64** ISO and save it into: $ISO_DIR"
  ISO="$(find "$ISO_DIR" -maxdepth 1 -iname '*.iso' | head -n1 || true)"
  [[ -n "$ISO" ]] || { echo "Still no ISO in $ISO_DIR. Re-run once it's there." >&2; exit 1; }
fi
note "Using ISO: $ISO"

# --- 4. Create the VM (GUI — no CLI exists) ---------------------------------
step "Creating the VM in UTM"
if "$UTMCTL" status "$VM_NAME" >/dev/null 2>&1; then
  note "VM '$VM_NAME' already exists — skipping creation."
else
  open -a UTM
  cat <<EOF

    In UTM (one-time, no CLI for this):
      1. +  ->  Virtualize  ->  Windows
      2. Check "Install drivers and SPICE tools"; Browse to:
             $ISO
      3. ~4 CPUs, 8 GB RAM, ~64 GB disk
      4. Name the VM exactly: $VM_NAME
      5. Finish, then run the Windows installer + create a local user named: $SSH_USER
      6. After first boot, install SPICE guest tools from the mounted CD.
EOF
  pause "Complete the Windows install and reach the desktop as user '$SSH_USER'."
fi

# --- 5. Port forward 127.0.0.1:$SSH_PORT -> guest:22 (GUI) -------------------
step "Configuring loopback SSH port forward"
cat <<EOF

    In UTM: select '$VM_NAME' -> Edit -> Network -> (QEMU/Emulated VLAN) -> Port Forward:
        Protocol TCP | Host 127.0.0.1 | Host Port $SSH_PORT | Guest Port 22
    Loopback is what lets automation reach the VM. Save and (re)start the VM.
EOF
pause "Add the port forward and ensure the VM is running."

# --- 6. Bootstrap provisioning from inside the guest ------------------------
step "Serving provision.ps1 for the guest to pull"
HOST_IP="$(ipconfig getifaddr en0 2>/dev/null || ipconfig getifaddr en1 2>/dev/null || echo "")"
PORT_HTTP=8099
( cd "$HERE" && python3 -m http.server "$PORT_HTTP" >/dev/null 2>&1 & echo $! > "$HERE/.httpd.pid" )
trap 'kill "$(cat "$HERE/.httpd.pid" 2>/dev/null)" 2>/dev/null || true; rm -f "$HERE/.httpd.pid"' EXIT
cat <<EOF

    In the VM, open an **elevated PowerShell** and paste this one line
    (it installs OpenSSH + .NET 8 SDK + rsync and authorizes your key):

      \$k='$PUBKEY'; & ([scriptblock]::Create((irm "http://$HOST_IP:$PORT_HTTP/provision.ps1"))) -PublicKey \$k

EOF
note "If the VM can't reach $HOST_IP, use the host IP shown in the guest's default gateway."
pause "Run the one-liner in the VM and wait for it to print 'Provisioning complete.'"

# --- 7. Verify over SSH -----------------------------------------------------
step "Verifying SSH + toolchain from the Mac"
SSH_OPTS=(-p "$SSH_PORT" -o StrictHostKeyChecking=accept-new -o ConnectTimeout=5 -i "$KEY")
for _ in $(seq 1 30); do
  if ssh "${SSH_OPTS[@]}" "$SSH_USER@127.0.0.1" "exit 0" 2>/dev/null; then OK=1; break; fi
  sleep 2
done
if [[ "${OK:-0}" != "1" ]]; then
  echo "Could not SSH to 127.0.0.1:$SSH_PORT. Check the port forward and that sshd is running." >&2
  exit 4
fi
ssh "${SSH_OPTS[@]}" "$SSH_USER@127.0.0.1" "dotnet --version; rsync --version | Select-Object -First 1"

step "Setup complete"
note "Run a Windows test with:"
note "  .claude/skills/run-windows-tests/run.sh \"Name~CancelThenAbandon_WhenGrandchild\""
