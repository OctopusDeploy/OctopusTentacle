#!/usr/bin/env bash
#
# test-upgrade-from-previous.sh
#
# Proves that an in-place package upgrade keeps an existing Linux Tentacle's
# identity, and that the upgraded Tentacle re-encrypts its protected settings
# with the key it now generates per machine (LEV-1171).
#
# For each distribution, inside a throwaway container:
#
#   1. Install the last *released* Tentacle that predates the versioned scheme
#      from the public apt/rpm feed (see --previous).
#   2. Create an instance, generate a certificate, and set a proxy password and a
#      polling-proxy password: the three settings tentacle.config encrypts.
#   3. Record the thumbprint and check every protected value is in the format
#      the released version writes (no version prefix).
#   4. Install the freshly built package from _artifacts over the top.
#   5. `tentacle show-thumbprint` must return the same thumbprint: the new
#      version still decrypts what the old one wrote, without rewriting it.
#   6. Run `tentacle agent` for a few seconds. On start it re-encrypts the three
#      settings and logs one line per setting. Afterwards every value carries
#      the prefix, the thumbprint is unchanged, the key beside the configuration
#      (/etc/octopus/<instance>/machinekey.v1) is readable only by its owner, and
#      tentacle.config.before-reencryption holds the configuration exactly as the
#      released version left it, also owner-only.
#   7. Run the agent again: nothing left to re-encrypt, so it logs nothing.
#   8. Downgrade: reinstall the released version, confirm it cannot read the
#      re-encrypted configuration, restore the backup, and confirm it reads that
#      with the original thumbprint.
#
# Earlier versions took their key from /etc/machine-id when it was populated and
# generated one at /etc/octopus/machinekey when it was not. Both paths have to
# migrate, so by default each distribution is tested twice (see --key-source).
# Note the ubuntu:24.04 and redhat/ubi9 images both ship an EMPTY /etc/machine-id,
# so the test writes one itself for the machine-id run rather than relying on
# the image.
#
# Usage:
#   ./testing/linux-packages/test-upgrade-from-previous.sh [options]
#        (runs from anywhere; it resolves the repo root itself)
#   --pack           Build the .deb/.rpm first, via NUKE PackDebianPackage
#   --distro IMAGE   Test this distribution instead of the defaults. Repeatable.
#                    Defaults: ubuntu:24.04 (deb) and redhat/ubi9 (rpm).
#   --previous VER   The released version to upgrade from. Default: the last
#                    release before LEV-1171 shipped. "latest" takes whatever
#                    the feed serves, which only works until LEV-1171 is released.
#   --key-source S   Which legacy key the released version should have used:
#                    machine-id, generated, or both (default). For machine-id
#                    a 32-character id is written to /etc/machine-id before the
#                    released version runs; for generated it is left empty.
#   -h, --help       Show this help
#
# Needs Docker and outbound access to apt.octopus.com / rpm.octopus.com from
# inside the containers. Not wired into NUKE: it depends on the public feeds.
#
set -euo pipefail

REPO_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
cd "$REPO_DIR"
[[ -f "$REPO_DIR/build.sh" ]] \
    || { echo "ERROR: could not locate the repo root (got '$REPO_DIR')" >&2; exit 1; }

PACK=0
DISTROS=()
# The last release before the versioned scheme (LEV-1171). Bump deliberately, never to a version that includes it.
PREVIOUS_VERSION_WANTED="12.2.4509"
KEY_SOURCES=()

while [[ $# -gt 0 ]]; do
    case "$1" in
        --pack) PACK=1 ;;
        --distro)
            [[ $# -ge 2 ]] || { echo "--distro needs an image, e.g. --distro ubuntu:24.04" >&2; exit 2; }
            DISTROS+=("$2"); shift ;;
        --previous)
            [[ $# -ge 2 ]] || { echo "--previous needs a version, e.g. --previous 12.2.4509, or latest" >&2; exit 2; }
            PREVIOUS_VERSION_WANTED="$2"; shift ;;
        --key-source)
            [[ $# -ge 2 ]] || { echo "--key-source needs machine-id, generated or both" >&2; exit 2; }
            case "$2" in
                machine-id|generated) KEY_SOURCES+=("$2") ;;
                both) KEY_SOURCES=(machine-id generated) ;;
                *) echo "--key-source must be machine-id, generated or both (got '$2')" >&2; exit 2 ;;
            esac
            shift ;;
        -h|--help)
            awk 'NR>1 { if (/^#/) { sub(/^# ?/, ""); print } else { exit } }' "${BASH_SOURCE[0]}"
            exit 0 ;;
        *) echo "Unknown option: $1 (try --help)" >&2; exit 2 ;;
    esac
    shift
