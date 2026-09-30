# Upstream API baseline

These OpenAPI files describe the exact API versions selected for the BFF client refresh. `manifest.json` records the Docker Hub digest and the SHA-256 of each specification. Run `bash scripts/generate-clients.sh` to restore the pinned Kiota tool and regenerate all four clients. Products, Users and Payments map to matching generated directories; the Invoice contract maps to `Generated/Orders` to preserve the existing BFF client name and namespace.

| File | Export source |
| --- | --- |
| `products.openapi.json` | Application layer extracted from the published Products image; run with migrations disabled for the export. |
| `users.openapi.json` | Isolated `WebApplicationFactory` export at the Users source commit matching the published image SHA tag. The existing Users acceptance test suite starts Docker from a global hook even when filtering to its OpenAPI export test. |
| `invoice.openapi.json` | Application layer extracted from the published Invoices image; run with `ASPNETCORE_ENVIRONMENT=OpenApiExport`. |
| `payments.openapi.json` | `payments-openapi` artifact from successful Payments `main` CI at the source commit in the manifest; FastAPI exports without MongoDB. |

Run `bash scripts/verify-upstream-images.sh` with Docker Compose, curl and Python 3. The script starts the four pinned images with disposable SQL Server and MongoDB, waits for `/health/ready`, checks `/health/live`, and compares each served OpenAPI document with the committed document. Payments serves `/openapi.json`; the other three serve `/swagger/v1/swagger.json`. It removes the temporary stack on exit. Set `KEEP_UPSTREAMS=1` to leave it running for local work, then stop it with `docker compose -f docker-compose.upstream.yml down --volumes`. The local port defaults are 15000, 16500, 17000 and 18000, configurable with `PRODUCTS_PORT`, `USERS_PORT`, `INVOICE_PORT` and `PAYMENTS_PORT`.

Changing an upstream image requires a fresh OpenAPI export and manifest update. A changed endpoint path or model must be reviewed before regenerating the corresponding Kiota client. The BFF's YARP routes remain defined in `src/ECommerceStoreBFF.API/appsettings.json`.
