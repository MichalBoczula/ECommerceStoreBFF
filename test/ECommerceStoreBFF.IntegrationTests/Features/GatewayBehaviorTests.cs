using ECommerceStoreBFF.AcceptanceTests;
using ECommerceStoreBFF.Infrastructure.Generated.Orders.Models;
using ECommerceStoreBFF.Infrastructure.Generated.Users.Models;
using ECommerceStoreBFF.IntegrationTests.Features.Orders;
using ECommerceStoreBFF.IntegrationTests.Features.Products;
using ECommerceStoreBFF.IntegrationTests.Features.Users;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ECommerceStoreBFF.IntegrationTests.Features;

[Collection("Api Test Collection")]
public class GatewayBehaviorTests(ApplicationFactory factory)
{
    [Theory]
    [InlineData("products", "/api/products/swagger/v1/swagger.json")]
    [InlineData("users", "/api/users/swagger/v1/swagger.json")]
    [InlineData("invoice", "/api/orders/swagger/v1/swagger.json")]
    [InlineData("payments", "/api/payments/openapi.json")]
    public async Task OpenApiProxy_PreservesUpstreamDocument(string service, string gatewayPath)
    {
        using var bff = factory.CreateClient();
        using var upstream = UpstreamClient(service);
        using var proxied = await bff.GetAsync(gatewayPath);
        using var original = await upstream.GetAsync(service == "payments" ? "/openapi.json" : "/swagger/v1/swagger.json");

        proxied.StatusCode.ShouldBe(HttpStatusCode.OK);
        proxied.StatusCode.ShouldBe(original.StatusCode);
        using var proxiedJson = JsonDocument.Parse(await proxied.Content.ReadAsStringAsync());
        using var originalJson = JsonDocument.Parse(await original.Content.ReadAsStringAsync());
        proxiedJson.RootElement.GetProperty("paths").EnumerateObject().ShouldNotBeEmpty();
        JsonElement.DeepEquals(proxiedJson.RootElement, originalJson.RootElement).ShouldBeTrue();
    }

