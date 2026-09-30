# ADR-0003: Coordinate customer registration and cart creation

- Status: Accepted
- Date: 2026-09-30

## Context

Users owns customer profiles and Invoice owns carts. Proxying `POST /customers` does not create a cart, so the storefront's registration contract fails. The two services do not share a transaction.

## Decision

Keep `/customers` as a transparent Users proxy. Add `POST /registrations/customers` to the BFF. Look up the profile by `externalId`, create it when missing, then confirm an Invoice cart by customer GUID, creating it when missing. Return success only after both records are confirmed. Retries reuse the matching profile and cart, including a concurrent 409 followed by a successful lookup. Reject a request whose profile data differs from the existing profile. Expose `POST /registrations/customers/{externalId}/cart` as an explicit repair operation for historical profiles without carts. The BFF application layer owns the sequence; infrastructure gateways call the configured upstream service destinations directly.

## Consequences

A failure after Users commits returns an error and leaves a recoverable partial registration. Repeating the same request completes it without duplicating the profile or cart. The BFF has no durable transaction log, and a cart cannot be created implicitly from the cart page. The repair route needs authorization before real customer identity and permissions are introduced; the current storefront is an explicit demo mode.

## Alternatives considered

- Create a cart from the Angular cart page: hides incomplete registration and spreads the invariant across clients.
- Change the Users `/customers` proxy response: breaks its transparent contract.
- Add a distributed transaction across MongoDB databases: couples independently deployed services.
