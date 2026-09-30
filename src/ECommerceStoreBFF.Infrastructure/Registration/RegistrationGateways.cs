using System.Net.Http.Json;
using ECommerceStoreBFF.Application.Registration;

namespace ECommerceStoreBFF.Infrastructure.Registration;

public sealed class CustomerRegistrationGateway(HttpClient client) : ICustomerRegistrationGateway
{
    public async Task<UpstreamResult<RegisteredCustomer>> FindAsync(
        string externalId, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            $"customers/external/{Uri.EscapeDataString(externalId)}", cancellationToken);
        return await RegistrationResponse.ReadAsync<RegisteredCustomer>(response, cancellationToken);
    }

    public async Task<UpstreamResult<RegisteredCustomer>> CreateAsync(
        RegisterCustomerRequest request, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync("customers",
            request with { Companies = request.Companies ?? [] }, cancellationToken);
        return await RegistrationResponse.ReadAsync<RegisteredCustomer>(response, cancellationToken);
    }
}

public sealed class ShoppingCartRegistrationGateway(HttpClient client) : IShoppingCartRegistrationGateway
{
    public async Task<UpstreamResult<ShoppingCart>> FindAsync(Guid clientId, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync($"shopping-carts/client/{clientId}", cancellationToken);
        return await RegistrationResponse.ReadAsync<ShoppingCart>(response, cancellationToken);
    }

    public async Task<UpstreamResult<ShoppingCart>> CreateAsync(Guid clientId, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsync($"shopping-carts/{clientId}", null, cancellationToken);
        return await RegistrationResponse.ReadAsync<ShoppingCart>(response, cancellationToken);
    }
}

internal static class RegistrationResponse
{
    public static async Task<UpstreamResult<T>> ReadAsync<T>(
        HttpResponseMessage response, CancellationToken cancellationToken) where T : class
    {
        if (!response.IsSuccessStatusCode)
            return new UpstreamResult<T>((int)response.StatusCode,
                ErrorBody: await response.Content.ReadAsStringAsync(cancellationToken));

        var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        return new UpstreamResult<T>((int)response.StatusCode, value);
    }
}
