#!/usr/bin/env bash
#
# build-and-test-linux-docker-image.sh
#
# Builds and verifies the Linux Tentacle container image (docker/linux/Dockerfile)
# end to end, the same way TeamCity does:
#
#   TeamFireAndMotion_OctopusTentacle_TentacleVLatest_net80
#     -> Build: Tentacle (Linux)      (produces _artifacts/deb/tentacle_<ver>_amd64.deb)
#     -> Build: Linux Docker image    (docker-compose -f docker-compose.build.yml build ...)
#
# Stages:
#   1. deb    - build the linux-x64 .deb via NUKE (--target PackDebianPackage)
#   2. image  - build docker/linux/Dockerfile via docker-compose.build.yml
#   3. smoke  - assert the image's contents and behaviour
#   4. e2e    - stand up a real Octopus Server and point a listening and a
#               polling Tentacle, built from the fresh image, at it
#
# Environment:
#   OCTOPUS_SERVER_BASE64_LICENSE
#       A base64-encoded Octopus Server licence. An unlicensed server enforces
#       a limit of 0 targets, so without one no Tentacle can register.
#       If this is unset AND stdin is a TTY, the script reads a development
#       licence from 1Password:
#           op://software licencing/octopus deploy ultimate license key base64
#       the same item the Octopus Server repo's ./environment.sh setup uses.
#       `op` may prompt you to authenticate; the value is never printed. It is
#       never invoked non-interactively (CI, background shells, agents), since
#       `op` cannot prompt there and would hang.
#       With a licence, stage 4 asserts full registration, comms style and
#       health status. Without one it falls back to asserting configuration
#       and server connectivity up to the licence refusal.
#
# Note: stage 1 runs the real NUKE build, which stamps the calculated version
# into installer/Octopus.Tentacle.Installer/Product.wxs and regenerates
# .nuke/build.schema.json. Both are tracked, so `git checkout --` them
# afterwards if you do not want that noise in your working tree.
#
# Usage: ./testing/docker-linux/build-and-test-linux-docker-image.sh [options]
#        (runs from anywhere; it resolves the repo root itself)
#   --skip-deb     Reuse the newest existing _artifacts/deb/tentacle_*_amd64.deb
#   --skip-image   Reuse the already-built image for the resolved BUILD_NUMBER
#   --skip-smoke   Skip the image smoke tests
#   --skip-e2e     Skip the Octopus Server registration test
#   --keep         Leave the e2e containers running afterwards (for debugging)
#   --no-1password Never invoke `op`; use OCTOPUS_SERVER_BASE64_LICENSE only.
#                  Needed anywhere `op` must not run (CI, sandboxes).
#   -h, --help     Show this help
#
set -euo pipefail

# This script lives in testing/docker-linux/, but every path below (./build.sh,
# _artifacts/deb, docker-compose.build.yml, docker/linux/Dockerfile) is relative
# to the repo root, so resolve that and work from there.
REPO_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
cd "$REPO_DIR"
[[ -f "$REPO_DIR/docker-compose.build.yml" ]] \
    || { echo "ERROR: could not locate the repo root (got '$REPO_DIR')" >&2; exit 1; }

SKIP_DEB=0
SKIP_IMAGE=0
SKIP_SMOKE=0
SKIP_E2E=0
KEEP=0
NO_1PASSWORD=0

while [[ $# -gt 0 ]]; do
    case "$1" in
        --skip-deb) SKIP_DEB=1 ;;
        --skip-image) SKIP_IMAGE=1 ;;
        --skip-smoke) SKIP_SMOKE=1 ;;
        --skip-e2e) SKIP_E2E=1 ;;
        --keep) KEEP=1 ;;
        --no-1password) NO_1PASSWORD=1 ;;
        # Print the header comment block, minus the shebang, as the help text.
        -h|--help)
            awk 'NR>1 { if (/^#/) { sub(/^# ?/, ""); print } else { exit } }' "${BASH_SOURCE[0]}"
            exit 0 ;;
        *) echo "Unknown option: $1 (try --help)" >&2; exit 2 ;;
    esac
    shift
done

###############################################################################
# Output helpers
###############################################################################

if [[ -t 1 ]]; then
    C_RESET=$'\033[0m'; C_RED=$'\033[31m'; C_GREEN=$'\033[32m'
    C_YELLOW=$'\033[33m'; C_BOLD=$'\033[1m'
else
    C_RESET=""; C_RED=""; C_GREEN=""; C_YELLOW=""; C_BOLD=""
fi

TESTS_RUN=0
TESTS_FAILED=0
FAILED_NAMES=()

step()  { echo; echo "${C_BOLD}==> $*${C_RESET}"; }
info()  { echo "    $*"; }
warn()  { echo "${C_YELLOW}    WARN: $*${C_RESET}"; }
die()   { echo "${C_RED}ERROR: $*${C_RESET}" >&2; exit 1; }

