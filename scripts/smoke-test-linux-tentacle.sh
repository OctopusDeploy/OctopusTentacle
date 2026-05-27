#!/usr/bin/env bash
#
# End-to-end smoke test for the Linux Tentacle Docker image (EFT-3311).
#
# Builds the image from the .deb in _artifacts/deb, brings up a self-contained
# Octopus Server + MSSQL stack via docker compose, registers the Tentacle as a
# worker, runs a hello-world AdHocScript on it, and asserts success.
#
# Required tools: docker, curl, jq, openssl.
# Required state: a built .deb in _artifacts/deb/tentacle_*_amd64.deb.
#
# License source: set $OCTOPUS_LICENSE_BASE64 to a base64-encoded Octopus license
# to skip the 1Password lookup (this is the path CI runners should use). When
# the env var is unset, the script falls back to `op read` against 1Password
# for local-dev use, in which case `op` must be installed and signed in.

set -euo pipefail

TENTACLE_REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="$TENTACLE_REPO/scripts/smoke-test-linux-tentacle.compose.yml"

API="http://localhost:8065/api"
# Ephemeral credentials — the Server DB is recreated from scratch on every run
# (compose down -v in teardown), so a fixed sentinel is safe and keeps the
# Authorization header below trivial. The SA password must satisfy SQL Server's
# complexity policy (upper + lower + digit + special); openssl supplies entropy
# for the digits/lowercase portion.
ADMIN_API_KEY="API-SMOKETEST0000000000000"
ADMIN_PASSWORD="Smoke-$(openssl rand -hex 16)!"
SA_PASSWORD="Sa$(openssl rand -hex 12)!"
H="X-Octopus-ApiKey: $ADMIN_API_KEY"
IMAGE_TAG="smoke-debian12"
ONEPASSWORD_LICENSE_REF="op://software licencing/octopus deploy ultimate license key base64/value"

# Per-run worker name. Mostly cosmetic since the DB is fresh every run, but it
# makes container logs easier to trace and lets teardown deregister by ID.
WORKER_TARGET_NAME="smoke-tentacle-$(date +%Y%m%d-%H%M%S)-$$"
WORKER_ID=""

TENTACLE_TAG="$IMAGE_TAG"
OCTOPUS_SERVER_TAG="${OCTOPUS_SERVER_TAG:-latest}"

log()  { printf '\033[1;34m[smoke]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[smoke]\033[0m %s\n' "$*" >&2; }
die()  { printf '\033[1;31m[smoke]\033[0m %s\n' "$*" >&2; exit 1; }

require() { command -v "$1" >/dev/null || die "Missing required tool: $1"; }
require docker
require curl
require jq
require openssl
[[ -n "${OCTOPUS_LICENSE_BASE64:-}" ]] || require op

compose() { docker compose -f "$COMPOSE_FILE" "$@"; }

teardown() {
  local exit_code=$?
  log "--- teardown ---"
  if [[ -n "$WORKER_ID" ]]; then
    log "Deregistering worker $WORKER_ID"
    curl -fsS -X DELETE -H "$H" "$API/workers/$WORKER_ID" >/dev/null 2>&1 || true
  fi
  compose down -v 2>/dev/null || true
  exit "$exit_code"
}
trap teardown EXIT

###############################################################################
# Step 1: Resolve license
###############################################################################
log "--- Step 1: resolve license ---"
if [[ -n "${OCTOPUS_LICENSE_BASE64:-}" ]]; then
  LICENSE_BASE64="$OCTOPUS_LICENSE_BASE64"
  log "Using license from \$OCTOPUS_LICENSE_BASE64 (${#LICENSE_BASE64} bytes)"
else
  if ! op account list >/dev/null 2>&1; then
    die "1Password CLI is not signed in. Run: eval \$(op signin) — or pre-set \$OCTOPUS_LICENSE_BASE64."
  fi
  LICENSE_BASE64="$(op read "$ONEPASSWORD_LICENSE_REF" 2>/dev/null || true)"
  [[ -n "$LICENSE_BASE64" ]] || die "Could not read license from 1Password at: $ONEPASSWORD_LICENSE_REF"
  log "Fetched license from 1Password (${#LICENSE_BASE64} bytes)"
fi

###############################################################################
# Step 2: Build the Linux Tentacle image from the local .deb
###############################################################################
log "--- Step 2: build Tentacle image ---"
cd "$TENTACLE_REPO"

