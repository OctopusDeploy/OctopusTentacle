#!/usr/bin/env bash
#
# End-to-end smoke test for the Linux Tentacle Docker image (EFT-3311).
#
# Builds the image from the .deb in _artifacts/deb, brings up a local Octopus
# Server in the sibling OctopusDeploy repo, registers the Tentacle as a worker,
# runs a hello-world AdHocScript on it, and asserts success.
#
# Required tools: docker, op (signed in), curl, jq.
# Required state: a built .deb in ../_artifacts/deb/tentacle_*_amd64.deb and the
# OctopusDeploy repo checked out alongside OctopusTentacle.
#
# Note on $API_KEY below: "API-APIKEY01" is the well-known dev sentinel API key
# provisioned by the sibling OctopusDeploy repo's docker-compose stack for its
# local-only Server instance. It is not a real secret and is safe to commit.

set -euo pipefail

TENTACLE_REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER_REPO="${SERVER_REPO:-$(cd "$TENTACLE_REPO/../OctopusDeploy" && pwd)}"
ENV_FILE="$SERVER_REPO/.env"
# .env backup path is assigned via mktemp in Step 2 (after we know $ENV_FILE
# exists). Using a unique per-run path avoids clobbering a stale backup from
# a previously-crashed run.
ENV_BACKUP=""
# Transient compose override: disables Docker-in-Docker on the Tentacle. The
# default tentacle entrypoint launches a dockerd daemon, which requires the
# container to run with `--privileged`; without that the daemon fails and its
# wrapper script kills the Tentacle agent. Setting DISABLE_DIND=Y skips it.
# Created via mktemp in Step 4 so we never clobber an unrelated file the user
# may already have in the sibling repo.
OVERRIDE_COMPOSE=""

API="http://localhost:8065/api"
API_KEY="API-APIKEY01"
H="X-Octopus-ApiKey: $API_KEY"
IMAGE_TAG="smoke-debian12"
ONEPASSWORD_LICENSE_REF="op://software licencing/octopus deploy ultimate license key base64/value"

log()  { printf '\033[1;34m[smoke]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[smoke]\033[0m %s\n' "$*" >&2; }
die()  { printf '\033[1;31m[smoke]\033[0m %s\n' "$*" >&2; exit 1; }

require() { command -v "$1" >/dev/null || die "Missing required tool: $1"; }
require docker
require op
require curl
require jq

teardown() {
  local exit_code=$?
  log "--- teardown ---"
  if [[ -n "$OVERRIDE_COMPOSE" && -f "$OVERRIDE_COMPOSE" ]]; then
    (cd "$SERVER_REPO" && docker compose -f docker-compose.yml -f "$OVERRIDE_COMPOSE" --profile tentacle down 2>/dev/null) || true
    rm -f "$OVERRIDE_COMPOSE"
  fi
  (cd "$SERVER_REPO" && docker compose down 2>/dev/null) || true
  if [[ -n "$ENV_BACKUP" && -f "$ENV_BACKUP" ]]; then
    mv "$ENV_BACKUP" "$ENV_FILE"
    log "Restored $ENV_FILE"
  fi
  exit "$exit_code"
}
trap teardown EXIT

###############################################################################
# Step 1: Build the Linux Tentacle image from the local .deb
###############################################################################
log "--- Step 1: build Tentacle image ---"
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
# Step 2: Fetch license from 1Password & patch .env
###############################################################################
log "--- Step 2: fetch license and patch .env ---"
[[ -f "$ENV_FILE" ]] || die "Expected $ENV_FILE to exist."

if ! op account list >/dev/null 2>&1; then
  die "1Password CLI is not signed in. Run: eval \$(op signin)"
fi

LICENSE_BASE64="$(op read "$ONEPASSWORD_LICENSE_REF" 2>/dev/null || true)"
[[ -n "$LICENSE_BASE64" ]] || die "Could not read license from 1Password at: $ONEPASSWORD_LICENSE_REF"
log "Fetched license from 1Password (${#LICENSE_BASE64} bytes)"

ENV_BACKUP="$(mktemp "${TMPDIR:-/tmp}/octopus-server-env-smoke-tentacle-XXXXXX")"
cp "$ENV_FILE" "$ENV_BACKUP"
log "Backed up .env to $ENV_BACKUP (will be restored on exit)"

