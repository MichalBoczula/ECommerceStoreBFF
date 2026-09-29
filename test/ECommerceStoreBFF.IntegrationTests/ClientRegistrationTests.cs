using ECommerceStoreBFF.Infrastructure;
using ECommerceStoreBFF.Infrastructure.Generated.Orders;
using ECommerceStoreBFF.Infrastructure.Generated.Products;
using ECommerceStoreBFF.Infrastructure.Generated.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Net;

namespace ECommerceStoreBFF.IntegrationTests;

public class ClientRegistrationTests
{
    [Fact]
    public async Task RegisteredClients_UseConfiguredGatewayBaseUrl()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GatewaySettings:BaseUrl"] = "https://gateway.example.test:8443/"
            })
            .Build();

        var productsHandler = new RecordingHandler();
        var usersHandler = new RecordingHandler();
        var ordersHandler = new RecordingHandler();
        var services = new ServiceCollection().AddInfrastructureServices(configuration);
        services.AddHttpClient(nameof(ProductsApiClient)).ConfigurePrimaryHttpMessageHandler(() => productsHandler);
        services.AddHttpClient(nameof(UsersApiClient)).ConfigurePrimaryHttpMessageHandler(() => usersHandler);
        services.AddHttpClient(nameof(OrdersApiClient)).ConfigurePrimaryHttpMessageHandler(() => ordersHandler);
        using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<ProductsApiClient>().MobilePhones.GetAsync();
        await provider.GetRequiredService<UsersApiClient>().UsersDocumentation.Flows.GetAsync();
        await provider.GetRequiredService<OrdersApiClient>().OrdersDocumentation.Flows.GetAsync();

        productsHandler.RequestUri!.GetLeftPart(UriPartial.Path)
            .ShouldBe("https://gateway.example.test:8443/mobile-phones");
        usersHandler.RequestUri!.GetLeftPart(UriPartial.Path)
            .ShouldBe("https://gateway.example.test:8443/users-documentation/flows");
        ordersHandler.RequestUri!.GetLeftPart(UriPartial.Path)
            .ShouldBe("https://gateway.example.test:8443/orders-documentation/flows");
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }
    }
}
