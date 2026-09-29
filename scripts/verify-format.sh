#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

mapfile -d '' handwritten < <(find src test -type f -name '*.cs' \
  ! -path '*/Generated/*' ! -path '*/obj/*' ! -path '*/bin/*' -print0)
if (( ${#handwritten[@]} == 0 )); then
  echo 'No handwritten C# files found for format verification.' >&2
  exit 1
fi
dotnet format ECommerceStoreBFF.slnx --verify-no-changes --no-restore --include "${handwritten[@]}"
