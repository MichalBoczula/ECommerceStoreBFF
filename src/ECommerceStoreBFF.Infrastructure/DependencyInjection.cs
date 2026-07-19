using ECommerceStoreBFF.Infrastructure.Generated.Orders;
using ECommerceStoreBFF.Infrastructure.Generated.Products;
using ECommerceStoreBFF.Infrastructure.Generated.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace ECommerceStoreBFF.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        var authProvider = new AnonymousAuthenticationProvider();

        services.AddScoped(sp =>
        {
            var adapter = new HttpClientRequestAdapter(authProvider,
                httpClient: new HttpClient { BaseAddress = new Uri("http://localhost:5000") });
            return new ProductsApiClient(adapter);
        });

        services.AddScoped(sp =>
        {
            var adapter = new HttpClientRequestAdapter(authProvider,
                httpClient: new HttpClient { BaseAddress = new Uri("http://localhost:6500") });
            return new UsersApiClient(adapter);
        });

        services.AddScoped(sp =>
        {
            var adapter = new HttpClientRequestAdapter(authProvider,
                httpClient: new HttpClient { BaseAddress = new Uri("http://localhost:7000") });
            return new OrdersApiClient(adapter);
        });

        return services;
    }
}