done

[[ ${#DISTROS[@]} -gt 0 ]] || DISTROS=("ubuntu:24.04" "redhat/ubi9")
[[ ${#KEY_SOURCES[@]} -gt 0 ]] || KEY_SOURCES=(machine-id generated)

###############################################################################
# Output helpers
###############################################################################

if [[ -t 1 ]]; then
    C_RESET=$'\033[0m'; C_RED=$'\033[31m'; C_GREEN=$'\033[32m'; C_BOLD=$'\033[1m'
else
    C_RESET=""; C_RED=""; C_GREEN=""; C_BOLD=""
fi

step()  { echo; echo "${C_BOLD}==> $*${C_RESET}"; }
info()  { echo "    $*"; }
die()   { echo "${C_RED}ERROR: $*${C_RESET}" >&2; exit 1; }

###############################################################################
# Environment
###############################################################################

# The packages are amd64; see test-packages-on-distros.sh for why these are set.
export DOCKER_DEFAULT_PLATFORM="linux/amd64"
export DOCKER_CLI_HINTS=false
export OCTOPUS__Tests__SecretManagerEnabled=False

command -v docker >/dev/null || die "docker is not on PATH"
docker info >/dev/null 2>&1 || die "the Docker daemon is not running"

###############################################################################
# Packages
###############################################################################

# find_one <deb|rpm> - the sole linux-x64 package of that type, or empty.
# Same globs as RunLinuxPackageTestsFor (build/Build.Tests.cs).
find_one() {
    local ext="$1" pattern matches
    case "$ext" in
        deb) pattern="_artifacts/deb/*_amd64.deb" ;;
        rpm) pattern="_artifacts/rpm/*.x86_64.rpm" ;;
        *)   return 1 ;;
    esac
    matches=$(ls -1 $pattern 2>/dev/null || true)
    [[ $(printf '%s' "$matches" | grep -c .) -eq 1 ]] || return 1
    printf '%s' "$matches"
}

if [[ $PACK -eq 1 ]]; then
    step "Building the linux-x64 packages via NUKE"
    rm -f _artifacts/deb/*_amd64.deb _artifacts/rpm/*.x86_64.rpm 2>/dev/null || true
    ./build.sh --target PackDebianPackage --runtime-ids linux-x64
fi

DEB_PATH=$(find_one deb) || die "expected exactly one _artifacts/deb/*_amd64.deb. Run with --pack, or clear out the extras."
RPM_PATH=$(find_one rpm) || die "expected exactly one _artifacts/rpm/*.x86_64.rpm. Run with --pack, or clear out the extras."

info "Package (deb): $DEB_PATH"
info "Package (rpm): $RPM_PATH"

###############################################################################
# The test that runs inside each container
###############################################################################

# Written to a temp file and mounted read-only, so the quoting stays readable.
# It is bash, run as root, on a distribution that has either apt-get or yum.
INNER=$(mktemp "${TMPDIR:-/tmp}/tentacle-upgrade-test.XXXXXX")
trap 'rm -f "$INNER"' EXIT
cat > "$INNER" <<'INNER_SH'
#!/bin/bash
set -euo pipefail

PACKAGE="$1"

# LinuxMachineKeyEncryptor.ProtectedValuePrefix. Must never change, so it is spelled out here.
PREFIX='$OctopusMachineKeyV1$'
INSTANCE="upgrade-test"
CONFIG="/etc/octopus/$INSTANCE/tentacle.config"
# The key is kept beside the configuration it protects; earlier versions kept theirs at /etc/octopus/machinekey.
MACHINE_KEY_FILE="/etc/octopus/$INSTANCE/machinekey.v1"
LEGACY_MACHINE_KEY_FILE="/etc/octopus/machinekey"
BACKUP="$CONFIG.before-reencryption"
PROTECTED_SETTINGS=(Tentacle.Certificate Octopus.Proxy.ProxyPassword Octopus.Server.Proxy.ProxyPassword)

log()  { echo; echo "==> $*"; }
fail() { echo "FAIL: $*" >&2; exit 1; }

# raw <key> - the stored (still encrypted) value of a setting in tentacle.config.
raw() { sed -n "s/.*key=\"$1\">\([^<]*\)<.*/\1/p" "$CONFIG"; }

install_released_tentacle() {
    if command -v apt-get >/dev/null 2>&1; then
        export DEBIAN_FRONTEND=noninteractive
        apt-get update
        apt-get install -y --no-install-recommends ca-certificates curl gnupg
        install -d -m 0755 /etc/apt/keyrings
        curl -fsSL https://apt.octopus.com/public.key | gpg --dearmor -o /etc/apt/keyrings/octopus.gpg
        echo "deb [signed-by=/etc/apt/keyrings/octopus.gpg] https://apt.octopus.com/ stable main" > /etc/apt/sources.list.d/octopus.list
        apt-get update
        if [[ "$PREVIOUS_VERSION_WANTED" == "latest" ]]; then
            apt-get install -y --no-install-recommends tentacle
        else
            apt-get install -y --no-install-recommends "tentacle=$PREVIOUS_VERSION_WANTED"
        fi
    elif command -v yum >/dev/null 2>&1; then
        curl -fsSL https://rpm.octopus.com/tentacle.repo -o /etc/yum.repos.d/tentacle.repo
        if [[ "$PREVIOUS_VERSION_WANTED" == "latest" ]]; then
            yum install -y tentacle
        else
            yum install -y "tentacle-$PREVIOUS_VERSION_WANTED"
        fi
    else
        fail "No supported package management tools found."
    fi
}

# run_agent_briefly <seconds> - start the agent, let it initialise, stop it, and
# return everything it printed. It has no Octopus Server to talk to, which does
# not matter: re-encryption happens before it starts listening.
run_agent_briefly() {
    timeout --signal=TERM "$1" Tentacle agent --instance "$INSTANCE" --noninteractive 2>&1 || true
}

log "Arranging the legacy key source: $KEY_SOURCE"
# Neither default image ships a populated /etc/machine-id, so the test decides which legacy path the released
# version takes rather than leaving it to the image.
case "$KEY_SOURCE" in
    machine-id)
        od -An -N16 -tx1 /dev/urandom | tr -d ' \n' > /etc/machine-id; echo >> /etc/machine-id
        echo "Wrote machine-id $(cat /etc/machine-id)" ;;
    generated)
        : > /etc/machine-id
        echo "Emptied /etc/machine-id" ;;
    *) fail "unknown KEY_SOURCE '$KEY_SOURCE'" ;;