pass()  { TESTS_RUN=$((TESTS_RUN + 1)); echo "    ${C_GREEN}PASS${C_RESET}  $1"; }
fail()  {
    TESTS_RUN=$((TESTS_RUN + 1))
    TESTS_FAILED=$((TESTS_FAILED + 1))
    FAILED_NAMES+=("$1")
    echo "    ${C_RED}FAIL${C_RESET}  $1"
    [[ -n "${2:-}" ]] && echo "          $2"
    return 0
}

# assert_contains <name> <haystack> <needle>
assert_contains() {
    if [[ "$2" == *"$3"* ]]; then
        pass "$1"
    else
        fail "$1" "expected to find '$3' in: $(echo "$2" | head -3 | tr '\n' ' ')"
    fi
}

# assert_equals <name> <actual> <expected>
assert_equals() {
    if [[ "$2" == "$3" ]]; then
        pass "$1"
    else
        fail "$1" "expected '$3', got '$2'"
    fi
}

###############################################################################
# Host platform
###############################################################################

# The .deb we build (and therefore the image) is amd64. On an arm64 host
# (Apple Silicon) Docker would otherwise default to arm64 and the `apt install`
# of the .deb would fail, so pin the platform for every build and run.
PLATFORM="linux/amd64"
export DOCKER_DEFAULT_PLATFORM="$PLATFORM"
# Suppress Docker Scout hints, which are written to stderr and look like errors.
export DOCKER_CLI_HINTS=false

if [[ "$(uname -m)" == "arm64" || "$(uname -m)" == "aarch64" ]]; then
    info "Host is $(uname -m); building and running everything as $PLATFORM under emulation."
fi

command -v docker >/dev/null || die "docker is not on PATH"
docker info >/dev/null 2>&1 || die "the Docker daemon is not running"

###############################################################################
# 1. Build the .deb
###############################################################################

find_deb() {
    # Newest tentacle_*_amd64.deb, or empty if there are none.
    ls -t _artifacts/deb/tentacle_*_amd64.deb 2>/dev/null | head -1
}

if [[ $SKIP_DEB -eq 1 ]]; then
    step "Stage 1/4: .deb (skipped, reusing existing)"
    DEB_PATH=$(find_deb)
    [[ -n "$DEB_PATH" ]] || die "--skip-deb was given but no _artifacts/deb/tentacle_*_amd64.deb exists"
else
    step "Stage 1/4: building the linux-x64 .deb via NUKE"
    info "This runs 'PackDebianPackage', which cross-compiles Tentacle for linux-x64"
    info "and packages it inside the tool-linux-packages container. Takes a few minutes."

    # NUKE resolves every [ParameterFromPasswordStore] at start-up, which shells out
    # to the 1Password CLI. Those secrets are only used by the SBOM target, and the
    # 'op' call hangs when it cannot prompt interactively, so opt out.
    export OCTOPUS__Tests__SecretManagerEnabled=False

    # Only linux-x64 is needed for docker/linux/Dockerfile; building every runtime
    # ID would take far longer for no benefit.
    ./build.sh --target PackDebianPackage --runtime-ids linux-x64

    DEB_PATH=$(find_deb)
    [[ -n "$DEB_PATH" ]] || die "PackDebianPackage finished but no .deb landed in _artifacts/deb/"
fi

DEB_FILE=$(basename "$DEB_PATH")
# tentacle_<BUILD_NUMBER>_amd64.deb
BUILD_NUMBER="${DEB_FILE#tentacle_}"
BUILD_NUMBER="${BUILD_NUMBER%_amd64.deb}"
[[ -n "$BUILD_NUMBER" ]] || die "could not derive BUILD_NUMBER from $DEB_FILE"

# A local NUKE build appends '-<yyyyMMddHHmmss>' to FullSemVer for the package
# filename (Build.cs), but the binary itself only carries the plain FullSemVer,
# followed by '+Branch...Sha...'. Strip the timestamp so both the smoke test and
# the e2e stage can compare against the version Tentacle actually reports. On
# TeamCity there is no timestamp suffix and this is a straight comparison.
EXPECTED_VERSION=$(echo "$BUILD_NUMBER" | sed -E 's/-[0-9]{14}$//')

info "Package:      $DEB_PATH"
info "BUILD_NUMBER: $BUILD_NUMBER"

###############################################################################
# 2. Build the image
###############################################################################

IMAGE="docker.packages.octopushq.com/octopusdeploy/tentacle:${BUILD_NUMBER}-linux"

# TeamCity uses `date --iso-8601=seconds`, which is GNU-only; this is equivalent
# and works on both macOS and Linux.
BUILD_DATE=$(date +%Y-%m-%dT%H:%M:%S%z)

export BUILD_NUMBER BUILD_DATE
export BUILD_ARCH=amd64

if [[ $SKIP_IMAGE -eq 1 ]]; then
    step "Stage 2/4: image (skipped, reusing existing)"
    docker image inspect "$IMAGE" >/dev/null 2>&1 \
        || die "--skip-image was given but $IMAGE has not been built"
else
    step "Stage 2/4: building the image from docker/linux/Dockerfile"
    info "Image: $IMAGE"
    docker compose -f docker-compose.build.yml build --pull octopusdeploy-tentacle-linux
