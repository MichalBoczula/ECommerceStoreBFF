# PACT/10 Web → BFF registration verification

BFF CI replays the Web acceptance client's Pact against the running BFF
candidate. The two interactions register a new demo customer and repair a
missing cart for an existing customer. Both exercise the real registration
service, HTTP gateways, and response serialization. The provider states use
isolated loopback fixtures for Users and Invoice and check that each flow
creates a cart. The existing container integration suite exercises the real
upstream services and their databases.

## Run locally

Use .NET 10 and uv from the repository root.

```bash
dotnet build src/ECommerceStoreBFF.API/ECommerceStoreBFF.API.csproj --configuration Release
bash scripts/ci.sh pact
```

The check reads `contracts/pact/web-bff.json` and validates its SHA-256 and
identity against `contracts/pact/web-source.json`. To update the pin, review
the `web-bff-pact` and `web-bff-publication` artifacts from the intended
successful Web `master` run and preserve the original JSON bytes. The
`pact-provider` job requires the replay to pass and uploads the BFF log,
Pact results and normalized verification report as `web-bff-verification`.

## Registry publication

The replay runs in PRs without registry credentials. For publication on
`master`, set the repository Actions variable `PACT_REGISTRY_URL` to the
HTTPS registry origin and the Actions secret `PACT_PUBLISHER_TOKEN` to the raw
token for the publisher scoped to `bff`. In Terraform, map
`bff-ci = ["bff"]` in `pact_publishers` and set its corresponding SHA-256
in `pact_publisher_token_sha256`.

The publish job confirms the source publication and hashes its registry
download before posting the result under `web-bff-registration`. The
`web-bff-verification-receipt` artifact identifies the consumer publication,
exact BFF commit, and CI run.
