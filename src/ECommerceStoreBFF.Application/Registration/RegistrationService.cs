namespace ECommerceStoreBFF.Application.Registration;

public interface ICustomerRegistrationGateway
{
    Task<UpstreamResult<RegisteredCustomer>> FindAsync(string externalId, CancellationToken cancellationToken);
    Task<UpstreamResult<RegisteredCustomer>> CreateAsync(RegisterCustomerRequest request, CancellationToken cancellationToken);
}

public interface IShoppingCartRegistrationGateway
{
    Task<UpstreamResult<ShoppingCart>> FindAsync(Guid clientId, CancellationToken cancellationToken);
    Task<UpstreamResult<ShoppingCart>> CreateAsync(Guid clientId, CancellationToken cancellationToken);
}

public sealed class RegistrationService(
    ICustomerRegistrationGateway customers,
    IShoppingCartRegistrationGateway carts)
{
    public async Task<RegistrationResult> RegisterAsync(RegisterCustomerRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ExternalId))
            return RegistrationResult.Failure(400);

        var found = await customers.FindAsync(request.ExternalId, cancellationToken);
        if (found.Status != 404 && !found.IsSuccess) return Failure(found);

        var customer = found.Value;
        if (found.Status == 404)
        {
            var created = await customers.CreateAsync(request, cancellationToken);
            if (created.Status == 409)
            {
                // Another attempt may have persisted the customer before this one.
                found = await customers.FindAsync(request.ExternalId, cancellationToken);
                if (!found.IsSuccess) return Failure(found);
                customer = found.Value;
            }
            else if (!created.IsSuccess) return Failure(created);
            else customer = created.Value;
        }

        if (customer is null || customer.Id == Guid.Empty || !Matches(customer, request))
            return RegistrationResult.Failure(409);

        return await EnsureCartAsync(customer, cancellationToken);
    }

    // Explicit repair for profiles created before registration orchestration existed.
    public async Task<RegistrationResult> RepairCartAsync(string externalId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalId)) return RegistrationResult.Failure(400);
        var found = await customers.FindAsync(externalId, cancellationToken);
        return found.IsSuccess && found.Value is not null
            ? await EnsureCartAsync(found.Value, cancellationToken)
            : Failure(found);
    }

    private async Task<RegistrationResult> EnsureCartAsync(RegisteredCustomer customer, CancellationToken cancellationToken)
    {
        var found = await carts.FindAsync(customer.Id, cancellationToken);
        if (found.IsSuccess) return VerifyCart(customer, found.Value);
        if (found.Status != 404) return Failure(found);

        var created = await carts.CreateAsync(customer.Id, cancellationToken);
        if (created.IsSuccess) return VerifyCart(customer, created.Value);
        if (created.Status != 409) return Failure(created);

        // A concurrent retry may have created the cart after our 404.
        found = await carts.FindAsync(customer.Id, cancellationToken);
        return found.IsSuccess ? VerifyCart(customer, found.Value) : Failure(found);
    }

    private static RegistrationResult VerifyCart(RegisteredCustomer customer, ShoppingCart? cart) =>
        cart is not null && cart.ClientId == customer.Id && cart.Id != Guid.Empty
            ? RegistrationResult.Success(customer)
            : RegistrationResult.Failure(502);

    private static RegistrationResult Failure<T>(UpstreamResult<T> result) where T : class =>
        RegistrationResult.Failure(result.Status is >= 400 and < 500 ? result.Status : 502,
            result.Status is >= 400 and < 500 ? result.ErrorBody : null);

    private static bool Matches(RegisteredCustomer customer, RegisterCustomerRequest request)
    {
        if (customer.ExternalId != request.ExternalId || customer.Individual != request.Individual) return false;
        var companies = request.Companies ?? [];
        return customer.Companies.Count == companies.Count &&
            companies.All(company => customer.Companies.Any(saved =>
                saved.TaxId == company.TaxId && saved.CompanyName == company.CompanyName &&
                saved.BillingAddress == company.BillingAddress && saved.ShippingAddress == company.ShippingAddress));
    }
}
