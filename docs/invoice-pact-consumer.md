# PACT/10 BFF → Invoice registration cart

The consumer test drives the real `ShoppingCartRegistrationGateway` against
PactNet's HTTP mock. Registration uses these Invoice calls to reuse or create
the customer's cart.

| Interaction | Contract |
| --- | --- |
| Find an existing cart | `GET /shopping-carts/client/{clientId}` returns HTTP 200 with a cart ID, the matching client ID and a populated `lines` array containing `productId` and `quantity`. |
| Find a missing cart | The lookup returns HTTP 404 with Problem Details. The BFF can proceed to creation. |
| Create a cart | Bodyless `POST /shopping-carts/{clientId}` returns HTTP 200 with a cart ID, the matching client ID and empty `lines`. |
| Create a duplicate cart | The create returns HTTP 409 with Problem Details. The BFF retries the lookup. |

Run the focused consumer check with .NET 10.

```bash
BFF_INVOICE_PACT_DIR="$PWD/artifacts/verification/pact-consumer-invoice" \
  dotnet test test/ECommerceStoreBFF.IntegrationTests/ECommerceStoreBFF.IntegrationTests.csproj \
    --configuration Release --filter FullyQualifiedName~InvoiceConsumerPactTests
```

The integration job uploads `bff-invoice-pact` with its SHA-256 manifest.
The publisher on `master` sends the original bytes using the existing BFF
publisher and uploads `bff-invoice-publication`. Invoice provider CI can
then pin the published artifact and verify the four interactions.
