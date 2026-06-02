#!/usr/bin/env bash
# Run Windows-only test(s) on a GitHub Actions windows-latest runner and stream results.
#
# Usage: run.sh "<dotnet test --filter>"
#   e.g. run.sh "Name~CancelThenAbandon_WhenGrandchild"
#
# Requires: gh authenticated, and .github/workflows/windows-test.yml present on the
# repo's default branch (workflow_dispatch only works from the default branch).
# NOTE: gh's HTTPS fails under the command sandbox (TLS proxy) — invoke with the sandbox
# disabled.
#
# Exits with the run's status: 0 = tests passed.
set -euo pipefail

FILTER="${1:?usage: run.sh \"<filter>\"  e.g. Name~WhenGrandchildHoldsRedirectedPipes}"
REF="$(git rev-parse --abbrev-ref HEAD)"

# workflow_dispatch can only see the workflow once it's on the default branch. Fail with an
# actionable message instead of a confusing gh error if it isn't there yet.
DEFAULT="$(gh repo view --json defaultBranchRef --jq '.defaultBranchRef.name')"
if ! gh api "repos/{owner}/{repo}/contents/.github/workflows/windows-test.yml?ref=$DEFAULT" >/dev/null 2>&1; then
  echo "ERROR: windows-test.yml is not on the default branch ('$DEFAULT') yet, so workflow_dispatch can't see it. Merge this branch to '$DEFAULT' first." >&2
  exit 1
fi

echo "Dispatching windows-test.yml on $REF with filter: $FILTER"
SINCE="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
gh workflow run windows-test.yml -f filter="$FILTER" --ref "$REF"

# Don't blind-sleep then grab the latest run: a slow registration or a parallel dispatch
# would point us at the wrong one. Poll until OUR run shows up, matched by creation time.
RID=""
for _ in $(seq 1 30); do
  RID="$(gh run list --workflow windows-test.yml --branch "$REF" --limit 10 \
    --json databaseId,createdAt \
    --jq "[.[] | select(.createdAt >= \"$SINCE\")] | sort_by(.createdAt) | .[0].databaseId")"
  [[ -n "$RID" ]] && break
  sleep 2
done
[[ -n "$RID" ]] || { echo "ERROR: the dispatched run never appeared in 'gh run list'." >&2; exit 1; }

echo "Watching run $RID ..."
gh run watch "$RID" --exit-status