    [Theory]
    [InlineData("products", "/api/products/swagger/v1/swagger.json")]
    [InlineData("users", "/api/users/swagger/v1/swagger.json")]
    [InlineData("orders", "/api/orders/swagger/v1/swagger.json")]
    [InlineData("payments", "/api/payments/openapi.json")]
    public async Task ScalarPageAndCorrespondingProxiedSpec_AreAvailable(string page, string specPath)
    {
        using var bff = factory.CreateClient();
        using var response = await bff.GetAsync($"/scalar/{page}");
        using var spec = await bff.GetAsync(specPath);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
        (await response.Content.ReadAsStringAsync()).ShouldContain($"ECommerce BFF - {char.ToUpperInvariant(page[0])}{page[1..]} API");
        spec.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var specJson = JsonDocument.Parse(await spec.Content.ReadAsStringAsync());
        specJson.RootElement.GetProperty("paths").EnumerateObject().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ProductGet_ForwardsQueryStringAndJsonBody()
    {
        using var bff = factory.CreateClient();
        await ProductTestData.CreatePhoneAsync(ProductTestData.Client(bff));
        using var upstream = UpstreamClient("products");

        using var one = await bff.GetAsync("/mobile-phones?amount=1");
        using var two = await bff.GetAsync("/mobile-phones?amount=2");
        using var original = await upstream.GetAsync("/mobile-phones?amount=2");

        one.StatusCode.ShouldBe(HttpStatusCode.OK);
        two.StatusCode.ShouldBe(HttpStatusCode.OK);
        original.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var oneJson = JsonDocument.Parse(await one.Content.ReadAsStringAsync());
        using var twoJson = JsonDocument.Parse(await two.Content.ReadAsStringAsync());
        using var originalJson = JsonDocument.Parse(await original.Content.ReadAsStringAsync());
        oneJson.RootElement.GetArrayLength().ShouldBe(1);
        twoJson.RootElement.GetArrayLength().ShouldBe(2);
        JsonElement.DeepEquals(twoJson.RootElement, originalJson.RootElement).ShouldBeTrue();
    }

    [Fact]
    public async Task UsersPostAndPut_ForwardBodyAndPersistAtUpstream()
    {
        using var bff = factory.CreateClient();
        using var upstream = UpstreamClient("users");
        var externalId = UsersTestData.ExternalId();
        var created = await UsersTestData.CreateCustomerAsync(UsersTestData.Client(bff), externalId);
        var update = new UpdateIndividualDataRequestDto
        {
            Individual = UsersTestData.IndividualRequest("Anna")
        };

        using var response = await bff.PutAsJsonAsync($"/customers/{created.Id}/individual", update);
        using var original = await upstream.GetAsync($"/customers/external/{externalId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        original.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var proxiedJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        using var originalJson = JsonDocument.Parse(await original.Content.ReadAsStringAsync());
        proxiedJson.RootElement.GetProperty("individual").GetProperty("firstName").GetString().ShouldBe("Anna");
        proxiedJson.RootElement.GetProperty("id").GetGuid().ShouldBe(originalJson.RootElement.GetProperty("id").GetGuid());
        var persistedIndividual = originalJson.RootElement.GetProperty("individual");
        persistedIndividual.GetProperty("firstName").GetString().ShouldBe("Anna");
        persistedIndividual.GetProperty("email").GetString().ShouldBe(update.Individual!.Email);
    }

    [Fact]
    public async Task OrdersPatch_ForwardsMethodAndBody()
    {
        using var bff = factory.CreateClient();
        using var upstream = UpstreamClient("invoice");
        var phone = await ProductTestData.CreatePhoneAsync(ProductTestData.Client(bff));
        var order = await OrdersTestData.CreateOrderAsync(OrdersTestData.Client(bff), Guid.NewGuid(), phone.Id!.Value);

        using var response = await bff.PatchAsJsonAsync($"/orders/{order.Id}/status",
            new UpdateOrderStatusRequestDto { Status = "Paid" });
        using var original = await upstream.GetAsync($"/orders/{order.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        original.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var proxiedJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        using var originalJson = JsonDocument.Parse(await original.Content.ReadAsStringAsync());
        proxiedJson.RootElement.GetProperty("status").GetString().ShouldBe("Paid");
        proxiedJson.RootElement.GetProperty("id").GetGuid().ShouldBe(originalJson.RootElement.GetProperty("id").GetGuid());
        originalJson.RootElement.GetProperty("status").GetString().ShouldBe("Paid");
    }

    [Theory]
    [InlineData("products", "/mobile-phones/")]
    [InlineData("users", "/customers/external/")]
    [InlineData("invoice", "/orders/")]
    [InlineData("payments", "/payments/order/")]
    public async Task MissingResource_PreservesUpstreamStatusAndProblemDetails(string service, string prefix)
    {
        using var bff = factory.CreateClient();
        using var upstream = UpstreamClient(service);
        var path = prefix + Guid.NewGuid();
        using var proxied = await bff.GetAsync(path);
        using var original = await upstream.GetAsync(path);

        proxied.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        proxied.StatusCode.ShouldBe(original.StatusCode);
        proxied.Content.Headers.ContentType?.MediaType.ShouldBe(original.Content.Headers.ContentType?.MediaType);
        var proxiedBody = JsonNode.Parse(await proxied.Content.ReadAsStringAsync())!.AsObject();
        var originalBody = JsonNode.Parse(await original.Content.ReadAsStringAsync())!.AsObject();
        // Each request gets its own trace ID; all other error fields belong to the upstream API.
        proxiedBody.Remove("traceId");
        originalBody.Remove("traceId");
        JsonNode.DeepEquals(proxiedBody, originalBody).ShouldBeTrue();
    }

    [Fact]
    public async Task ValidationFailure_PreservesUpstreamStatusAndErrorBody()
    {
        using var bff = factory.CreateClient();
        using var upstream = UpstreamClient("users");
        var request = UsersTestData.CustomerRequest(UsersTestData.ExternalId());
        request.Individual!.Email = "invalid-email";

        using var proxied = await bff.PostAsJsonAsync("/customers", request);
        using var original = await upstream.PostAsJsonAsync("/customers", request);

        proxied.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        proxied.StatusCode.ShouldBe(original.StatusCode);
        proxied.Content.Headers.ContentType?.MediaType.ShouldBe(original.Content.Headers.ContentType?.MediaType);
        var proxiedBody = JsonNode.Parse(await proxied.Content.ReadAsStringAsync())!.AsObject();
        var originalBody = JsonNode.Parse(await original.Content.ReadAsStringAsync())!.AsObject();
        proxiedBody.Remove("traceId");
        originalBody.Remove("traceId");
        JsonNode.DeepEquals(proxiedBody, originalBody).ShouldBeTrue();
    }

    [Theory]
    [InlineData("/mobile-phones")]
    [InlineData("/customers")]
    [InlineData("/orders/client/00000000-0000-0000-0000-000000000001")]
    [InlineData("/payments/00000000-0000-0000-0000-000000000001/pay")]
    public async Task CorsPreflight_AllowsConfiguredAngularOrigin(string path)
    {
        using var bff = factory.CreateClient();
        using var preflight = new HttpRequestMessage(HttpMethod.Options, path);
        preflight.Headers.Add("Origin", "http://localhost:4200");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type");

        using var response = await bff.SendAsync(preflight);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").Single().ShouldBe("http://localhost:4200");
        response.Headers.GetValues("Access-Control-Allow-Credentials").Single().ShouldBe("true");
        response.Headers.GetValues("Access-Control-Allow-Methods").Single().ShouldContain("POST");
        response.Headers.GetValues("Access-Control-Allow-Headers").Single().ShouldContain("content-type", Case.Insensitive);
    }

    [Fact]
    public async Task Cors_DoesNotAllowOtherOrigins()
    {
        using var bff = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/orders-documentation/flows");
        request.Headers.Add("Origin", "https://unconfigured.example");

        using var response = await bff.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
        response.Headers.Contains("Access-Control-Allow-Credentials").ShouldBeFalse();
    }

    [Fact]
    public async Task Health_IsServedByBffAndUnknownRouteReturnsNotFound()
    {
        using var bff = factory.CreateClient();
        using var health = await bff.GetAsync("/health");
        using var unknown = await bff.GetAsync("/unmapped/bff-route");

        health.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await health.Content.ReadAsStringAsync()).ShouldBe("Healthy");
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private HttpClient UpstreamClient(string service) => new()
    {
        BaseAddress = service switch
        {
            "products" => factory.ProductsBaseAddress,
            "users" => factory.UsersBaseAddress,
            "invoice" => factory.InvoiceBaseAddress,
            "payments" => factory.PaymentsBaseAddress,
            _ => throw new ArgumentOutOfRangeException(nameof(service))
        }
    };
}
