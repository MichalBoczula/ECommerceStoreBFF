#!/usr/bin/env bash
set -Eeuo pipefail
cd "$(dirname "$0")/.."

stage_name=Setup
trap 'status=$?; echo "Verification stage $stage_name failed (exit $status)." >&2' ERR
stage() {
  stage_name="$1"
  echo "==> $stage_name"
}

results_dir="$PWD/artifacts/verification"
mkdir -p "$results_dir"
rm -rf "$results_dir/test"
rm -f "$results_dir/summary.md"

stage 'Source checks'
bash scripts/ci.sh source
stage 'Restore and build with NuGet Audit'
bash scripts/ci.sh build
stage 'Format handwritten code'
bash scripts/ci.sh format
stage 'Container integration tests, TRX and coverage'
bash scripts/ci.sh test
echo 'Local verification passed.'
