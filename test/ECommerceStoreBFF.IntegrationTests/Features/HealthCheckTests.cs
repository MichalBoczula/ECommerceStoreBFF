using ECommerceStoreBFF.AcceptanceTests;
using ECommerceStoreBFF.Infrastructure.Generated.Products;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace ECommerceStoreBFF.IntegrationTests.Features;

public class HealthCheckTests : IClassFixture<ApplicationFactory>
{
    private readonly ApplicationFactory _factory;

    public HealthCheckTests(ApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Gateway_HealthCheck_ShouldReturnOk()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProductsKiotaClient_ShouldProxyThroughYarp()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var productsClient = scope.ServiceProvider.GetRequiredService<ProductsApiClient>();

        // Act
        var flow = await productsClient.ProductsDocumentation.Flow.GetAsync();

        // Assert
        Assert.NotNull(flow);
    }

    [Fact]
    public async Task UsersRoute_ShouldProxyThroughYarp_AndReturnSuccess()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - Directly testing YARP routing to Users service
        var response = await client.GetAsync("/users-documentation/flow");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OrdersRoute_ShouldProxyThroughYarp_AndReturnSuccess()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - Directly testing YARP routing to Invoice/Orders service
        var response = await client.GetAsync("/orders-documentation/flow");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}