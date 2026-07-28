using ECommerceStoreBFF.Infrastructure.Generated.Products;
using ECommerceStoreBFF.Infrastructure.Generated.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace ECommerceStoreBFF.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var bffBaseUrl = configuration["GatewaySettings:BaseUrl"]
            ?? throw new InvalidOperationException("Configuration error: 'GatewaySettings:BaseUrl' is missing in appsettings.json.");

        var authProvider = new AnonymousAuthenticationProvider();

        services.AddHttpClient<ProductsApiClient>(client =>
        {
            client.BaseAddress = new Uri(bffBaseUrl);
        }).AddTypedClient((httpClient, sp) =>
        {
            var adapter = new HttpClientRequestAdapter(authProvider, httpClient: httpClient);
            return new ProductsApiClient(adapter);
        });

        services.AddHttpClient<UsersApiClient>(client =>
        {
            client.BaseAddress = new Uri(bffBaseUrl);
        }).AddTypedClient((httpClient, sp) =>
            new UsersApiClient(new HttpClientRequestAdapter(authProvider, httpClient: httpClient)));

        return services;
    }
}