upsert_env_var() {
  # Pure-bash: avoids sed/awk escape headaches with a base64 value (which
  # contains '/' and '=' but not '\' or '&'). Matches the line by literal
  # "KEY=" prefix, not regex, so unusual keys won't bite us.
  local key="$1" value="$2"
  local tmp="$ENV_FILE.tmp" line found=
  : > "$tmp"
  while IFS= read -r line || [[ -n "$line" ]]; do
    if [[ "$line" == "${key}="* ]]; then
      printf '%s=%s\n' "$key" "$value" >> "$tmp"
      found=1
    else
      printf '%s\n' "$line" >> "$tmp"
    fi
  done < "$ENV_FILE"
  [[ -z "$found" ]] && printf '%s=%s\n' "$key" "$value" >> "$tmp"
  mv "$tmp" "$ENV_FILE"
}

upsert_env_var TENTACLE_TAG "$IMAGE_TAG"
upsert_env_var OCTOPUS_SERVER_BASE64_LICENSE "$LICENSE_BASE64"

###############################################################################
# Step 3: Bring up Octopus Server and wait for /api to respond
###############################################################################
log "--- Step 3: start octopus-server ---"
cd "$SERVER_REPO"
docker compose up -d octopus-server

log "Waiting for $API/octopusservernodes/ping ..."
for i in {1..120}; do
  if curl -fsS -H "$H" "$API/octopusservernodes/ping" >/dev/null 2>&1; then
    log "Server is up after ${i}s"
    break
  fi
  [[ $i -eq 120 ]] && die "Server did not become ready in 120s"
  sleep 1
done

###############################################################################
# Step 4: Bring up the Tentacle (Worker, polling mode, DIND disabled)
###############################################################################
log "--- Step 4: start tentacle ---"
OVERRIDE_COMPOSE="$(mktemp "${TMPDIR:-/tmp}/docker-compose-smoke-tentacle-XXXXXX")"
cat > "$OVERRIDE_COMPOSE" <<'YAML'
services:
  tentacle:
    environment:
      DISABLE_DIND: "Y"
YAML

COMPOSE=(docker compose -f docker-compose.yml -f "$OVERRIDE_COMPOSE" --profile tentacle)

# --no-deps because octopus-server may lack a healthcheck; we already polled
# its API ping above and know it's ready.
"${COMPOSE[@]}" up -d --no-deps tentacle

log "Waiting for Tentacle 'Configuration successful.' in logs ..."
for i in {1..60}; do
  if "${COMPOSE[@]}" logs --no-color tentacle 2>/dev/null | grep -qF "Configuration successful."; then
    log "Tentacle registered after ${i}s"
    break
  fi
  [[ $i -eq 60 ]] && die "Tentacle did not register in 60s. Logs:
$("${COMPOSE[@]}" logs --no-color --tail=80 tentacle)"
  sleep 1
done

# Make sure the agent is still running (the wrapper script can exit shortly
# after registration if a sidecar like dockerd dies).
if ! "${COMPOSE[@]}" ps --status running --services 2>/dev/null | grep -qx tentacle; then
  die "Tentacle container exited shortly after registration. Logs:
$("${COMPOSE[@]}" logs --no-color --tail=80 tentacle)"
fi

###############################################################################
# Step 5: Verify worker is registered & run hello-world AdHocScript
###############################################################################
log "--- Step 5: verify registration and run hello-world ---"

# Find the worker we just registered. The Tentacle picks its container hostname
# as the default name, so we can't filter by name reliably. Instead, take the
# worker whose Id is the largest "Workers-N" — i.e. the most recent registration.
WORKER_ID=""
WORKER_NAME=""
for i in {1..60}; do
  WORKERS_JSON="$(curl -fsS -H "$H" "$API/workers?take=1000" 2>/dev/null || echo '{"Items":[]}')"
  WORKER_ID="$(echo "$WORKERS_JSON" \
    | jq -r '[.Items[] | select(.Id | startswith("Workers-"))] | sort_by(.Id | ltrimstr("Workers-") | tonumber) | last | .Id // empty')"
  WORKER_NAME="$(echo "$WORKERS_JSON" | jq -r --arg id "$WORKER_ID" '.Items[] | select(.Id == $id) | .Name // empty')"
  [[ -n "$WORKER_ID" ]] && break
  sleep 1
done
if [[ -z "$WORKER_ID" ]]; then
  warn "No worker appeared. Diagnostic dump of $API/workers:"
  curl -fsS -H "$H" "$API/workers" || true
  warn "Tentacle container logs (tail 80):"
  docker compose --profile tentacle logs --no-color --tail=80 tentacle || true
  die "Worker did not appear after 60s"
fi
log "Registered worker: $WORKER_ID  (name='$WORKER_NAME')"

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
