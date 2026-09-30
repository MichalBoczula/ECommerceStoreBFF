using ECommerceStoreBFF.Infrastructure.Generated.Orders;
using ECommerceStoreBFF.Application.Registration;
using ECommerceStoreBFF.Infrastructure.Generated.Payments;
using ECommerceStoreBFF.Infrastructure.Generated.Products;
using ECommerceStoreBFF.Infrastructure.Generated.Users;
using ECommerceStoreBFF.Infrastructure.Registration;
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
        }).AddTypedClient((httpClient, _) =>
        {
            var adapter = new HttpClientRequestAdapter(authProvider, httpClient: httpClient);
            adapter.BaseUrl = httpClient.BaseAddress!.ToString().TrimEnd('/');
            return new ProductsApiClient(adapter);
        });

        services.AddHttpClient<UsersApiClient>(client =>
        {
            client.BaseAddress = new Uri(bffBaseUrl);
        }).AddTypedClient((httpClient, _) =>
        {
            var adapter = new HttpClientRequestAdapter(authProvider, httpClient: httpClient);
            adapter.BaseUrl = httpClient.BaseAddress!.ToString().TrimEnd('/');
            return new UsersApiClient(adapter);
        });

        services.AddHttpClient<OrdersApiClient>(client =>
        {
            client.BaseAddress = new Uri(bffBaseUrl);
        }).AddTypedClient((httpClient, _) =>
        {
            var adapter = new HttpClientRequestAdapter(authProvider, httpClient: httpClient);
            adapter.BaseUrl = httpClient.BaseAddress!.ToString().TrimEnd('/');
            return new OrdersApiClient(adapter);
        });

        services.AddHttpClient<PaymentsApiClient>(client =>
        {
            client.BaseAddress = new Uri(bffBaseUrl);
        }).AddTypedClient((httpClient, _) =>
        {
            var adapter = new HttpClientRequestAdapter(authProvider, httpClient: httpClient);
            adapter.BaseUrl = httpClient.BaseAddress!.ToString().TrimEnd('/');
            return new PaymentsApiClient(adapter);
        });

        services.AddScoped<RegistrationService>();
        services.AddHttpClient<ICustomerRegistrationGateway, CustomerRegistrationGateway>(client =>
            client.BaseAddress = UpstreamAddress(configuration, "users-cluster"));
        services.AddHttpClient<IShoppingCartRegistrationGateway, ShoppingCartRegistrationGateway>(client =>
            client.BaseAddress = configuration["Registration:InvoiceBaseUrl"] is { } overrideAddress
                ? new Uri(overrideAddress.TrimEnd('/') + "/", UriKind.Absolute)
                : UpstreamAddress(configuration, "orders-cluster"));

        return services;
    }

    private static Uri UpstreamAddress(IConfiguration configuration, string cluster)
    {
        var address = configuration[$"ReverseProxy:Clusters:{cluster}:Destinations:destination1:Address"]
            ?? throw new InvalidOperationException($"Missing upstream address for {cluster}.");
        return new Uri(address.TrimEnd('/') + "/", UriKind.Absolute);
    }
}