esac

log "Installing the released Tentacle ($PREVIOUS_VERSION_WANTED)"
install_released_tentacle
PREVIOUS_VERSION=$(Tentacle version)
echo "Released version: $PREVIOUS_VERSION"

log "Configuring an instance with the released version"
mkdir -p "$(dirname "$CONFIG")"
Tentacle create-instance --instance "$INSTANCE" --config "$CONFIG"
Tentacle new-certificate --instance "$INSTANCE"
Tentacle proxy --instance "$INSTANCE" --proxyEnable=true --proxyHost=proxy.example.com --proxyPort=3128 --proxyUsername=proxy-user --proxyPassword="proxy secret one"
Tentacle polling-proxy --instance "$INSTANCE" --proxyEnable=true --proxyHost=polling-proxy.example.com --proxyPort=3128 --proxyUsername=polling-user --proxyPassword="polling secret two"

THUMBPRINT_BEFORE=$(Tentacle show-thumbprint --instance "$INSTANCE")
[[ -n "$THUMBPRINT_BEFORE" ]] || fail "the released version reported no thumbprint"
echo "Thumbprint: $THUMBPRINT_BEFORE"

for key in "${PROTECTED_SETTINGS[@]}"; do
    value=$(raw "$key")
    [[ -n "$value" ]] || fail "$key is not in $CONFIG"
    [[ "$value" != "$PREFIX"* ]] || fail "$key already uses the versioned scheme. The released version ($PREVIOUS_VERSION) includes LEV-1171; pin an older package for this test."
