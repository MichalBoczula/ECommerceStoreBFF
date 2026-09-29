using ECommerceStoreBFF.AcceptanceTests;
using System.Net;

namespace ECommerceStoreBFF.IntegrationTests.Features;

[Collection("Api Test Collection")]
public class HealthCheckTests(ApplicationFactory factory)
{
    [Theory]
    [InlineData("products")]
    [InlineData("users")]
    [InlineData("invoice")]
    public async Task Upstream_ReadyHealthCheck_ShouldReturnOk(string service)
    {
        var address = service switch
        {
            "products" => factory.ProductsBaseAddress,
            "users" => factory.UsersBaseAddress,
            "invoice" => factory.InvoiceBaseAddress,
            _ => throw new ArgumentOutOfRangeException(nameof(service))
        };

        using var client = new HttpClient { BaseAddress = address };
        using var response = await client.GetAsync("health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Bff_HealthCheck_ShouldReturnOk()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/products-documentation/flow")]
    [InlineData("/users-documentation/flows")]
    [InlineData("/orders-documentation/flows")]
    public async Task Bff_ForwardsToReadyUpstream(string path)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
