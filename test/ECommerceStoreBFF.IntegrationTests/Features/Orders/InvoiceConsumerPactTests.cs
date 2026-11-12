using System.Globalization;
using System.Net;
using System.Text.Json;
using ECommerceStoreBFF.Application.Registration;
using ECommerceStoreBFF.Infrastructure.Registration;
using ECommerceStoreBFF.IntegrationTests.Common;
using PactNet;
using PactNet.Matchers;
using Shouldly;

namespace ECommerceStoreBFF.IntegrationTests.Features.Orders;

public sealed class InvoiceConsumerPactTests
{
    private const string CartsPath = "/shopping-carts";
    private const string Json = "application/json";
    private const string ProblemJson = "application/problem+json";
    private const string IdPattern = "^[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$";
    private const int InteractionCount = 4;
    private const int Quantity = 2;
    private static readonly Guid ExistingClientId = Guid.Parse("48000000-0000-4000-8000-000000000001");
    private static readonly Guid NewClientId = Guid.Parse("48000000-0000-4000-8000-000000000002");
    private static readonly Guid CartId = Guid.Parse("48000000-0000-4000-8000-000000000003");
    private static readonly Guid ProductId = Guid.Parse("48000000-0000-4000-8000-000000000004");

    [Fact]
    public Task ShoppingCartRegistrationGatewayEmitsReproduciblePact() =>
        ConsumerPactArtifact.VerifyAndWriteAsync("invoice", InteractionCount, "BFF_INVOICE_PACT_DIR", VerifyAsync);

    private static async Task VerifyAsync(string directory)
    {
        var pact = Pact.V4("bff", "invoice", new PactConfig { PactDir = directory }).WithHttpInteractions();
        const string existingState = "the BFF registration cart exists";
        const string missingState = "the BFF registration cart is absent";
        var existingParameters = new Dictionary<string, string>
        {
            ["clientId"] = ExistingClientId.ToString(),
            ["productId"] = ProductId.ToString(),
            ["quantity"] = Quantity.ToString(CultureInfo.InvariantCulture)
        };
        var missingParameters = new Dictionary<string, string> { ["clientId"] = NewClientId.ToString() };

        pact.UponReceiving("look up the existing registration cart")
            .Given(existingState, existingParameters)
            .WithRequest(HttpMethod.Get, $"{CartsPath}/client/{ExistingClientId}")
            .WillRespond().WithStatus(HttpStatusCode.OK).WithHeader("Content-Type", Json)
            .WithJsonBody(new
            {
                id = Match.Regex(CartId.ToString(), IdPattern),
                clientId = ExistingClientId,
                lines = new[] { new { productId = ProductId, quantity = Quantity } }
            });

        pact.UponReceiving("look up a missing registration cart")
            .Given(missingState, missingParameters)
            .WithRequest(HttpMethod.Get, $"{CartsPath}/client/{NewClientId}")
            .WillRespond().WithStatus(HttpStatusCode.NotFound).WithHeader("Content-Type", ProblemJson)
            .WithBody("""{"status":404}""", ProblemJson);

        pact.UponReceiving("create an empty registration cart")
            .Given(missingState, missingParameters)
            .WithRequest(HttpMethod.Post, $"{CartsPath}/{NewClientId}")
            .WillRespond().WithStatus(HttpStatusCode.OK).WithHeader("Content-Type", Json)
            .WithJsonBody(new
            {
                id = Match.Regex(CartId.ToString(), IdPattern),
                clientId = NewClientId,
                lines = Array.Empty<object>()
            });

        pact.UponReceiving("receive a conflict when the registration cart was already created")
            .Given(existingState, existingParameters)
            .WithRequest(HttpMethod.Post, $"{CartsPath}/{ExistingClientId}")
            .WillRespond().WithStatus(HttpStatusCode.Conflict).WithHeader("Content-Type", ProblemJson)
            .WithBody("""{"status":409}""", ProblemJson);

        await pact.VerifyAsync(async context =>
        {
            using var http = new HttpClient { BaseAddress = context.MockServerUri };
            var gateway = new ShoppingCartRegistrationGateway(http);
            var existing = AssertCart(await gateway.FindAsync(ExistingClientId, CancellationToken.None), ExistingClientId);
            existing.Lines.ShouldHaveSingleItem().ShouldBe(new ShoppingCartLine(ProductId, Quantity));
            AssertProblem(await gateway.FindAsync(NewClientId, CancellationToken.None), HttpStatusCode.NotFound);
            var created = AssertCart(await gateway.CreateAsync(NewClientId, CancellationToken.None), NewClientId);
            created.Lines.ShouldBeEmpty();
            AssertProblem(await gateway.CreateAsync(ExistingClientId, CancellationToken.None), HttpStatusCode.Conflict);
        });
    }

    private static ShoppingCart AssertCart(UpstreamResult<ShoppingCart> result, Guid clientId)
    {
        result.Status.ShouldBe((int)HttpStatusCode.OK);
        var cart = result.Value.ShouldNotBeNull();
        cart.Id.ShouldBe(CartId);
        cart.ClientId.ShouldBe(clientId);
        return cart;
    }

    private static void AssertProblem(UpstreamResult<ShoppingCart> result, HttpStatusCode expected)
    {
        result.Status.ShouldBe((int)expected);
        result.Value.ShouldBeNull();
        using var problem = JsonDocument.Parse(result.ErrorBody.ShouldNotBeNull());
        problem.RootElement.GetProperty("status").GetInt32().ShouldBe((int)expected);
    }
}
