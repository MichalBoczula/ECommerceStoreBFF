using ECommerceStoreBFF.Infrastructure.Generated.Users;
using ECommerceStoreBFF.Infrastructure.Generated.Users.Models;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Shouldly;
using System.Net;
using System.Text.Json;

namespace ECommerceStoreBFF.IntegrationTests.Features.Users;

internal static class UsersTestData
{
    public static string ExternalId() => $"bff-{Guid.NewGuid():N}";

    public static UsersApiClient Client(HttpClient httpClient)
    {
        var adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: httpClient)
        {
            BaseUrl = httpClient.BaseAddress!.ToString().TrimEnd('/')
        };
        return new UsersApiClient(adapter);
    }

    public static CreateCustomerRequestDto CustomerRequest(string externalId) => new()
    {
        ExternalId = externalId,
        Individual = IndividualRequest()
    };

    public static IndividualDataRequestDto IndividualRequest(string firstName = "Jan") => new()
    {
        FirstName = firstName,
        LastName = "Kowalski",
        Email = $"{Guid.NewGuid():N}@example.com",
        Phone = "123456789",
        BillingAddress = Address(),
        ShippingAddress = Address()
    };

    public static CreateAdminRequestDto AdminRequest(string externalId) => new()
    {
        ExternalId = externalId,
        FullName = "Admin Testowy",
        Email = $"{Guid.NewGuid():N}@example.com"
    };

    public static async Task<CustomerResponseDto> CreateCustomerAsync(UsersApiClient client, string externalId)
    {
        var created = await client.Customers.PostAsync(CustomerRequest(externalId));
        created.ShouldNotBeNull();
        created.Id.ShouldNotBeNull();
        return created;
    }

    public static async Task<AdminResponseDto> CreateAdminAsync(UsersApiClient client, string externalId)
    {
        var created = await client.Admins.PostAsync(AdminRequest(externalId));
        created.ShouldNotBeNull();
        created.Id.ShouldNotBeNull();
        return created;
    }

    public static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.ShouldBe(status);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotBeNullOrWhiteSpace();
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("status").GetInt32().ShouldBe((int)status);
        json.RootElement.GetProperty("code").GetString().ShouldBe(code);
    }

    private static AddressRequestDto Address() => new()
    {
        PostalCode = "00-001",
        City = "Warsaw",
        Street = "Main Street",
        BuildingNumber = "10",
        ApartmentNumber = "2"
    };
}
