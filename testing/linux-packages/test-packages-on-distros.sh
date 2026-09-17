#!/usr/bin/env bash
#
# test-packages-on-distros.sh
#
# Runs NUKE's TestLinuxPackages target locally and reports what actually
# happened, which the target itself does not.
#
# TestLinuxPackages installs the built .deb/.rpm on every distribution in the
# matrix in build/Build.Tests.cs and checks the installed Tentacle reports the
# expected version. Running it by hand needs three things set up first, and its
# result needs reading carefully:
#
#   1. Artifacts. The target globs _artifacts/{deb,rpm} for the linux-x64
#      package and calls .Single(), so it throws on zero packages AND on more
#      than one of that architecture. Use --pack to build them, or leave
#      existing ones in place.
#
#   2. Platform. Every image in the matrix is amd64-only and the target sets no
#      platform of its own, so on Apple Silicon Docker would default to arm64
#      and the install would fail. DOCKER_DEFAULT_PLATFORM is pinned below.
#
#   3. Secrets. NUKE resolves every [ParameterFromPasswordStore] at start-up by
#      shelling out to the 1Password CLI, which hangs when it cannot prompt.
#      Only the SBOM target needs those, so this opts out.
#
# The result needs interpreting because Logging.InTest (build/Utilities/
# Logging.cs) catches every per-distro exception, logs it and carries on. On
# TeamCity that still fails the build, via the ##teamcity[testFailed] service
# message it writes. Locally there is no TeamCity instance, so a distro whose
# image has been retagged or removed upstream is reported only in the "Errors &
# Warnings" block at the very bottom - the target still says "Succeeded" and
# build.sh still exits 0. This script reads the log back, reports every distro
# individually, and exits non-zero if any of them did not pass.
#
# Usage:
#   ./testing/linux-packages/test-packages-on-distros.sh [options]
#        (runs from anywhere; it resolves the repo root itself)
#   --pack           Build the .deb/.rpm first, via NUKE PackDebianPackage
#   --distro IMAGE   Test a single distribution directly, skipping NUKE
#                    entirely, e.g. --distro ubuntu:24.04
#   --keep-log       Keep the raw NUKE output instead of deleting it on success
#   -h, --help       Show this help
#
# Note: --pack runs the real NUKE build, which stamps the calculated version
# into installer/Octopus.Tentacle.Installer/Product.wxs and regenerates
# .nuke/build.schema.json. Both are tracked, so expect them to show up as
# modified afterwards.
#
set -euo pipefail

# Every path below (./build.sh, _artifacts, build/Build.Tests.cs) is relative to
# the repo root, so resolve that and work from there.
REPO_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
cd "$REPO_DIR"
[[ -f "$REPO_DIR/build.sh" ]] \
    || { echo "ERROR: could not locate the repo root (got '$REPO_DIR')" >&2; exit 1; }

