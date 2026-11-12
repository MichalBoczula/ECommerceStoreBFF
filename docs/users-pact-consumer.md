# PACT/10 BFF → Users registration

The consumer test drives the real `CustomerRegistrationGateway` against
PactNet's HTTP mock. It exercises the JSON emitted by the gateway and the
responses that `RegistrationService` relies on.

| Interaction | Contract |
| --- | --- |
| Find an existing customer | `GET /customers/external/{externalId}` returns 200 with the customer ID, external ID, individual details, company details, and update timestamp. |
| Find a missing customer | The lookup returns 404 with Problem Details. BFF can proceed to creation. |
| Create a new customer | `POST /customers` sends the registration data with an empty company list when omitted by the caller. Users returns the created profile with 200. |
| Create a duplicate customer | Users returns 409 with Problem Details so BFF can retry the lookup. |

Run the consumer check with .NET 10.

```bash
BFF_PACT_DIR="$PWD/artifacts/verification/pact-consumer-users" \
  dotnet test test/ECommerceStoreBFF.IntegrationTests/ECommerceStoreBFF.IntegrationTests.csproj \
    --configuration Release --filter FullyQualifiedName~UsersConsumerPactTests
```

The test checks that two independent mock runs produce identical Pact V4 bytes.
The integration job uploads `bff-users-pact` with a SHA-256 manifest. After the
quality gate on `master`, CI publishes the original bytes with the BFF publisher
and uploads a `bff-users-publication` receipt for the exact commit and run. Both
pushes and manual runs use the existing repository Actions settings.

Users provider CI can pin that artifact and replay it against isolated customer states.
