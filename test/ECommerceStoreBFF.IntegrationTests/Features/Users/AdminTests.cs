using ECommerceStoreBFF.AcceptanceTests;
using ECommerceStoreBFF.Infrastructure.Generated.Users.Models;
using Shouldly;
using System.Net;
using System.Net.Http.Json;

namespace ECommerceStoreBFF.IntegrationTests.Features.Users;

[Collection("Api Test Collection")]
public class AdminTests(ApplicationFactory factory)
{
    [Fact]
    public async Task CreateAndReadAdmin_ByExternalId_ReturnsOwnProfile()
    {
        using var httpClient = factory.CreateClient();
        var api = UsersTestData.Client(httpClient);
        var externalId = UsersTestData.ExternalId();

        var created = await UsersTestData.CreateAdminAsync(api, externalId);
        var read = await api.Admins.External[externalId].GetAsync();

        created.ExternalId.ShouldBe(externalId);
        created.FullName.ShouldBe("Admin Testowy");
        created.IsActive.ShouldBe(true);
        read.ShouldNotBeNull();
        read.Id.ShouldBe(created.Id);
        read.Email.ShouldBe(created.Email);
    }

    [Fact]
    public async Task UpdateAdmin_ChangesOwnProfile()
    {
        using var httpClient = factory.CreateClient();
        var api = UsersTestData.Client(httpClient);
        var externalId = UsersTestData.ExternalId();
        var created = await UsersTestData.CreateAdminAsync(api, externalId);
        var newEmail = $"{Guid.NewGuid():N}@example.com";

        var updated = await api.Admins[created.Id!.Value].PutAsync(new UpdateAdminProfileRequestDto
        {
            FullName = "Admin Zmieniony", Email = newEmail
        });
        var read = await api.Admins.External[externalId].GetAsync();

        updated.ShouldNotBeNull();
        updated.Id.ShouldBe(created.Id);
        updated.FullName.ShouldBe("Admin Zmieniony");
        read.ShouldNotBeNull();
        read.Email.ShouldBe(newEmail);
    }

    [Fact]
    public async Task CreateAdmin_WithInvalidEmail_ReturnsValidationProblem()
    {
        using var httpClient = factory.CreateClient();
        var request = UsersTestData.AdminRequest(UsersTestData.ExternalId());
        request.Email = "invalid-email";

        using var response = await httpClient.PostAsJsonAsync("/admins", request);

        await UsersTestData.AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed");
        (await response.Content.ReadAsStringAsync()).ShouldContain("Email", Case.Insensitive);
    }

    [Fact]
    public async Task ReadAdmin_WithUnknownExternalId_ReturnsNotFound()
    {
        using var httpClient = factory.CreateClient();

        using var response = await httpClient.GetAsync($"/admins/external/{UsersTestData.ExternalId()}");

        await UsersTestData.AssertProblemAsync(response, HttpStatusCode.NotFound, "resource_not_found");
    }

    [Fact]
    public async Task CreateAdmin_WithDuplicateExternalId_ReturnsConflict()
    {
        using var httpClient = factory.CreateClient();
        var api = UsersTestData.Client(httpClient);
        var externalId = UsersTestData.ExternalId();
        await UsersTestData.CreateAdminAsync(api, externalId);

        using var response = await httpClient.PostAsJsonAsync(
            "/admins", UsersTestData.AdminRequest(externalId));

        await UsersTestData.AssertProblemAsync(response, HttpStatusCode.Conflict, "resource_conflict");
    }
}
