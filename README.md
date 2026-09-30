# ECommerceStoreBFF

## Purpose

.NET 10 gateway for the ProductsCatalog, Users, Orders/Invoices and Payments APIs. YARP forwards their existing routes. The BFF also coordinates customer registration with an empty cart, serves Scalar pages backed by the proxied OpenAPI documents and exposes its own `/health` endpoint. It does not own product, customer, order, payment or invoice data.

## Architecture

| Component | Responsibility |
| --- | --- |
| `src/ECommerceStoreBFF.API` | YARP routes and destinations, registration endpoints, CORS, Scalar and local health endpoint. |
| `src/ECommerceStoreBFF.Infrastructure` | Four generated Kiota clients plus registration gateways to the configured Users and Invoice destinations. |
| `src/ECommerceStoreBFF.Application` | Registration coordination and retry rules behind upstream ports. |
| `test/ECommerceStoreBFF.IntegrationTests` | HTTP tests through the BFF with four real upstream APIs and their databases. |
| `contracts/upstream` | Pinned image digests, OpenAPI documents and generation baseline. |

YARP owns the public forwarding behavior. Kiota clients are generated from the pinned upstream specifications for typed access and integration tests; they do not replace the proxy routes. See [ADR-0001](docs/adr/0001-yarp-boundary.md).

### Routes

The public paths are the upstream paths, without an `/api/{service}` prefix. These prefixes are used only for the four OpenAPI proxy routes.

| Upstream | Public route groups | Proxied OpenAPI | Scalar |
| --- | --- | --- | --- |
| ProductsCatalog | `/products-documentation`, `/mobile-phones`, `/categories`, `/currencies` | `/api/products/swagger/v1/swagger.json` | `/scalar/products` |
| Users | `/users-documentation`, `/users`, `/customers`, `/admins`, `/favorites` | `/api/users/swagger/v1/swagger.json` | `/scalar/users` |
| Orders/Invoices | `/orders-documentation`, `/orders`, `/shopping-carts`, `/invoices`, `/client-data-versions` | `/api/orders/swagger/v1/swagger.json` | `/scalar/orders` |
| Payments | `/payments` | `/api/payments/openapi.json` | `/scalar/payments` |

Payments currently creates or reads a `created` Payment and checks the order snapshot. It does not contact a payment provider, set an order to `Paid`, cancel a Payment via HTTP, or issue an invoice. The BFF forwards this contract without treating a payment record as proof of settlement. See [ADR-0004](docs/adr/0004-payments-boundary.md).

### Registration

`POST /registrations/customers` accepts the Users customer creation body and returns the customer only after its Invoice cart is confirmed. Retrying the identical request with the same `externalId` reuses the profile and cart; a different profile for that ID returns 409. If Users succeeds and Invoice is unavailable, the BFF returns 502 and the caller can retry with the same request. `POST /registrations/customers/{externalId}/cart` repairs a preexisting customer whose cart is missing, without creating another profile. Existing `/customers` and `/shopping-carts` routes continue to proxy their upstream APIs. See [ADR-0003](docs/adr/0003-registration-orchestration.md).

`/health` belongs to the BFF and checks its process, not the readiness of the upstream APIs. YARP destination addresses and route matches live in [`appsettings.json`](src/ECommerceStoreBFF.API/appsettings.json); the integration tests assert the current proxy behavior. The configured browser CORS origin is `http://localhost:4200` for the service route groups. Adjust that policy in `Program.cs` for another frontend origin.

## Local startup

### Prerequisites

- .NET 10 SDK (the `global.json` file selects 10.0.100 with `latestFeature` roll-forward).
- Docker Engine or Docker Desktop with Docker Compose, Bash, curl and Python 3.
- Free host ports `5137`, `15000`, `16500`, `17000` and `18000`. Docker also needs enough resources for SQL Server, MongoDB and four API containers.

The checked-in [`docker-compose.upstream.yml`](docker-compose.upstream.yml) starts disposable SQL Server, a MongoDB replica set and the four upstream APIs pinned by digest. The example database passwords and data are for local work only. The BFF runs on the host in this walkthrough, so Docker publishes the upstream ports to `127.0.0.1`.

From the repository root, start and check the upstream stack:

```bash
KEEP_UPSTREAMS=1 bash scripts/verify-upstream-images.sh
```

This waits for `/health/ready` and `/health/live` and compares each served OpenAPI document to the pinned contract. In a second shell, from the repository root, start the BFF with the addresses of those containers:

