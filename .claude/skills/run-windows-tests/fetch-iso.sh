#!/usr/bin/env bash
# Build a Windows 11 ARM64 ISO via UUP dump (downloads from Microsoft's update servers
# and converts locally). Run by setup.sh when no ISO is present; can run standalone.
# Result lands in $ISO_DIR. ~5 GB download + a few minutes of conversion.
#
# FIRST-RUN-VALIDATE: the get.php package params and the listid JSON shape are the most
# likely things to need a tweak — each step echoes what it resolved so failures localize.
set -euo pipefail
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"

step "Installing UUP converter dependencies (brew)"
brew install aria2 cabextract wimlib cdrtools jq
# chntpw lives in an external tap and is sometimes flaky; the converter only needs it for
# some operations, so don't hard-fail setup if it won't install.
brew tap sidneys/homebrew 2>/dev/null || true
brew install sidneys/homebrew/chntpw 2>/dev/null || note "chntpw unavailable — continuing without it."

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

step "Downloading the UUP package + building the ISO"
WORK="$(mktemp -d)"; trap 'rm -rf "$WORK"' EXIT
# autodl=2 => "download and convert to ISO"; the zip bundles uup_download_macos.sh.
curl -fL --data "autodl=2&updates=1&cleanup=1" \
  "$UUP_GET?id=$UUID&pack=$WIN_LANG&edition=$WIN_EDITION" -o "$WORK/uup.zip"
unzip -q "$WORK/uup.zip" -d "$WORK/uup"
[[ -f "$WORK/uup/uup_download_macos.sh" ]] || die "UUP package missing uup_download_macos.sh (check params)."
( cd "$WORK/uup" && bash ./uup_download_macos.sh )

ISO="$(find "$WORK/uup" -maxdepth 2 -iname '*.iso' | head -n1)"
[[ -n "$ISO" ]] || die "Conversion finished but produced no .iso (see output above)."
mkdir -p "$ISO_DIR"
mv "$ISO" "$ISO_DIR/"
step "ISO ready: $ISO_DIR/$(basename "$ISO")"
