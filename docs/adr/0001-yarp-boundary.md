# ADR-0001: Keep YARP as the BFF HTTP boundary

- Status: Accepted
- Date: 2026-09-29

## Context

The BFF exposes ProductsCatalog, Users and Orders/Invoices through three YARP clusters. Their HTTP methods, paths, query strings, bodies and upstream responses need to remain visible to consumers. Generated Kiota clients provide typed access to the pinned upstream contracts but are not the implementation of those gateway routes.

## Decision

Keep the existing YARP routes and transforms in `src/ECommerceStoreBFF.API/appsettings.json`. Service endpoints retain their upstream paths; only the three `/api/{products|users|orders}/swagger/v1/swagger.json` routes strip their prefixes before forwarding. Scalar uses these proxied specifications. The BFF owns its `/health` endpoint and the configured CORS policy. Register Kiota clients with `GatewaySettings:BaseUrl` pointing at the BFF; generate their sources from the reviewed OpenAPI baseline under `contracts/upstream/`. Test observable routing through the BFF against the real API containers.

## Consequences

Upstream APIs continue to own validation, data and business error responses. The BFF's `/health` does not imply upstream readiness. Adding a new upstream route group requires a YARP route change and gateway tests; changing a contract also requires a reviewed OpenAPI baseline and regenerated Kiota client. Deployment must provide network-reachable YARP destinations and the BFF's own base URL.

## Alternatives considered

- Replace proxy forwarding with controller endpoints that call Kiota: this changes the transparent HTTP boundary and requires mapping every upstream behavior.
- Add `/api/{service}` to all public business routes: this changes existing consumer URLs.