shopt -s nullglob
DEBS=(_artifacts/deb/tentacle_*_amd64.deb)
shopt -u nullglob
[[ ${#DEBS[@]} -ge 1 ]] || die "No .deb found in _artifacts/deb/. Build it first."
[[ ${#DEBS[@]} -eq 1 ]] || die "Multiple .debs in _artifacts/deb/; expected one: ${DEBS[*]}"
DEB_FILE="${DEBS[0]}"
DEB_BASENAME="$(basename "$DEB_FILE")"
BUILD_NUMBER="${DEB_BASENAME#tentacle_}"
BUILD_NUMBER="${BUILD_NUMBER%_amd64.deb}"
export BUILD_NUMBER
export BUILD_DATE="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

log "BUILD_NUMBER=$BUILD_NUMBER"
# Use `docker build` directly rather than `docker compose -f docker-compose.build.yml`
# because that compose file also defines kubernetes/windows tentacle services which
# require extra env vars (BUILD_ARCH, BUILD_VARIANT) we don't care about here.
DST_IMAGE="octopusdeploy/tentacle:${IMAGE_TAG}"
docker build \
  --platform linux/amd64 \
  --build-arg BUILD_NUMBER="$BUILD_NUMBER" \
  --build-arg BUILD_DATE="$BUILD_DATE" \
  -f docker/linux/Dockerfile \
  -t "$DST_IMAGE" \
  .
log "Built $DST_IMAGE"

###############################################################################
# Step 3: Bring up MSSQL + Octopus Server and wait for /api to respond
###############################################################################
log "--- Step 3: start mssql and octopus-server ---"
# Export every var the compose file interpolates. octopus-server depends on
# mssql with condition: service_healthy, so compose will block until MSSQL is
# accepting queries before starting the Server.
export TENTACLE_TAG OCTOPUS_SERVER_TAG SA_PASSWORD ADMIN_PASSWORD ADMIN_API_KEY \
  WORKER_TARGET_NAME
export OCTOPUS_SERVER_BASE64_LICENSE="$LICENSE_BASE64"

compose up -d mssql octopus-server

log "Waiting for $API/octopusservernodes/ping ..."
for i in {1..300}; do
  if curl -fsS -H "$H" "$API/octopusservernodes/ping" >/dev/null 2>&1; then
    log "Server is up after ${i}s"
    break
  fi
  if [[ $i -eq 300 ]]; then
    warn "Server did not become ready in 300s. Recent logs:"
    compose logs --no-color --tail=120 octopus-server mssql || true
    die "Octopus Server did not become ready"
  fi
  sleep 1
done

###############################################################################
# Step 4: Bring up the Tentacle (Worker, polling mode, DIND disabled)
###############################################################################
log "--- Step 4: start tentacle ---"
# --no-deps because octopus-server has no compose-level healthcheck; we already
# polled its API ping above and know it's ready.
compose up -d --no-deps tentacle

log "Waiting for Tentacle 'Configuration successful.' in logs ..."
for i in {1..60}; do
  if compose logs --no-color tentacle 2>/dev/null | grep -qF "Configuration successful."; then
    log "Tentacle registered after ${i}s"
    break
  fi
  [[ $i -eq 60 ]] && die "Tentacle did not register in 60s. Logs:
$(compose logs --no-color --tail=80 tentacle)"
  sleep 1
done

# Make sure the agent is still running (the wrapper script can exit shortly
# after registration if a sidecar like dockerd dies).
if ! compose ps --status running --services 2>/dev/null | grep -qx tentacle; then
  die "Tentacle container exited shortly after registration. Logs:
$(compose logs --no-color --tail=80 tentacle)"
fi

###############################################################################
# Step 5: Verify worker is registered & run hello-world AdHocScript
###############################################################################
log "--- Step 5: verify registration and run hello-world ---"

# Find the worker we just registered by its per-run TargetName.
for i in {1..60}; do
  WORKERS_JSON="$(curl -fsS -H "$H" --data-urlencode "name=$WORKER_TARGET_NAME" -G "$API/workers" 2>/dev/null || echo '{"Items":[]}')"
  WORKER_ID="$(echo "$WORKERS_JSON" \
    | jq -r --arg name "$WORKER_TARGET_NAME" '.Items[] | select(.Name == $name) | .Id' \
    | head -n1)"
  [[ -n "$WORKER_ID" ]] && break
  sleep 1
done
if [[ -z "$WORKER_ID" ]]; then
  warn "No worker named '$WORKER_TARGET_NAME' appeared. Diagnostic dump of $API/workers:"
  curl -fsS -H "$H" "$API/workers" || true
  warn "Tentacle container logs (tail 80):"
  compose logs --no-color --tail=80 tentacle || true
  die "Worker '$WORKER_TARGET_NAME' did not appear after 60s"
fi
log "Registered worker: $WORKER_ID  (name='$WORKER_TARGET_NAME')"

ADHOC_BODY="$(jq -nc \
  --arg id "$WORKER_ID" \
  '{
    Name: "AdHocScript",
    Description: "EFT-3311 Debian 12 smoke test",
    Arguments: {
      ScriptBody: "echo Hello from $(hostname); cat /etc/os-release | head -2",
      Syntax: "Bash",
      WorkerIds: [$id]
    }
  }')"

TASK_RESP="$(curl -fsS -X POST -H "$H" -H "Content-Type: application/json" \
  "$API/tasks" -d "$ADHOC_BODY")"
TASK_ID="$(echo "$TASK_RESP" | jq -r '.Id')"
[[ -n "$TASK_ID" && "$TASK_ID" != "null" ]] || die "Could not submit AdHocScript task. Response: $TASK_RESP"
log "Submitted task: $TASK_ID"

STATE=""
for i in {1..120}; do
  STATE="$(curl -fsS -H "$H" "$API/tasks/$TASK_ID" | jq -r '.State')"
  echo "  task=$TASK_ID state=$STATE"
  case "$STATE" in
    Success|Failed|Canceled|TimedOut) break ;;
  esac
  sleep 2
done

log "--- Task log ---"
curl -fsS -H "$H" "$API/tasks/$TASK_ID/raw" || true
log "--- end task log ---"

if [[ "$STATE" != "Success" ]]; then
  die "Task finished in state '$STATE' (expected Success)"
fi

# Load-bearing assertion: the whole point of this smoke test is to prove the
# Debian 12 base image is what's actually running on the Tentacle, so a missing
# os-release line is a hard failure, not a warning.
if ! curl -fsS -H "$H" "$API/tasks/$TASK_ID/raw" | grep -qF 'Debian GNU/Linux 12'; then
  die "Task succeeded but the log does NOT mention 'Debian GNU/Linux 12'. Inspect output above."
fi

log "PASS — Tentacle (Debian 12) registered and executed hello-world."
