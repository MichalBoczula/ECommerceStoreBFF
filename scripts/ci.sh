#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

command="${1:?Usage: scripts/ci.sh source|build|format|test}"
results_dir="${VERIFY_RESULTS_DIR:-$PWD/artifacts/verification}"
summary_file="${VERIFY_SUMMARY_FILE:-$results_dir/summary.md}"

case "$command" in
  source)
    git diff --check
    for script in scripts/*.sh; do bash -n "$script"; done
    python3 -m py_compile scripts/summarize-trx.py scripts/report-coverage.py
    ;;
  build)
    dotnet restore ECommerceStoreBFF.slnx
    dotnet build ECommerceStoreBFF.slnx --configuration Release --no-restore
    ;;
  format)
    bash scripts/verify-format.sh
    ;;
  test)
    mkdir -p "$results_dir"
    rm -rf "$results_dir/test"
    dotnet restore ECommerceStoreBFF.slnx
    test_status=0
    dotnet test test/ECommerceStoreBFF.IntegrationTests/ECommerceStoreBFF.IntegrationTests.csproj \
      --configuration Release --no-restore \
      --logger 'trx;LogFileName=bff-integration.trx' --results-directory "$results_dir/test" \
      --collect 'XPlat Code Coverage' \
      --settings test/ECommerceStoreBFF.IntegrationTests/coverage.runsettings || test_status=$?
    summary_status=0
    python3 scripts/summarize-trx.py "$results_dir/test" "$summary_file" || summary_status=$?
    if (( test_status != 0 )); then exit "$test_status"; fi
    if (( summary_status != 0 )); then exit "$summary_status"; fi
    python3 scripts/report-coverage.py "$results_dir/test" "$summary_file"
    ;;
  *)
    echo "Unknown verification command: $command" >&2
    exit 2
    ;;
esac
