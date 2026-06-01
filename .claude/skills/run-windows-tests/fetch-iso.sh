#!/usr/bin/env bash
# Build a Windows 11 ARM64 ISO via UUP dump. The API resolve + package download happen on
# the Mac; the conversion runs in a Debian container, because the UUP converter requires
# chntpw, which cannot build on Apple Silicon (the sidneys tap's openssl@1.0 fails its
# tests). In Linux, chntpw/wimtools/genisoimage install cleanly via apt.
#
# Result lands in $ISO_DIR. ~5 GB download (inside the container) + a few minutes convert.
set -euo pipefail
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"

command -v docker >/dev/null && docker info >/dev/null 2>&1 \
  || die "Docker must be installed and running (the ISO conversion runs in a Linux container)."
command -v jq >/dev/null || brew install jq

step "Resolving the latest ARM64 $UUP_SEARCH build from UUP dump"
QUERY="$(printf '%s' "$UUP_SEARCH" | sed 's/ /%20/g')"
UUID="$(curl -fsSL "$UUP_API/listid.php?search=$QUERY" \
  | jq -r '(.response.builds | if type=="object" then [.[]] else . end)
            | map(select(.arch=="arm64"))
            | sort_by(.created) | reverse
            | ((map(select(.title|test("Insider|Dev|Beta|Preview";"i")|not)))[0] // .[0])
            | .uuid')"
[[ -n "$UUID" && "$UUID" != "null" ]] || die "Could not resolve an ARM64 build id from UUP dump."
note "build id: $UUID  (lang=$WIN_LANG edition=$WIN_EDITION)"

step "Downloading the UUP script package"
# Keep the work dir under $HOME so Docker Desktop's default file sharing can bind-mount it.
WORK="$VM_HOME/uup-build"
rm -rf "$WORK"; mkdir -p "$WORK"
# autodl=2 => "download and convert to ISO" (sets ConvertConfig AutoStart). The zip is small
# (~KB): it's the scripts; the ~5 GB payload is pulled by aria2 during conversion.
curl -fL --data "autodl=2&updates=1&cleanup=1" \
  "$UUP_GET?id=$UUID&pack=$WIN_LANG&edition=$WIN_EDITION" -o "$WORK/uup.zip"
unzip -q "$WORK/uup.zip" -d "$WORK"
[[ -f "$WORK/uup_download_linux.sh" ]] || die "UUP package missing uup_download_linux.sh (check params)."

step "Converting to ISO inside a Debian container (downloads ~5 GB)"
docker run --rm -v "$WORK:/uup" -w /uup debian:bookworm bash -c '
  set -e
  apt-get update -qq
  DEBIAN_FRONTEND=noninteractive apt-get install -y -qq \
    aria2 cabextract wimtools chntpw genisoimage curl >/dev/null
  chmod +x uup_download_linux.sh
  ./uup_download_linux.sh
'

ISO="$(find "$WORK" -maxdepth 1 -iname '*.iso' | head -n1)"
[[ -n "$ISO" ]] || die "Conversion finished but produced no .iso (see output above)."
mkdir -p "$ISO_DIR"
mv "$ISO" "$ISO_DIR/"
rm -rf "$WORK"
step "ISO ready: $ISO_DIR/$(basename "$ISO")"
