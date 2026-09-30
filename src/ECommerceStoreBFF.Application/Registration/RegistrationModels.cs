namespace ECommerceStoreBFF.Application.Registration;

public sealed record AddressData(
    string PostalCode, string City, string Street, string BuildingNumber, string? ApartmentNumber);

public sealed record IndividualData(
    string FirstName, string LastName, string Email, string Phone,
    AddressData BillingAddress, AddressData ShippingAddress);

public sealed record CompanyData(
    string TaxId, string CompanyName, AddressData BillingAddress, AddressData ShippingAddress);

public sealed record RegisterCustomerRequest(
    string ExternalId, IndividualData Individual, IReadOnlyList<CompanyData>? Companies = null);

public sealed record RegisteredCompany(
    Guid Id, string TaxId, string CompanyName, AddressData BillingAddress, AddressData ShippingAddress);

public sealed record RegisteredCustomer(
    Guid Id, string ExternalId, IndividualData Individual,
    IReadOnlyList<RegisteredCompany> Companies, DateTimeOffset UpdatedAt);

public sealed record ShoppingCart(Guid Id, Guid ClientId, IReadOnlyList<ShoppingCartLine> Lines);
public sealed record ShoppingCartLine(Guid ProductId, int Quantity);

public sealed record UpstreamResult<T>(int Status, T? Value = default, string? ErrorBody = null)
    where T : class
{
    public bool IsSuccess => Status is >= 200 and < 300 && Value is not null;
}

public sealed record RegistrationResult(RegisteredCustomer? Customer, int Status, string? ErrorBody = null)
{
    public static RegistrationResult Success(RegisteredCustomer customer) => new(customer, 200);
    public static RegistrationResult Failure(int status, string? errorBody = null) => new(null, status, errorBody);
}