fi

###############################################################################
# 3. Smoke tests
###############################################################################

# run_in_image <command...> - run a shell snippet in the image, bypassing the
# entrypoint (which would otherwise try to register a Tentacle).
run_in_image() {
    docker run --rm --platform "$PLATFORM" --entrypoint bash "$IMAGE" -c "$1" 2>&1
}

if [[ $SKIP_SMOKE -eq 0 ]]; then
    step "Stage 3/4: smoke-testing the image"

    # --- base image -------------------------------------------------------
    OS_ID=$(run_in_image '. /etc/os-release && echo "$ID"')
    OS_VER=$(run_in_image '. /etc/os-release && echo "$VERSION_ID"')
    assert_equals "base image is Ubuntu"        "$OS_ID"  "ubuntu"
    assert_equals "base image is 22.04 (jammy)" "$OS_VER" "22.04"

    # --- .NET runtime dependencies ---------------------------------------
    # These are the Ubuntu 22.04 equivalents of the Debian 11 names the
    # Dockerfile used to install; getting them wrong fails the image build,
    # but a silently-missing one would fail Tentacle at runtime instead.
    for pkg in ca-certificates libc6 libgcc-s1 libgssapi-krb5-2 libicu70 libssl3 libstdc++6 zlib1g; do
        # Captured rather than piped into grep -q: see the note on wait_for_log.
        PKG_STATUS=$(run_in_image "dpkg-query -W -f='\${Status}' $pkg")
        if [[ "$PKG_STATUS" == *"install ok installed"* ]]; then
            pass "runtime dependency installed: $pkg"
        else
            fail "runtime dependency installed: $pkg"
        fi
    done

    # --- tools the Dockerfile installs explicitly ------------------------
    for tool in curl dos2unix jq sudo xxd; do
        if run_in_image "command -v $tool >/dev/null" >/dev/null 2>&1; then
            pass "tool on PATH: $tool"
        else
            fail "tool on PATH: $tool"
        fi
    done

    # --- Tentacle --------------------------------------------------------
    TENTACLE_VERSION=$(run_in_image 'tentacle version')
    assert_equals "'tentacle version' reports the built version" \
        "${TENTACLE_VERSION%%+*}" "$EXPECTED_VERSION"

    SYMLINK=$(run_in_image 'readlink -f /usr/bin/tentacle')
    assert_equals "/usr/bin/tentacle symlinks to the install" "$SYMLINK" "/opt/octopus/tentacle/Tentacle"

    # OpenSSL 3 is what libssl3 provides; Tentacle's .deb accepts 1.0/1.1/3.
    OPENSSL_VER=$(run_in_image 'openssl version')
    assert_contains "OpenSSL is 3.x" "$OPENSSL_VER" "OpenSSL 3."

    # --- Docker-in-Docker ------------------------------------------------
    # install-docker.sh adds Docker's apt repo. That repo is distro-specific,
    # so it has to point at .../linux/ubuntu now, not .../linux/debian.
    DOCKER_LIST=$(run_in_image 'cat /etc/apt/sources.list.d/docker.list')
    assert_contains "Docker apt source targets the Ubuntu repo" "$DOCKER_LIST" "download.docker.com/linux/ubuntu"
    assert_contains "Docker apt source targets jammy"           "$DOCKER_LIST" "jammy"

    for bin in docker dockerd containerd; do
        if run_in_image "command -v $bin >/dev/null" >/dev/null 2>&1; then
            pass "docker-in-docker binary present: $bin"
        else
            fail "docker-in-docker binary present: $bin"
        fi
    done

    for f in /usr/local/bin/dind /usr/local/bin/dockerd-entrypoint.sh; do
        if run_in_image "test -x $f" >/dev/null 2>&1; then
            pass "executable: $f"
        else
            fail "executable: $f"
        fi
    done

    # install-docker.sh switches the iptables alternatives; if that step were
    # skipped, dockerd would fail to create its NAT chain at runtime.
    # Read the link one level only: -f would resolve on through to the shared
    # xtables-*-multi binary, which does not say which mode was selected.
    IPTABLES_ALT=$(run_in_image 'readlink /etc/alternatives/iptables')
    if [[ "$IPTABLES_ALT" == *"iptables-legacy" || "$IPTABLES_ALT" == *"iptables-nft" ]]; then
        pass "iptables alternative selected ($(basename "$IPTABLES_ALT"))"
    else
        fail "iptables alternative selected" "got '$IPTABLES_ALT'"
    fi

    # Both files are checked separately: `grep -q a b` exits 0 on the first
    # match in *either* file, so passing both at once would pass with only one.
    if run_in_image 'getent passwd dockremap >/dev/null && grep -q "^dockremap:" /etc/subuid && grep -q "^dockremap:" /etc/subgid' >/dev/null 2>&1; then
        pass "dockremap user and subuid/subgid ranges configured"
    else
        fail "dockremap user and subuid/subgid ranges configured"
    fi

    # --- entrypoint scripts ----------------------------------------------
    for s in configure-and-run.sh configure-tentacle.sh run-tentacle.sh dockerd-entrypoint.sh; do
        if run_in_image "test -x /scripts/$s" >/dev/null 2>&1; then
            pass "executable: /scripts/$s"
        else
            fail "executable: /scripts/$s"
        fi
    done

    # --- image metadata ---------------------------------------------------
    INSPECT=$(docker image inspect "$IMAGE")
    assert_contains "ENTRYPOINT is configure-and-run.sh" "$INSPECT" "/scripts/configure-and-run.sh"
    assert_contains "port 10933 is exposed"              "$INSPECT" "10933/tcp"
    assert_contains "/var/lib/docker is a volume"        "$INSPECT" "/var/lib/docker"
    assert_contains "version label matches the build"    "$INSPECT" "$BUILD_NUMBER"

    # --- entrypoint behaviour --------------------------------------------
    # The EULA gate and the argument validation are the two ways a user most
    # often gets a container that will not start, so assert they still bite.
    EULA_OUT=$(docker run --rm --platform "$PLATFORM" "$IMAGE" 2>&1 || true)
    assert_contains "container refuses to start without ACCEPT_EULA=Y" "$EULA_OUT" "You must accept the EULA"

    NOSERVER_OUT=$(docker run --rm --platform "$PLATFORM" -e ACCEPT_EULA=Y -e ServerApiKey=API-FAKE "$IMAGE" 2>&1 || true)
    assert_contains "container refuses to start without ServerUrl" "$NOSERVER_OUT" "Please specify an Octopus Server"
