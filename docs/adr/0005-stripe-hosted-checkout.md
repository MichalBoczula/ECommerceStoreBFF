# ADR-0005: Stripe-hosted Checkout through the existing gateway

Status: Accepted for STRIPE/4. Supersedes the provider-free scope in ADR-0004.

The STRIPE/3 Orders/Invoice image and merged Payments sandbox image are pinned in the upstream manifest. Regenerated Kiota clients expose Checkout and completed invoice lookup. Browser requests continue to use the BFF; purchase money comes from the Orders snapshot.

`POST /payments/{orderId}/checkout` is bodyless and returns the provider's hosted URL. The Angular UI redirects to Stripe for card/BLIK entry. The browser never receives Stripe secret keys or submits an amount. `GET /payments/order/{orderId}`, order reads and `GET /invoices/by-order/{clientId}/{orderId}` report authoritative state. A success/cancel return is only a refresh hint, never settlement evidence.

The existing YARP `/payments` route already exposes `POST /payments/webhooks/stripe`. Its public HTTPS URL is `https://<bff-host>/payments/webhooks/stripe`; the destination is the configured payments-cluster address plus the identical path. On the frontend origin, `/backend/payments/webhooks/stripe` removes `/backend` before reaching YARP. Prefer the direct public BFF URL for Stripe configuration. No JSON model binding, body transformation or header rewrite is added: YARP forwards the original bytes and `Stripe-Signature`; Payments verifies the signature. Configure the test-mode webhook signing secret in Payments only. Never log signatures, payloads, hosted URLs or query strings.

The current gateway has no authenticated customer principal. Browser demo selection and order/customer association checks improve demo UX but do not authorize access in production. Production identity and ownership enforcement remain separate work.

Signed events record successful payment and durable fulfillment work. A separately scheduled Payments worker updates the order and creates/reuses a completed invoice. API startup does not schedule this worker (DEP/6). Completed invoice metadata can be shown locally; a `file://` storage URL is not a browser download (DEP/7).

Gateway tests verify raw signed bytes and Checkout responses through YARP using a deterministic loopback provider boundary; existing tests additionally run the pinned real upstream containers. Ordinary CI does not automate Stripe's hosted UI. Actual test-mode browser smoke is documented separately in the web repository.
