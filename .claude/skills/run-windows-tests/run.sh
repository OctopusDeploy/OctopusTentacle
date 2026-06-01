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

FILTER="${1:-Name~CancelThenAbandon_WhenGrandchild}"
REF="$(git rev-parse --abbrev-ref HEAD)"

echo "Dispatching windows-test.yml on $REF with filter: $FILTER"
gh workflow run windows-test.yml -f filter="$FILTER" --ref "$REF"

# Give GitHub a moment to register the run, then find and watch it.
sleep 5
RID="$(gh run list --workflow windows-test.yml --branch "$REF" --limit 1 --json databaseId --jq '.[0].databaseId')"
echo "Watching run $RID ..."
gh run watch "$RID" --exit-status