done
case "$KEY_SOURCE" in
    machine-id)
        [[ -s /etc/machine-id ]] || fail "/etc/machine-id is empty, so the released version cannot have used the machine-id derived key"
        [[ ! -e "$LEGACY_MACHINE_KEY_FILE" ]] || fail "the released version generated $LEGACY_MACHINE_KEY_FILE even though a machine-id was present; this run did not test the machine-id path"
        echo "The released version encrypted the settings with the key derived from /etc/machine-id." ;;
    generated)
        [[ -f "$LEGACY_MACHINE_KEY_FILE" ]] || fail "the released version did not generate $LEGACY_MACHINE_KEY_FILE; this run did not test the generated-key path"
        echo "The released version encrypted the settings with the key it generated at $LEGACY_MACHINE_KEY_FILE." ;;
esac
echo "All protected settings are in the released version's format."

cp "$CONFIG" /tmp/as-released.config

log "Upgrading to the package under test: $PACKAGE"
bash /test-scripts/install-package.sh "$PACKAGE"
NEW_VERSION=$(Tentacle version)
echo "Installed version: $NEW_VERSION"
[[ "$NEW_VERSION" != "$PREVIOUS_VERSION" ]] || fail "the version did not change; the upgrade did not take"

log "Reading the released version's configuration with the new version"
THUMBPRINT_AFTER_UPGRADE=$(Tentacle show-thumbprint --instance "$INSTANCE")
[[ "$THUMBPRINT_AFTER_UPGRADE" == "$THUMBPRINT_BEFORE" ]] \
    || fail "show-thumbprint reported '$THUMBPRINT_AFTER_UPGRADE' after the upgrade, but '$THUMBPRINT_BEFORE' before it"
# Reading is not migrating: show-thumbprint may run as a user who cannot write the configuration.
[[ "$(raw Tentacle.Certificate)" != "$PREFIX"* ]] || fail "show-thumbprint rewrote the configuration; only the agent should"
echo "The upgraded Tentacle decrypts the certificate written by $PREVIOUS_VERSION, and leaves it as it was."

log "Starting the agent, which re-encrypts the protected settings"
AGENT_LOG=$(run_agent_briefly 45)
RE_ENCRYPTED=$(grep -c "Re-encrypted the protected setting" <<< "$AGENT_LOG" || true)
if [[ "$RE_ENCRYPTED" != "${#PROTECTED_SETTINGS[@]}" ]]; then
    echo "$AGENT_LOG" | tail -40
    fail "expected ${#PROTECTED_SETTINGS[@]} settings to be re-encrypted on the first start, saw $RE_ENCRYPTED"
fi
for key in "${PROTECTED_SETTINGS[@]}"; do
    [[ "$(raw "$key")" == "$PREFIX"* ]] || fail "$key was not re-encrypted with the versioned scheme"
done
THUMBPRINT_AFTER_MIGRATION=$(Tentacle show-thumbprint --instance "$INSTANCE")
[[ "$THUMBPRINT_AFTER_MIGRATION" == "$THUMBPRINT_BEFORE" ]] \
    || fail "show-thumbprint reported '$THUMBPRINT_AFTER_MIGRATION' after re-encryption, but '$THUMBPRINT_BEFORE' before it"
MACHINE_KEY_MODE=$(stat -c %a "$MACHINE_KEY_FILE")
[[ "$MACHINE_KEY_MODE" == "600" ]] || fail "$MACHINE_KEY_FILE has permissions $MACHINE_KEY_MODE, expected 600"
[[ -f "$BACKUP" ]] || fail "no copy of the configuration was kept at $BACKUP before re-encrypting it"
# sha256sum rather than cmp: redhat/ubi9 has no diffutils.
[[ "$(sha256sum < "$BACKUP")" == "$(sha256sum < /tmp/as-released.config)" ]] || fail "$BACKUP is not the configuration exactly as $PREVIOUS_VERSION left it"
[[ "$(stat -c %a "$BACKUP")" == "600" ]] || fail "$BACKUP has permissions $(stat -c %a "$BACKUP"), expected 600"
echo "All ${#PROTECTED_SETTINGS[@]} protected settings were re-encrypted, the thumbprint is unchanged, the key file is owner-only, and the pre-upgrade configuration was kept."

log "Starting the agent again, which has nothing left to re-encrypt"
AGENT_LOG=$(run_agent_briefly 30)
RE_ENCRYPTED=$(grep -c "Re-encrypted the protected setting" <<< "$AGENT_LOG" || true)
[[ "$RE_ENCRYPTED" == "0" ]] || fail "the second start re-encrypted $RE_ENCRYPTED settings; it should have been a no-op"
echo "The second start was a no-op."

