#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

dotnet_cli="${DOTNET:-dotnet}"
"$dotnet_cli" tool restore

generate() {
  local contract="$1" directory="$2" class_name="$3"
  "$dotnet_cli" kiota generate \
    --openapi "contracts/upstream/${contract}.openapi.json" \
    --language CSharp \
    --class-name "$class_name" \
    --namespace-name "ECommerceStoreBFF.Infrastructure.Generated.${directory}" \
    --output "src/ECommerceStoreBFF.Infrastructure/Generated/${directory}" \
    --additional-data \
    --clean-output
}

generate products Products ProductsApiClient
generate users Users UsersApiClient
generate invoice Orders OrdersApiClient
generate payments Payments PaymentsApiClient
