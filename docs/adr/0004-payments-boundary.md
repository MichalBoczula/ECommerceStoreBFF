# ADR-0004: Expose the Payments API without inferring settlement

- Status: Accepted
- Date: 2026-09-30

## Context

The Payments service currently exposes `POST /payments/{order_id}/pay` and `GET /payments/order/{order_id}`. The first operation verifies the order snapshot and creates a Payment with status `created`; repeating it returns the same record. It does not contact a provider or mark the order paid. There is no HTTP cancel, provider confirmation or invoice issuance operation in this contract.

## Decision

Pin the published Payments image and OpenAPI document alongside the existing upstreams. Forward `/payments` through its own YARP cluster and expose the upstream document at `/api/payments/openapi.json` with Scalar at `/scalar/payments`. Generate the typed Payments client from the pinned contract. Test payment creation and idempotent reads through real containers, and assert that creation leaves the order in `Created` without an invoice.

## Consequences

Consumers may prepare and inspect a payment through the BFF. A `created` Payment is not evidence of a successful charge. UI checkout and invoice completion require a future Payments provider flow and the corresponding Orders/Invoice transition, each with its own contract and integration tests.
