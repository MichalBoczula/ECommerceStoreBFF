using ECommerceStoreBFF.AcceptanceTests;
using ECommerceStoreBFF.Infrastructure.Generated.Users.Models;
using Shouldly;
using System.Net;
using System.Net.Http.Json;

namespace ECommerceStoreBFF.IntegrationTests.Features.Users;

[Collection("Api Test Collection")]
public class CustomerTests(ApplicationFactory factory)
{
    [Fact]
    public async Task CreateAndReadCustomer_ByExternalId_ReturnsOwnProfile()
    {
        using var httpClient = factory.CreateClient();
        var api = UsersTestData.Client(httpClient);
        var externalId = UsersTestData.ExternalId();

        var created = await UsersTestData.CreateCustomerAsync(api, externalId);
        var read = await api.Customers.External[externalId].GetAsync();

        created.ExternalId.ShouldBe(externalId);
        created.Individual.ShouldNotBeNull();
        created.Individual.FirstName.ShouldBe("Jan");
        read.ShouldNotBeNull();
        read.Id.ShouldBe(created.Id);
        read.ExternalId.ShouldBe(externalId);
        read.Individual.ShouldNotBeNull();
        read.Individual.Email.ShouldBe(created.Individual.Email);
    }

    [Fact]
    public async Task UpdateIndividual_ChangesOnlyOwnCustomer()
    {
        using var httpClient = factory.CreateClient();
        var api = UsersTestData.Client(httpClient);
        var externalId = UsersTestData.ExternalId();
        var created = await UsersTestData.CreateCustomerAsync(api, externalId);
        var request = new UpdateIndividualDataRequestDto
        {
            Individual = UsersTestData.IndividualRequest("Anna")
        };

        var updated = await api.Customers[created.Id!.Value].Individual.PutAsync(request);
        var read = await api.Customers.External[externalId].GetAsync();

        updated.ShouldNotBeNull();
        updated.Id.ShouldBe(created.Id);
        updated.Individual.ShouldNotBeNull();
        updated.Individual.FirstName.ShouldBe("Anna");
        read.ShouldNotBeNull();
        read.Individual.ShouldNotBeNull();
        read.Individual.FirstName.ShouldBe("Anna");
    }

    [Fact]
    public async Task CreateCustomer_WithInvalidEmail_ReturnsValidationProblem()
    {
        using var httpClient = factory.CreateClient();
        var request = UsersTestData.CustomerRequest(UsersTestData.ExternalId());
        request.Individual!.Email = "invalid-email";

        using var response = await httpClient.PostAsJsonAsync("/customers", request);

        await UsersTestData.AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed");
        (await response.Content.ReadAsStringAsync()).ShouldContain("Email", Case.Insensitive);
    }

    [Fact]
    public async Task ReadCustomer_WithUnknownExternalId_ReturnsNotFound()
    {
        using var httpClient = factory.CreateClient();

        using var response = await httpClient.GetAsync($"/customers/external/{UsersTestData.ExternalId()}");

        await UsersTestData.AssertProblemAsync(response, HttpStatusCode.NotFound, "resource_not_found");
    }

    [Fact]
    public async Task CreateCustomer_WithDuplicateExternalId_ReturnsConflict()
    {
        using var httpClient = factory.CreateClient();
        var api = UsersTestData.Client(httpClient);
        var externalId = UsersTestData.ExternalId();
        await UsersTestData.CreateCustomerAsync(api, externalId);

        using var response = await httpClient.PostAsJsonAsync(
            "/customers", UsersTestData.CustomerRequest(externalId));

        await UsersTestData.AssertProblemAsync(response, HttpStatusCode.Conflict, "resource_conflict");
    }
}
