using System.Globalization;
using System.Net;
using System.Text.Json;
using ECommerceStoreBFF.Application.Registration;
using ECommerceStoreBFF.Infrastructure.Registration;
using ECommerceStoreBFF.IntegrationTests.Common;
using PactNet;
using PactNet.Matchers;
using Shouldly;

namespace ECommerceStoreBFF.IntegrationTests.Features.Users;

public sealed class UsersConsumerPactTests
{
    private const string CustomersPath = "/customers";
    private const string Json = "application/json";
    private const string ProblemJson = "application/problem+json";
    private const string IdPattern = "^[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$";
    private const string UpdatedAt = "2026-01-01T00:00:00+00:00";
    private const string UpdatedAtPattern = @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})$";
    private const int InteractionCount = 4;
    private static readonly Guid CustomerId = Guid.Parse("47000000-0000-4000-8000-000000000001");
    private static readonly Guid CompanyId = Guid.Parse("47000000-0000-4000-8000-000000000002");
    private static readonly AddressData Billing = new("00-001", "Warsaw", "Main Street", "10", "2");
    private static readonly AddressData Shipping = new("00-002", "Warsaw", "Second Street", "11", null);
    private static readonly CompanyData Company = new("1234567890", "Pact Company", Billing, Shipping);
    private static readonly RegisterCustomerRequest Existing = new("bff-pact-existing",
        new IndividualData("Pact", "Existing", "bffexisting@example.com", "123456789", Billing, Shipping), [Company]);
    private static readonly RegisterCustomerRequest NewCustomer = new("bff-pact-new",
        new IndividualData("Pact", "New", "bffnew@example.com", "123456789", Billing, Shipping));
    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public Task CustomerRegistrationGatewayEmitsReproduciblePact() =>
        ConsumerPactArtifact.VerifyAndWriteAsync("users", InteractionCount, "BFF_PACT_DIR", VerifyAsync);

    private static async Task VerifyAsync(string directory)
    {
        var pact = Pact.V4("bff", "users", new PactConfig { PactDir = directory }).WithHttpInteractions();
        const string existingState = "the BFF registration customer exists";
        const string missingState = "the BFF registration customer is absent";
        var existingParameters = new Dictionary<string, string> { ["registrationJson"] = RequestJson(Existing) };
        var missingParameters = new Dictionary<string, string> { ["registrationJson"] = RequestJson(NewCustomer) };

        pact.UponReceiving("look up an existing registration customer")
            .Given(existingState, existingParameters)
            .WithRequest(HttpMethod.Get, $"/customers/external/{Existing.ExternalId}")
            .WillRespond().WithStatus(HttpStatusCode.OK).WithHeader("Content-Type", Json)
            .WithJsonBody(CustomerBody(Existing));

        pact.UponReceiving("look up a registration customer that does not exist")
            .Given(missingState, missingParameters)
            .WithRequest(HttpMethod.Get, $"/customers/external/{NewCustomer.ExternalId}")
            .WillRespond().WithStatus(HttpStatusCode.NotFound).WithHeader("Content-Type", ProblemJson)
            .WithBody("""{"status":404}""", ProblemJson);

        pact.UponReceiving("create a registration customer with an empty company list")
            .Given(missingState, missingParameters)
            .WithRequest(HttpMethod.Post, CustomersPath)
            .WithHeader("Content-Type", Json).WithBody(RequestJson(NewCustomer), Json)
            .WillRespond().WithStatus(HttpStatusCode.OK).WithHeader("Content-Type", Json)
            .WithJsonBody(CustomerBody(NewCustomer));

        pact.UponReceiving("receive a conflict when the registration customer was already created")
            .Given(existingState, existingParameters)
            .WithRequest(HttpMethod.Post, CustomersPath)
            .WithHeader("Content-Type", Json).WithBody(RequestJson(Existing), Json)
            .WillRespond().WithStatus(HttpStatusCode.Conflict).WithHeader("Content-Type", ProblemJson)
            .WithBody("""{"status":409}""", ProblemJson);

        await pact.VerifyAsync(async context =>
        {
            using var http = new HttpClient { BaseAddress = context.MockServerUri };
            var gateway = new CustomerRegistrationGateway(http);
            var existing = AssertCustomer(await gateway.FindAsync(Existing.ExternalId, CancellationToken.None), Existing);
            existing.Companies.ShouldHaveSingleItem().ShouldBe(
                new RegisteredCompany(CompanyId, Company.TaxId, Company.CompanyName, Billing, Shipping));
            AssertProblem(await gateway.FindAsync(NewCustomer.ExternalId, CancellationToken.None), HttpStatusCode.NotFound);
            AssertCustomer(await gateway.CreateAsync(NewCustomer, CancellationToken.None), NewCustomer).Companies.ShouldBeEmpty();
            AssertProblem(await gateway.CreateAsync(Existing, CancellationToken.None), HttpStatusCode.Conflict);
        });
    }

    private static string RequestJson(RegisterCustomerRequest request) =>
        JsonSerializer.Serialize(request with { Companies = request.Companies ?? [] }, WireJson);

    private static object AddressBody(AddressData address) => new
    {
        postalCode = address.PostalCode,
        city = address.City,
        street = address.Street,
        buildingNumber = address.BuildingNumber,
        apartmentNumber = address.ApartmentNumber
    };

    private static object CustomerBody(RegisterCustomerRequest request) => new
    {
        id = Match.Regex(CustomerId.ToString(), IdPattern),
        externalId = request.ExternalId,
        individual = new
        {
            firstName = request.Individual.FirstName,
            lastName = request.Individual.LastName,
            email = request.Individual.Email,
            phone = request.Individual.Phone,
            billingAddress = AddressBody(request.Individual.BillingAddress),
            shippingAddress = AddressBody(request.Individual.ShippingAddress)
        },
        companies = (request.Companies ?? []).Select(company => new
        {
            id = Match.Regex(CompanyId.ToString(), IdPattern),
            taxId = company.TaxId,
            companyName = company.CompanyName,
            billingAddress = AddressBody(company.BillingAddress),
            shippingAddress = AddressBody(company.ShippingAddress)
        }).ToArray(),
        updatedAt = Match.Regex(UpdatedAt, UpdatedAtPattern)
    };

    private static RegisteredCustomer AssertCustomer(UpstreamResult<RegisteredCustomer> result, RegisterCustomerRequest request)
    {
        result.Status.ShouldBe((int)HttpStatusCode.OK);
        var customer = result.Value.ShouldNotBeNull();
        customer.Id.ShouldBe(CustomerId);
        customer.ExternalId.ShouldBe(request.ExternalId);
        customer.Individual.ShouldBe(request.Individual);
        customer.UpdatedAt.ShouldBe(DateTimeOffset.Parse(UpdatedAt, CultureInfo.InvariantCulture));
        return customer;
    }

    private static void AssertProblem(UpstreamResult<RegisteredCustomer> result, HttpStatusCode expected)
    {
        result.Status.ShouldBe((int)expected);
        result.Value.ShouldBeNull();
        using var problem = JsonDocument.Parse(result.ErrorBody.ShouldNotBeNull());
        problem.RootElement.GetProperty("status").GetInt32().ShouldBe((int)expected);
    }
}
