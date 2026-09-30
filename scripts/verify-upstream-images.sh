#!/usr/bin/env bash
set -Eeuo pipefail

cd "$(dirname "$0")/.."
compose=(docker compose -f docker-compose.upstream.yml)
export PRODUCTS_PORT="${PRODUCTS_PORT:-15000}"
export USERS_PORT="${USERS_PORT:-16500}"
export INVOICE_PORT="${INVOICE_PORT:-17000}"
export PAYMENTS_PORT="${PAYMENTS_PORT:-18000}"

cleanup() {
  result=$?
  if (( result != 0 )); then
    "${compose[@]}" ps || true
    "${compose[@]}" logs --tail=80 || true
  fi
  if [[ "${KEEP_UPSTREAMS:-0}" != 1 ]]; then
    "${compose[@]}" down --volumes --remove-orphans || true
  fi
  exit "$result"
}
trap cleanup EXIT

"${compose[@]}" up -d --wait --wait-timeout 240

for entry in "products:$PRODUCTS_PORT" "users:$USERS_PORT" "invoice:$INVOICE_PORT" "payments:$PAYMENTS_PORT"; do
  service="${entry%%:*}"
  port="${entry#*:}"
  ready=0
  for attempt in {1..90}; do
    if curl --noproxy '*' --fail --silent --max-time 3 "http://127.0.0.1:$port/health/ready" >/dev/null; then
      ready=1
      break
    fi
    sleep 2
  done
  if (( ready == 0 )); then
    echo "$service did not become ready on port $port" >&2
    exit 1
  fi
  curl --noproxy '*' --fail --silent --show-error "http://127.0.0.1:$port/health/live" >/dev/null
  spec_path="/swagger/v1/swagger.json"
  if [[ "$service" == payments ]]; then spec_path="/openapi.json"; fi
  curl --noproxy '*' --fail --silent --show-error \
    "http://127.0.0.1:$port$spec_path" \
    --output "${TMPDIR:-/tmp}/bff-$service-openapi.json"
done

python3 - <<'PY'
import json
import os
from pathlib import Path

root = Path("contracts/upstream")
for service, contract in (("products", "products"), ("users", "users"), ("invoice", "invoice"), ("payments", "payments")):
    actual = json.loads((Path(os.environ.get("TMPDIR", "/tmp")) / f"bff-{service}-openapi.json").read_text())
    expected = json.loads((root / f"{contract}.openapi.json").read_text())
    if actual != expected:
        raise SystemExit(f"{service} OpenAPI differs from the pinned contract")
    print(f"{service}: ready, live, and {len(actual['paths'])} OpenAPI paths verified")
PY