fi

###############################################################################
# 4. End-to-end registration test
###############################################################################

E2E_NET="tentacle-e2e-net"
E2E_SQL="tentacle-e2e-sql"
E2E_SERVER="tentacle-e2e-octopus"
E2E_LISTENING="tentacle-e2e-listening"
E2E_POLLING="tentacle-e2e-polling"
E2E_CONTAINERS=("$E2E_LISTENING" "$E2E_POLLING" "$E2E_SERVER" "$E2E_SQL")

# random_password - 20 characters of upper case, lower case and digits.
#
# Alphanumeric only, deliberately: the value is interpolated into a SQL Server
# connection string, a `sqlcmd -P` argument and a JSON request body, and
# punctuation would need escaping differently in each of the three.
#
# At least one character of each class is guaranteed rather than left to
# chance. SQL Server's SA password policy demands characters from three of its
# four categories, and with punctuation off the table that means upper, lower
# and digit are all mandatory - a purely random draw could legally come up
# without one and fail the container at start-up.
#
# /dev/urandom is read in fixed-size chunks so `head` finishes before `tr`
# does. Piping from an unbounded read would SIGPIPE the producer and trip
# `set -o pipefail`.
random_password() {
    local length=20 pw
    while :; do
        pw=""
        while (( ${#pw} < length )); do
            pw+=$(head -c 256 /dev/urandom | LC_ALL=C tr -cd 'A-Za-z0-9')
        done
        pw=${pw:0:length}
        if [[ "$pw" == *[[:upper:]]* && "$pw" == *[[:lower:]]* && "$pw" == *[[:digit:]]* ]]; then
            printf '%s' "$pw"
            return 0
        fi
    done
}

# Populated by e2e_prepare, which only runs when stage 4 is actually going to
# run - there is no reason to mint credentials or touch 1Password under
# --skip-e2e. Declared here so `set -u` and dump_logs are safe either way.
OCTOPUS_USER="admin"
SA_PASSWORD=""
OCTOPUS_PASSWORD=""
OCTOPUS_LICENSE=""

# resolve_license prefers OCTOPUS_SERVER_BASE64_LICENSE. Failing that it tries
# 1Password, which is where the Octopus Server repo's own
# `./environment.sh setup` gets a local development licence from (see
# setup-octopus/OctopusInstanceManager.cs). Note the vault is "software
# licencing", not the "Octopus Server Secrets for Tests" vault the secrets
# docs walk you through, so you need access to both. The value is never
# echoed, and it is handed to the container through an env file rather than
# `-e`, so it stays out of `ps` output.
#
# Only attempted when stdin is a TTY. `op` has to prompt to authenticate, so
# with nothing to prompt it hangs or fails - the same trap that stalls NUKE's
# own 1Password lookup in stage 1. Gating on a TTY keeps it out of CI runners,
# background shells and agents without anyone having to remember a flag.
OCTOPUS_LICENSE_OP_REF="op://software licencing/octopus deploy ultimate license key base64/value"

resolve_license() {
    OCTOPUS_LICENSE="${OCTOPUS_SERVER_BASE64_LICENSE:-}"
    [[ -n "$OCTOPUS_LICENSE" ]] && return 0
    [[ $NO_1PASSWORD -eq 0 ]] || return 0
    [[ -t 0 ]] || return 0
    command -v op >/dev/null 2>&1 || return 0

    info "Looking up a development licence in 1Password (you may be prompted)..."
    if OCTOPUS_LICENSE=$(op read "$OCTOPUS_LICENSE_OP_REF" 2>/dev/null) && [[ -n "$OCTOPUS_LICENSE" ]]; then
        info "Licence retrieved from 1Password (${#OCTOPUS_LICENSE} chars)."
    else
        OCTOPUS_LICENSE=""
        warn "Could not read a licence from 1Password. Sign in with 'op signin'"
        warn "and check you have access to the 'software licencing' vault, or set"
        warn "OCTOPUS_SERVER_BASE64_LICENSE yourself."
    fi
}

# Note: this also runs *before* the stack is started, to clear out anything a
# previous run left behind, so it must not touch $E2E_HELPER - that is cleaned
# up by the EXIT trap instead.
e2e_teardown() {
    if [[ $KEEP -eq 1 ]]; then
        warn "--keep was given; leaving the e2e containers running."
        warn "Tear down with: docker rm -f ${E2E_CONTAINERS[*]}; docker network rm $E2E_NET"
        return
    fi
    docker rm -f "${E2E_CONTAINERS[@]}" >/dev/null 2>&1 || true
    docker network rm "$E2E_NET" >/dev/null 2>&1 || true
}

# dump_logs <container> - print the tail of a container's logs, for diagnosis.
dump_logs() {
    echo "    --- last 30 log lines from $1 ---"
    # The Octopus Server image logs its own `license --licenseBase64 <value>`
    # invocation, so scrub the licence before anything reaches the terminal or
    # a CI log. Base64 never contains '|', so it is safe as the sed delimiter.
    docker logs --tail 30 "$1" 2>&1 \
        | { if [[ -n "${OCTOPUS_LICENSE:-}" ]]; then
                sed "s|${OCTOPUS_LICENSE}|<redacted licence>|g"
            else
                cat
            fi; } \
        | sed 's/^/    | /' || true
    echo "    --- end ---"
}

# wait_for <description> <timeout-seconds> <shell test> - poll until the test
# passes, or give up. Returns non-zero on timeout.
wait_for() {
    local what="$1" timeout="$2" test_cmd="$3"
    local waited=0
    while (( waited < timeout )); do
        if eval "$test_cmd" >/dev/null 2>&1; then
            info "$what ready after ${waited}s"
            return 0
        fi
        sleep 5
        waited=$((waited + 5))
        if (( waited % 60 == 0 )); then
            info "still waiting for $what (${waited}s/${timeout}s)"
        fi
    done
    return 1
}

# e2e_rm_temp - remove the helper script and every env file holding a secret.
# Runs from the EXIT trap, possibly before e2e_prepare has created any of them,
# so it must tolerate every one being unset.
e2e_rm_temp() {
    rm -f "${E2E_HELPER:-}" \
          "${E2E_ENV_SQL:-}" "${E2E_ENV_SERVER:-}" \
          "${E2E_ENV_AGENT:-}" "${E2E_ENV_API:-}" 2>/dev/null || true
}

# write_api_helper - write the Octopus API helper this stage shells out to.
#
# Octopus authenticates API calls with either an API key or a session cookie.
# Minting an API key itself needs an authenticated POST, so signing in and
# reusing the session is simpler. This runs inside a curl container on the e2e
# network; it is written to a file and mounted rather than passed with `sh -c`
# so the quoting stays readable.
#
# /tmp is one of Docker Desktop's default shared paths, so the mount works on
# macOS as well as Linux. BSD (macOS) mktemp only substitutes Xs at the very
# end of the template.
E2E_HELPER=""

write_api_helper() {
    E2E_HELPER=$(mktemp /tmp/octopus-api.XXXXXX)
    cat > "$E2E_HELPER" <<'HELPER_SH'
#!/bin/sh
# usage: octopus-api.sh <server-url> <GET|POST> <path> [body]
# Credentials come from OCTO_USER / OCTO_PASS in the environment, supplied by
# --env-file, so they never appear in the container's argv.
set -e
SERVER="$1"; METHOD="$2"; API_PATH="$3"; BODY="${4:-}"
: "${OCTO_USER:?OCTO_USER not set}" "${OCTO_PASS:?OCTO_PASS not set}"

HEADERS=$(curl -sf -D - -o /dev/null -H 'Content-Type: application/json' \
    -d "{\"Username\":\"${OCTO_USER}\",\"Password\":\"${OCTO_PASS}\"}" \
    "${SERVER}/api/users/login")

# curl will not replay cookies from its jar for a single-label host such as a
# Docker container name, so build the Cookie header from the login response
# instead of relying on -c/-b.
COOKIE=$(printf '%s' "$HEADERS" \
    | grep -i '^set-cookie:' \
    | sed 's/^[Ss]et-[Cc]ookie: //' \
    | cut -d';' -f1 | tr -d '\r' | tr '\n' ';')

if [ "$METHOD" = "GET" ]; then
    curl -sf -H "Cookie: ${COOKIE}" "${SERVER}${API_PATH}"
else
    # The anti-forgery header has to carry the token exactly as the Set-Cookie
    # delivered it, i.e. still URL-encoded. URL-decoding it earns a 403.
    CSRF=$(printf '%s' "$HEADERS" \
        | grep -io 'Octopus-Csrf-Token_[^;]*' | head -1 | cut -d= -f2- | tr -d '\r')
    curl -sf -X POST \
        -H "Cookie: ${COOKIE}" \
        -H 'Content-Type: application/json' \
        -H "X-Octopus-Csrf-Token: ${CSRF}" \
        -d "$BODY" "${SERVER}${API_PATH}"
fi
HELPER_SH
}

# Every secret reaches a container through --env-file rather than `-e`, so
# none of them appear in `ps` output for the life of the container. Docker
# takes each value literally - no quoting is applied or honoured - which
# suits the connection string's ';' and '=' characters.
#
# One file per consumer, so no container is handed a credential it has no
# use for. All are 0600 and removed by the EXIT trap.
new_env_file() {
    local f
    f=$(mktemp /tmp/octopus-e2e-env.XXXXXX)
    chmod 600 "$f"
    printf '%s\n' "$@" > "$f"
    printf '%s' "$f"
}

E2E_ENV_SQL=""
E2E_ENV_SERVER=""
E2E_ENV_AGENT=""
E2E_ENV_API=""

# e2e_prepare - mint this run's credentials and write every temp file stage 4
# needs. Called from stage 4 only, so --skip-e2e neither generates passwords
# nor prompts 1Password.
e2e_prepare() {
    # Fresh for every run, and never written anywhere but the containers they
    # are created for, which are torn down at the end.
    SA_PASSWORD=$(random_password)
    OCTOPUS_PASSWORD=$(random_password)

    resolve_license
    write_api_helper

    E2E_ENV_SQL=$(new_env_file \
        "MSSQL_SA_PASSWORD=${SA_PASSWORD}")

    # The licence line is omitted entirely when we have no licence, rather than
    # written as an empty value, so the server image sees it as unset.
    local server_env=(
        "ADMIN_PASSWORD=${OCTOPUS_PASSWORD}"
        "DB_CONNECTION_STRING=Server=${E2E_SQL},1433;Initial Catalog=Octopus;Persist Security Info=False;User ID=sa;Password=${SA_PASSWORD};MultipleActiveResultSets=False;Connection Timeout=30;TrustServerCertificate=True"
    )
    [[ -n "$OCTOPUS_LICENSE" ]] && server_env+=("OCTOPUS_SERVER_BASE64_LICENSE=${OCTOPUS_LICENSE}")
    E2E_ENV_SERVER=$(new_env_file "${server_env[@]}")

    E2E_ENV_AGENT=$(new_env_file \
        "ServerUsername=${OCTOPUS_USER}" \
        "ServerPassword=${OCTOPUS_PASSWORD}")

    E2E_ENV_API=$(new_env_file \
        "OCTO_USER=${OCTOPUS_USER}" \
        "OCTO_PASS=${OCTOPUS_PASSWORD}")
}

octopus_call() {
    docker run --rm --platform "$PLATFORM" --network "$E2E_NET" \
        --env-file "$E2E_ENV_API" \
        -v "${E2E_HELPER}:/octopus-api.sh:ro" \
        --entrypoint sh curlimages/curl:latest \
        /octopus-api.sh "http://${E2E_SERVER}:8080" "$@"
}

# wait_for_log <container> <timeout-seconds> <pattern> - poll a container's log
# until the pattern shows up. Fails fast if the container has already exited
# without ever printing it, rather than burning the whole timeout.
#
# Note: the log is captured into a variable rather than piped into `grep -q`.
# `grep -q` exits on the first match, and under `set -o pipefail` the SIGPIPE
# that gives the producer makes the whole pipeline report failure even though
# the pattern matched.
wait_for_log() {
    local container="$1" timeout="$2" pattern="$3"
    local waited=0 logs
    while (( waited < timeout )); do
        logs=$(docker logs "$container" 2>&1 || true)
        [[ "$logs" == *"$pattern"* ]] && return 0
        if [[ "$(docker inspect -f '{{.State.Status}}' "$container" 2>/dev/null)" == "exited" ]]; then
            logs=$(docker logs "$container" 2>&1 || true)
            [[ "$logs" == *"$pattern"* ]] && return 0
            return 1
        fi
        sleep 5
        waited=$((waited + 5))
    done
    return 1
}

# octopus_api <path> - GET an Octopus API path as the admin user.
octopus_api() { octopus_call GET "$1"; }

# octopus_post <path> <json-body> - POST to the Octopus API as the admin user.
octopus_post() { octopus_call POST "$1" "$2"; }

# sql_ready - can we log in and run a query yet? The password is read from
# the container's own environment, so it is not on the `docker exec` argv
# either.
sql_ready() {
    docker exec "$E2E_SQL" bash -c \
        '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "select 1"' \
        >/dev/null 2>&1
}

# machines_json - /api/machines/all with all whitespace stripped, so the
# assertions below do not depend on how the server chooses to format its JSON.
# None of the values we match on contain spaces.
machines_json() {
    octopus_api /api/machines/all | tr -d ' \n\r\t'
}

both_registered() {
    local json
    json=$(machines_json) || return 1
    [[ "$json" == *"\"Name\":\"${E2E_LISTENING}\""* && "$json" == *"\"Name\":\"${E2E_POLLING}\""* ]]
}

both_healthy() {
    local json count
    json=$(machines_json) || return 1
    count=$(printf '%s' "$json" | grep -o '"HealthStatus":"\(Healthy\|HasWarnings\)"' | wc -l)
    (( count >= 2 ))
}

if [[ $SKIP_E2E -eq 0 ]]; then
    step "Stage 4/4: end-to-end registration against a real Octopus Server"
    info "Spinning up SQL Server + Octopus Server, then registering a listening"
    info "and a polling Tentacle from the image just built."

    # Trap first, so anything e2e_prepare manages to create is still cleaned up
    # if it fails partway.
    trap 'e2e_teardown; e2e_rm_temp' EXIT
    e2e_prepare
    e2e_teardown   # clear out anything left over from a previous run

    # Guard against the helper going missing: Docker would silently mount an
    # empty directory over it and every API call would fail for no clear reason.
    [[ -s "$E2E_HELPER" ]] || die "internal error: Octopus API helper script missing at $E2E_HELPER"

    docker network create "$E2E_NET" >/dev/null

    # --- SQL Server -------------------------------------------------------
    info "Starting SQL Server..."
    docker run -d --name "$E2E_SQL" --platform "$PLATFORM" --network "$E2E_NET" \
        --env-file "$E2E_ENV_SQL" \
        -e ACCEPT_EULA=Y \
        -e MSSQL_PID=Express \
        mcr.microsoft.com/mssql/server:2022-latest >/dev/null

    if wait_for "SQL Server" 420 sql_ready; then
        pass "SQL Server started"
    else
        fail "SQL Server started"
        dump_logs "$E2E_SQL"
        die "cannot run the e2e test without a database"
    fi

    # --- Octopus Server ---------------------------------------------------
    info "Starting Octopus Server (this takes several minutes on first run)..."

    docker run -d --name "$E2E_SERVER" --platform "$PLATFORM" --network "$E2E_NET" \
        --env-file "$E2E_ENV_SERVER" \
        -e ACCEPT_EULA=Y \
        -e "ADMIN_USERNAME=${OCTOPUS_USER}" \
        docker.packages.octopushq.com/octopusdeploy/octopusdeploy:latest >/dev/null

    if wait_for "Octopus Server" 900 \
        "docker run --rm --platform $PLATFORM --network $E2E_NET curlimages/curl:latest -sf http://${E2E_SERVER}:8080/api"; then
        pass "Octopus Server started"
    else
        fail "Octopus Server started"
        dump_logs "$E2E_SERVER"
        die "cannot run the e2e test without an Octopus Server"
    fi

    # --- target environment ----------------------------------------------
    # A fresh Octopus Server has no environments, and `tentacle register-with`
    # will not create one: it fails with "Could not find the environment ...".
    # Roles, by contrast, are free-form and are created on registration.
    info "Creating the Development environment..."
    octopus_post /api/environments '{"Name":"Development"}' >/dev/null 2>&1 || true
    ENVIRONMENTS=$(octopus_api /api/environments/all | tr -d ' \n\r\t')
    if [[ "$ENVIRONMENTS" == *'"Name":"Development"'* ]]; then
        pass "Development environment exists"
    else
        fail "Development environment exists" "the Tentacles will not be able to register"
    fi

    # --- Tentacles --------------------------------------------------------
    # DISABLE_DIND=Y because docker-in-docker needs --privileged, which is not
    # reliable under cross-architecture emulation. The dind payload itself is
    # covered by the smoke tests above.
    # --hostname matters for the listening Tentacle: it registers itself under
    # PublicHostNameConfiguration=ComputerName, i.e. whatever `hostname` returns,
    # and the server has to be able to resolve that name to call back to it.
    info "Starting the listening Tentacle..."
    docker run -d --name "$E2E_LISTENING" --hostname "$E2E_LISTENING" --platform "$PLATFORM" --network "$E2E_NET" \
        --env-file "$E2E_ENV_AGENT" \
        -e ACCEPT_EULA=Y \
        -e DISABLE_DIND=Y \
        -e "ServerUrl=http://${E2E_SERVER}:8080" \
        -e "TargetEnvironment=Development" \
        -e "TargetRole=app-server" \
        -e "TargetName=${E2E_LISTENING}" \
        "$IMAGE" >/dev/null

    info "Starting the polling Tentacle..."
    docker run -d --name "$E2E_POLLING" --hostname "$E2E_POLLING" --platform "$PLATFORM" --network "$E2E_NET" \
        --env-file "$E2E_ENV_AGENT" \
        -e ACCEPT_EULA=Y \
        -e DISABLE_DIND=Y \
        -e "ServerUrl=http://${E2E_SERVER}:8080" \
        -e "ServerPort=10943" \
        -e "TargetEnvironment=Development" \
        -e "TargetRole=web-server" \
        -e "TargetName=${E2E_POLLING}" \
        "$IMAGE" >/dev/null

    if [[ -z "$OCTOPUS_LICENSE" ]]; then
        # An unlicensed Octopus Server reports a Targets limit of 0, so
        # `tentacle register-with` is refused before a machine is ever created.
        # Everything up to that point still exercises the image for real, so
        # assert on that instead of pretending the stage passed.
        warn "No licence available, so no Tentacle can register (0-target limit)."
        warn "Verifying configuration and server connectivity instead. Sign in to"
        warn "1Password ('op signin') or set OCTOPUS_SERVER_BASE64_LICENSE to run"
        warn "the full registration test."

        # Self-configuration: create-instance, set paths, comms mode, certificate.
        if wait_for_log "$E2E_LISTENING" 300 "A new certificate has been generated"; then
            pass "listening Tentacle configured itself and generated a certificate"
        else
            fail "listening Tentacle configured itself and generated a certificate"
            dump_logs "$E2E_LISTENING"
        fi

        # Reaching this line means the Tentacle resolved the server, opened an HTTP
        # connection and authenticated with the username/password it was given.
        if wait_for_log "$E2E_LISTENING" 300 "Registering the tentacle with the server at"; then
            pass "listening Tentacle reached and authenticated against the server"
        else
            fail "listening Tentacle reached and authenticated against the server"
            dump_logs "$E2E_LISTENING"
        fi

        # A licence refusal (rather than an auth or connection error) proves the
        # whole request round-tripped and the only thing left is the licence.
        if wait_for_log "$E2E_LISTENING" 300 "exceed the limits of your current license"; then
            pass "listening Tentacle blocked only by the licence target limit"
        else
            fail "listening Tentacle blocked only by the licence target limit"
            dump_logs "$E2E_LISTENING"
        fi

        # The polling Tentacle additionally opens a raw Halibut connection to the
        # server communications port, which the listening one never does.
        if wait_for_log "$E2E_POLLING" 300 "Connected successfully"; then
            pass "polling Tentacle connected to the server comms port (10943)"
        else
            fail "polling Tentacle connected to the server comms port (10943)"
            dump_logs "$E2E_POLLING"
        fi

    else
        # Registration is the first thing the entrypoint does, so both machines
        # should appear in the API well before the health check runs.
        if wait_for "Tentacle registration" 420 both_registered; then
            pass "both Tentacles registered with the Octopus Server"
        else
            fail "both Tentacles registered with the Octopus Server"
            dump_logs "$E2E_LISTENING"
            dump_logs "$E2E_POLLING"
        fi

        MACHINES=$(machines_json)

        # Assert on each machine individually so a half-working image is obvious.
        for name in "$E2E_LISTENING" "$E2E_POLLING"; do
            if [[ "$MACHINES" == *"\"Name\":\"$name\""* ]]; then
                pass "registered: $name"
            else
                fail "registered: $name"
                dump_logs "$name"
            fi
        done

        assert_contains "listening Tentacle registered as TentaclePassive" "$MACHINES" "TentaclePassive"
        assert_contains "polling Tentacle registered as TentacleActive"    "$MACHINES" "TentacleActive"

        # --- health -----------------------------------------------------------
        # A machine only reports Healthy once the server has completed a health
        # check against it, which exercises the Halibut connection in both
        # directions - the real proof the image works.
        info "Waiting for the Octopus Server to health-check both Tentacles..."
        if wait_for "Tentacle health checks" 600 both_healthy; then
            pass "both Tentacles reported Healthy"
        else
            fail "both Tentacles reported Healthy" \
                 "statuses: $(machines_json | grep -o '"HealthStatus":"[A-Za-z]*"' | tr '\n' ' ')"
            dump_logs "$E2E_LISTENING"
            dump_logs "$E2E_POLLING"
        fi

        # The version the server sees comes off the wire from the Tentacle itself,
        # so this confirms the .deb inside the image is the one we just built.
        MACHINES=$(machines_json)
        # EXPECTED_VERSION is the full FullSemVer (timestamp suffix stripped),
        # not just the numeric prefix, so this cannot pass on a coincidental
        # substring match elsewhere in the JSON.
        assert_contains "server reports the built Tentacle version ($EXPECTED_VERSION)" \
            "$MACHINES" "$EXPECTED_VERSION"
    fi
fi

###############################################################################
# Summary
###############################################################################

step "Summary"
info "Image:        $IMAGE"
info "Package:      $DEB_PATH"
info "Tests run:    $TESTS_RUN"

if [[ $TESTS_FAILED -gt 0 ]]; then
    info "Tests failed: ${C_RED}${TESTS_FAILED}${C_RESET}"
    for n in "${FAILED_NAMES[@]}"; do echo "      - $n"; done
    echo
    echo "${C_RED}${C_BOLD}FAILED${C_RESET}"
    exit 1
fi

echo
echo "${C_GREEN}${C_BOLD}ALL $TESTS_RUN TESTS PASSED${C_RESET}"