```bash
env \
  'GatewaySettings__BaseUrl=http://localhost:5137' \
  'ReverseProxy__Clusters__products-cluster__Destinations__destination1__Address=http://127.0.0.1:15000' \
  'ReverseProxy__Clusters__users-cluster__Destinations__destination1__Address=http://127.0.0.1:16500' \
  'ReverseProxy__Clusters__orders-cluster__Destinations__destination1__Address=http://127.0.0.1:17000' \
  'ReverseProxy__Clusters__payments-cluster__Destinations__destination1__Address=http://127.0.0.1:18000' \
  dotnet run --project src/ECommerceStoreBFF.API --launch-profile http
```

The `http` launch profile listens at `http://localhost:5137`. `GatewaySettings__BaseUrl` points typed Kiota clients back to the BFF. The four `ReverseProxy__Clusters__...__Address` values point YARP to upstream APIs. Keep the two kinds of address separate: pointing a destination back to the BFF would cause a proxy loop. For different ports, set `PRODUCTS_PORT`, `USERS_PORT`, `INVOICE_PORT` and `PAYMENTS_PORT` for the startup command and use the same values in the BFF destination URLs. If the BFF runs in a container, `localhost` inside that container does not refer to the host or another container; supply network-reachable addresses for all four values.

Check the gateway:

```bash
curl -i http://localhost:5137/health
curl -i http://localhost:5137/api/products/swagger/v1/swagger.json
curl -i http://localhost:5137/api/users/swagger/v1/swagger.json
curl -i http://localhost:5137/api/orders/swagger/v1/swagger.json
curl -i http://localhost:5137/api/payments/openapi.json
```

Open the Scalar pages from the route table to browse the four API contracts. Shut down the disposable upstream stack with `docker compose -f docker-compose.upstream.yml down --volumes`; this removes its database data. Without `KEEP_UPSTREAMS=1`, the verification script stops the stack automatically.

## Kiota clients and contracts

The four OpenAPI files in `contracts/upstream/` correspond to the image digests in [`manifest.json`](contracts/upstream/manifest.json). The generated clients live under `src/ECommerceStoreBFF.Infrastructure/Generated/{Products,Users,Orders,Payments}`. `Orders` maps to the Invoice API. [`scripts/generate-clients.sh`](scripts/generate-clients.sh) restores the pinned Kiota 1.34.1 tool and regenerates all four clients:

```bash
bash scripts/generate-clients.sh
```

When updating an upstream image, first refresh and review its OpenAPI document and manifest, then regenerate the clients and run the integration tests. [`contracts/upstream/README.md`](contracts/upstream/README.md) records the export methods and the verification command. Do not hand-edit generated files. Client DI registration is in [`DependencyInjection.cs`](src/ECommerceStoreBFF.Infrastructure/DependencyInjection.cs).

## Verification and CI

Run the complete local check from the repository root:

```bash
bash scripts/verify.sh
```

It checks shell/Python sources, restores and builds the solution with NuGet Audit, verifies handwritten C# formatting, runs container integration tests, writes TRX and handwritten-code coverage reports, and builds the BFF Docker image. Testcontainers create their own isolated SQL Server, MongoDB replica set and upstream API containers on dynamic ports; the manual Compose stack above is not required for tests. Docker must be running. Local reports are written under `artifacts/verification/` (including `summary.md`). Generated Kiota sources are excluded from the coverage report; `DependencyInjection.cs` remains in its scope. There is no minimum coverage percentage gate in the current BFF workflow.

For one focused integration run:

```bash
dotnet test test/ECommerceStoreBFF.IntegrationTests/ECommerceStoreBFF.IntegrationTests.csproj --configuration Release
```

On pull requests and pushes to `master`, [GitHub Actions CI](.github/workflows/ci.yml) runs source/build/format checks, container integration tests, secret scanning and a quality gate. Pull requests also run Dependency Review; on `master` that job is intentionally skipped. Only after the quality gate succeeds does CI build the image, smoke-test `/health`, and scan for fixable HIGH/CRITICAL vulnerabilities with Trivy. The [upstream baseline workflow](.github/workflows/upstream-contracts.yml) checks image readiness and OpenAPI compatibility when upstream contracts or their Compose verification change; it can also be started manually.

On a successful `master` run, CI pushes the same scanned image to Docker Hub as `mb0101/ecommerce-store-bff-api:<full commit SHA>` and `mb0101/ecommerce-store-bff-api:latest`, then compares the published digests. It uses GitHub Actions secrets `DOCKERHUB_USERNAME` and `DOCKERHUB_TOKEN`; pull requests never publish. Publishing an image does not deploy it to a runtime. See [ADR-0002](docs/adr/0002-scanned-image-publication.md).

## Architecture decisions

The [ADR index](docs/adr/README.md) records the gateway, image publication, registration and payment boundary decisions.