PACK=0
KEEP_LOG=0
SINGLE_DISTRO=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --pack) PACK=1 ;;
        --keep-log) KEEP_LOG=1 ;;
        --distro)
            [[ $# -ge 2 ]] || { echo "--distro needs an image, e.g. --distro ubuntu:24.04" >&2; exit 2; }
            SINGLE_DISTRO="$2"; shift ;;
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

step()  { echo; echo "${C_BOLD}==> $*${C_RESET}"; }
info()  { echo "    $*"; }
warn()  { echo "${C_YELLOW}    WARN: $*${C_RESET}"; }
die()   { echo "${C_RED}ERROR: $*${C_RESET}" >&2; exit 1; }

###############################################################################
# Environment
###############################################################################

# The packages are amd64, so pin every pull and run; on Apple Silicon this is
# what stops Docker reaching for an arm64 image the .deb cannot install into.
export DOCKER_DEFAULT_PLATFORM="linux/amd64"
# Suppress Docker Scout hints, which go to stderr and read like errors.
export DOCKER_CLI_HINTS=false
# Keep NUKE's start-up away from the 1Password CLI. See the header.
export OCTOPUS__Tests__SecretManagerEnabled=False

command -v docker >/dev/null || die "docker is not on PATH"
docker info >/dev/null 2>&1 || die "the Docker daemon is not running"

if [[ "$(uname -m)" == "arm64" || "$(uname -m)" == "aarch64" ]]; then
    info "Host is $(uname -m); pulling and running everything as linux/amd64 under emulation."
fi

###############################################################################
# 1. Packages
###############################################################################

# find_one <deb|rpm> - the sole linux-x64 package of that type, or empty.
#
# The globs are the target's own: RunLinuxPackageTestsFor (build/Build.Tests.cs)
# narrows to "*_amd64.deb" / "*.x86_64.rpm" and only then calls .Single().
# Matching that matters, because an unqualified PackLinux leaves arm64 and armhf
# packages alongside the amd64 one - a directory the target is perfectly happy
# with, and an unqualified `*.deb` here would reject.
#
# Deliberately not `ls | head -1`: two packages of the one architecture is what
# makes the target's .Single() throw, so that has to be reported here rather
# than papered over with a pick.
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
    info "PackDebianPackage cross-compiles Tentacle and produces both the .deb"
    info "and the .rpm inside the tool-linux-packages container. Takes a few minutes."

    # Stale linux-x64 packages would break the target's .Single(), and this is
    # about to replace them anyway. Other architectures are left alone: neither
    # the target nor find_one looks at them.
    rm -f _artifacts/deb/*_amd64.deb _artifacts/rpm/*.x86_64.rpm 2>/dev/null || true

    # Only linux-x64 is in the matrix; building every runtime ID would take far
    # longer for no benefit.
    ./build.sh --target PackDebianPackage --runtime-ids linux-x64
fi

DEB_PATH=$(find_one deb) || die "expected exactly one _artifacts/deb/*_amd64.deb (the target calls .Single()). Run with --pack, or clear out the extras."
RPM_PATH=$(find_one rpm) || die "expected exactly one _artifacts/rpm/*.x86_64.rpm (the target calls .Single()). Run with --pack, or clear out the extras."

info "Package (deb): $DEB_PATH"
info "Package (rpm): $RPM_PATH"

###############################################################################
# 2a. Single distribution, bypassing NUKE
###############################################################################

# This reproduces exactly what RunLinuxPackageTestsFor does for one row of the
# matrix: same registry prefix, same environment, same two mounts, same script.
# Useful for iterating on one distribution without waiting out all of them.
#
# BUILD_NUMBER is set to the package's own version rather than NUKE's
# FullSemVer. test-linux-package.sh normalises both sides before comparing, so
# either spelling works; this one needs no NUKE invocation to discover.
if [[ -n "$SINGLE_DISTRO" ]]; then
    step "Testing a single distribution: $SINGLE_DISTRO"

    IMAGE="docker.packages.octopushq.com/$SINGLE_DISTRO"

    # Pulled explicitly so an image that does not resolve is reported as such,
    # rather than as a failure of the probe below.
    docker pull "$IMAGE" >/dev/null 2>&1 || die "could not pull $IMAGE"

    # Which package to install is decided by probing the image for apt-get,
    # exactly as install-package.sh decides which installer to run. Roughly half
    # the matrix is rpm-based (Amazon Linux, RedHat), and handing one of those
    # the .deb makes `yum localinstall` fail in a way that reads like a
    # packaging bug rather than this wrapper picking the wrong artifact.
    #
    # Probed rather than matched against a list of distribution names, so it
    # cannot drift out of step with install-package.sh or with the matrix.
    if docker run --rm --entrypoint sh "$IMAGE" -c 'command -v apt-get' >/dev/null 2>&1; then
        PACKAGE_PATH="/artifacts/deb/$(basename "$DEB_PATH")"
    else
        PACKAGE_PATH="/artifacts/rpm/$(basename "$RPM_PATH")"
    fi

    # The version always comes off the .deb, whichever package is installed:
    # the .rpm's filename has had every '-' replaced with '_', so it no longer
    # spells the version Tentacle reports. Both come from the same build.
    DEB_FILE=$(basename "$DEB_PATH")
    VERSION="${DEB_FILE#tentacle_}"
    VERSION="${VERSION%_amd64.deb}"

    info "Package:      $PACKAGE_PATH"
    info "BUILD_NUMBER: $VERSION"

    if docker run --rm \
            -e "VERSION=$VERSION" \
            -e "BUILD_NUMBER=$VERSION" \
            -e INPUT_PATH=/input \
            -e OUTPUT_PATH=/output \
            -v "$REPO_DIR/linux-packages/test-scripts:/test-scripts:ro" \
            -v "$REPO_DIR/_artifacts:/artifacts:ro" \
            "$IMAGE" \
            bash /test-scripts/test-linux-package.sh "$PACKAGE_PATH"; then
        echo
        echo "${C_GREEN}${C_BOLD}PASSED: $SINGLE_DISTRO${C_RESET}"
        exit 0
    else
        echo
        echo "${C_RED}${C_BOLD}FAILED: $SINGLE_DISTRO${C_RESET}"
        exit 1
    fi
fi

###############################################################################
# 2b. The whole matrix, via NUKE
###############################################################################

step "Running the TestLinuxPackages target"
info "Distributions come from the matrix in build/Build.Tests.cs."
info "Each is pulled and the package installed into it; this takes a few minutes."

# TMPDIR conventionally carries a trailing slash on macOS, which would show up
# as '//' in the path printed at the end.
TMP_BASE="${TMPDIR:-/tmp}"
LOG=$(mktemp "${TMP_BASE%/}/test-linux-packages.XXXXXX")

# The log is the only record of which distributions passed, so it is kept when
# anything goes wrong and when asked. Run from a trap rather than at each exit
# point, so an unexpected failure cannot leak the temp file either.
cleanup_log() {
    if [[ $KEEP_LOG -eq 1 ]]; then
        info "Raw NUKE output: $LOG"
    else
        rm -f "$LOG" 2>/dev/null || true
    fi
}
trap cleanup_log EXIT

# Deliberately not `set -e`-fatal: build.sh exits 0 even when distributions
# failed, and it could equally exit non-zero before running any of them. Both
# cases are diagnosed from the log below, so capture the code and carry on.
set +e
./build.sh --target TestLinuxPackages 2>&1 | tee "$LOG"
NUKE_EXIT=${PIPESTATUS[0]}
set -e

###############################################################################
# 3. Report
###############################################################################

step "Results"

# Walk the log and pair each distribution up with its outcome.
#
# Parsing the log rather than build/Build.Tests.cs on purpose: the matrix is C#
# and has already changed shape more than once (a List<T> of explicitly-typed
# entries, then a collection expression of target-typed ones). The log records
# what was actually attempted, in order, whatever the source looks like.
#
# Parsing stops at the "Errors & Warnings" banner, because NUKE repeats the
# failing command lines there and they would otherwise be counted as extra
# attempts.
# awk reads the log itself and strips the ANSI colouring as it goes, rather
# than being fed by `sed`. Piping into it is what the obvious version does, and
# it breaks: the `exit` below closes the pipe, `sed` takes a SIGPIPE, and under
# `set -o pipefail` the whole substitution reports 141 and `set -e` kills the
# script before it can report anything.
RESULTS=$(awk '
    { gsub(/\033\[[0-9;]*m/, "") }

    /Errors & Warnings/ { exit }

    # Each distribution begins with its pull. Matched on the invocation prefix
    # NUKE writes, because a failed command is echoed a second time inside the
    # exception text it logs, which would otherwise count as an extra attempt.
    #
    # Indexed rather than keyed by image name, so the same image appearing in
    # the matrix twice cannot collide.
    /\[INF\] > .*docker pull / {
        n++; image[n] = $NF; status[n] = "FAIL"; reason[n] = ""; cur = n; next
    }

    # Emitted by test-linux-package.sh once every assertion has held. Matched
    # in its logged form, not the `+ echo ...` that set -x traces just above it.
    /\[DBG\] All tests passed\./ { if (cur) status[cur] = "PASS"; next }

    # Keep the most recent plausible cause, to show against a failure.
    /Error response from daemon|ProcessException|No supported package management tools|but expected version was/ {
        if (cur) { line = $0; sub(/^[0-9:]+ \[[A-Z]+\] +/, "", line); reason[cur] = line }
    }

    END { for (i = 1; i <= n; i++) printf "%s\t%s\t%s\n", status[i], image[i], reason[i] }
' "$LOG")

TOTAL=0
FAILED=0

if [[ -n "$RESULTS" ]]; then
    while IFS=$'\t' read -r status image reason; do
        [[ -n "$status" ]] || continue
        TOTAL=$((TOTAL + 1))
        # The registry prefix is the same for every row and just adds noise.
        short="${image#docker.packages.octopushq.com/}"
        if [[ "$status" == "PASS" ]]; then
            echo "    ${C_GREEN}PASS${C_RESET}  $short"
        else
            FAILED=$((FAILED + 1))
            echo "    ${C_RED}FAIL${C_RESET}  $short"
            [[ -n "$reason" ]] && echo "          $reason"
        fi
    done <<< "$RESULTS"
fi

echo
info "Distributions tested: $TOTAL"

if [[ $PACK -eq 1 ]]; then
    info "--pack restamped installer/Octopus.Tentacle.Installer/Product.wxs and"
    info ".nuke/build.schema.json with the calculated version. Both are tracked."
fi

# Every exit below leaves the log behind by raising KEEP_LOG; the EXIT trap is
# what acts on it, so the path out does not matter.

# Nothing ran at all: NUKE fell over before reaching the matrix, or the matrix
# is empty. Either way the log is the only thing that explains it.
if [[ $TOTAL -eq 0 ]]; then
    KEEP_LOG=1
    die "no distributions ran (build.sh exited $NUKE_EXIT); see the log"
fi

if [[ $FAILED -gt 0 || $NUKE_EXIT -ne 0 ]]; then
    KEEP_LOG=1

    # Distributions failing on the version, rather than on the install, mean the
    # packages in _artifacts are older than the version NUKE now calculates -
    # the usual cause is simply that commits have landed since they were built.
    # Worth saying, because a wall of identical failures otherwise looks like
    # something is badly wrong.
    #
    # Triggered on a majority rather than on all of them, so that one unrelated
    # failure - an image that no longer resolves, say - does not suppress the
    # explanation for the other seventeen.
    MISMATCHED=$(grep -c 'but expected version was' <<< "$RESULTS" || true)
    if [[ $MISMATCHED -gt 0 ]] && (( MISMATCHED * 2 > TOTAL )); then
        echo
        warn "$MISMATCHED of $TOTAL failed on the version, not the install."
        warn "The packages in _artifacts predate the version NUKE now calculates."
        warn "Rebuild them and re-run: $(basename "${BASH_SOURCE[0]}") --pack"
    fi

    echo
    [[ $NUKE_EXIT -ne 0 ]] && info "build.sh exited $NUKE_EXIT"
    echo "${C_RED}${C_BOLD}FAILED: $FAILED of $TOTAL distributions${C_RESET}"
    exit 1
fi

echo
echo "${C_GREEN}${C_BOLD}ALL $TOTAL DISTRIBUTIONS PASSED${C_RESET}"
