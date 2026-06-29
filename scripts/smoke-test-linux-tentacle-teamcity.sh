#!/usr/bin/env bash
#
# TeamCity wrapper for scripts/smoke-test-linux-tentacle.sh.
#
# Emits TeamCity service messages so the smoke test surfaces as a single
# named integration test on a TeamCity build agent, with progress blocks
# per step (parsed from the inner script's `--- Step N: ... ---` markers)
# and a buildProblem on failure. The inner script's stdout/stderr is
# forwarded verbatim, so the TC messages are harmless noise when this
# wrapper is run locally.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INNER_SCRIPT="$SCRIPT_DIR/smoke-test-linux-tentacle.sh"
[[ -x "$INNER_SCRIPT" ]] || { echo "Missing executable: $INNER_SCRIPT" >&2; exit 1; }

SUITE_NAME="${TEAMCITY_SMOKE_SUITE_NAME:-LinuxTentacleSmoke}"
TEST_NAME="${TEAMCITY_SMOKE_TEST_NAME:-LinuxTentacleSmokeTest}"

# Service-message escaping per TeamCity's Build Script Interaction spec:
#   |→||  '→|'  newline→|n  CR→|r  [→|[  ]→|]
tc_escape() {
  local s=$1
  s=${s//|/||}
  s=${s//\'/|\'}
  s=${s//$'\n'/|n}
  s=${s//$'\r'/|r}
  s=${s//[/|[}
  s=${s//]/|]}
  printf '%s' "$s"
}

tc() { printf '##teamcity[%s]\n' "$*"; }

open_block=""
close_open_block() {
  if [[ -n "$open_block" ]]; then
    tc "blockClosed name='$open_block'"
    open_block=""
  fi
}

# The while-read loop consumes every line the inner script prints, so we
# stash its exit code in a temp file to recover it after the pipe drains.
exit_file=$(mktemp)
trap 'rm -f "$exit_file"' EXIT

ESC_SUITE=$(tc_escape "$SUITE_NAME")
ESC_TEST=$(tc_escape "$TEST_NAME")

start_epoch=$(date +%s)
tc "testSuiteStarted name='$ESC_SUITE'"
tc "testStarted name='$ESC_TEST' captureStandardOutput='true'"

# Process substitution keeps the loop in the current shell so $open_block
# survives across iterations. The `rc=0; … || rc=$?; echo "$rc" > …` form
# captures the inner script's exit code while keeping `set -e` happy — a
# bare `; echo $? > …` would never run, because `set -e` is inherited into
# the subshell and aborts it the moment the inner script exits non-zero.
while IFS= read -r line; do
  printf '%s\n' "$line"
  # Match the inner script's `[smoke] --- title ---` markers; the [^-]*
  # gap tolerates ANSI colour bytes wrapped around `[smoke]` by log().
  if [[ "$line" =~ \[smoke\][^-]*---\ (.+)\ ---$ ]]; then
    title=$(tc_escape "${BASH_REMATCH[1]}")
    close_open_block
    tc "blockOpened name='$title'"
    open_block="$title"
  fi
done < <(rc=0; "$INNER_SCRIPT" 2>&1 || rc=$?; echo "$rc" > "$exit_file")

close_open_block

inner_exit=$(<"$exit_file")
inner_exit=${inner_exit:-1}
duration_ms=$(( ($(date +%s) - start_epoch) * 1000 ))

if [[ "$inner_exit" -ne 0 ]]; then
  fail_msg=$(tc_escape "smoke-test-linux-tentacle.sh exited with code $inner_exit")
  tc "testFailed name='$ESC_TEST' message='$fail_msg'"
  tc "testFinished name='$ESC_TEST' duration='$duration_ms'"
  tc "testSuiteFinished name='$ESC_SUITE'"
  tc "buildProblem description='$fail_msg' identity='linux-tentacle-smoke'"
  exit "$inner_exit"
fi

tc "testFinished name='$ESC_TEST' duration='$duration_ms'"
tc "testSuiteFinished name='$ESC_SUITE'"