log "Downgrading to the released version, which needs the backup"
if command -v apt-get >/dev/null 2>&1; then
    apt-get install -y --allow-downgrades "tentacle=$PREVIOUS_VERSION"
else
    # `yum downgrade` exits 0 with "Nothing to do" when the released version is *higher* than the package under test,
    # as it is for a local build whose version number trails the feed, so check the result rather than the exit code.
    yum downgrade -y "tentacle-$PREVIOUS_VERSION"
    [[ "$(Tentacle version)" == "$PREVIOUS_VERSION" ]] || yum install -y "tentacle-$PREVIOUS_VERSION"
fi
[[ "$(Tentacle version)" == "$PREVIOUS_VERSION" ]] || fail "the downgrade did not take; Tentacle reports $(Tentacle version)"
if Tentacle show-thumbprint --instance "$INSTANCE" >/dev/null 2>&1; then
    fail "$PREVIOUS_VERSION read the re-encrypted configuration, which it should not be able to"
fi
cp "$BACKUP" "$CONFIG"
THUMBPRINT_AFTER_DOWNGRADE=$(Tentacle show-thumbprint --instance "$INSTANCE")
[[ "$THUMBPRINT_AFTER_DOWNGRADE" == "$THUMBPRINT_BEFORE" ]] \
    || fail "after restoring $BACKUP, $PREVIOUS_VERSION reported '$THUMBPRINT_AFTER_DOWNGRADE', but '$THUMBPRINT_BEFORE' before the upgrade"
echo "$PREVIOUS_VERSION reads the restored backup with the original thumbprint: the downgrade path works."

echo
echo "UPGRADE TEST PASSED: $PREVIOUS_VERSION -> $NEW_VERSION"
INNER_SH

###############################################################################
# Run it on each distribution
###############################################################################

FAILED=()

for DISTRO in "${DISTROS[@]}"; do
  for KEY_SOURCE in "${KEY_SOURCES[@]}"; do
    step "Testing an upgrade on $DISTRO (legacy key: $KEY_SOURCE)"

    # Same registry prefix as the matrix in build/Build.Tests.cs.
    IMAGE="docker.packages.octopushq.com/$DISTRO"
    docker pull "$IMAGE" >/dev/null 2>&1 || die "could not pull $IMAGE"

    # Probe for apt-get the way install-package.sh does, to pick the package.
    if docker run --rm --entrypoint sh "$IMAGE" -c 'command -v apt-get' >/dev/null 2>&1; then
        PACKAGE_PATH="/artifacts/deb/$(basename "$DEB_PATH")"
    else
        PACKAGE_PATH="/artifacts/rpm/$(basename "$RPM_PATH")"
    fi
    info "Package: $PACKAGE_PATH"

    if docker run --rm \
            -e KEY_SOURCE="$KEY_SOURCE" \
            -e PREVIOUS_VERSION_WANTED="$PREVIOUS_VERSION_WANTED" \
            -v "$REPO_DIR/linux-packages/test-scripts:/test-scripts:ro" \
            -v "$REPO_DIR/_artifacts:/artifacts:ro" \
            -v "$INNER:/upgrade-test.sh:ro" \
            "$IMAGE" \
            bash /upgrade-test.sh "$PACKAGE_PATH"; then
        echo "    ${C_GREEN}PASS${C_RESET}  $DISTRO ($KEY_SOURCE)"
    else
        echo "    ${C_RED}FAIL${C_RESET}  $DISTRO ($KEY_SOURCE)"
        FAILED+=("$DISTRO ($KEY_SOURCE)")
    fi
  done
done

echo
if [[ ${#FAILED[@]} -gt 0 ]]; then
    echo "${C_RED}${C_BOLD}FAILED: ${#FAILED[@]} of $(( ${#DISTROS[@]} * ${#KEY_SOURCES[@]} )) runs${C_RESET}"
    for d in "${FAILED[@]}"; do echo "      - $d"; done
    exit 1
fi
echo "${C_GREEN}${C_BOLD}ALL $(( ${#DISTROS[@]} * ${#KEY_SOURCES[@]} )) RUNS PASSED${C_RESET}"
