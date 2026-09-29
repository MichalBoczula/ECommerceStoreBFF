using ECommerceStoreBFF.AcceptanceTests;
using Shouldly;
using System.Net;
using System.Net.Http.Json;

namespace ECommerceStoreBFF.IntegrationTests.Features.Products;

[Collection("Api Test Collection")]
public class UpdateMobilePhoneTests(ApplicationFactory factory)
{
    [Fact]
    public async Task UpdateMobilePhone_WithValidData_ReturnsUpdatedPhone()
    {
        using var httpClient = factory.CreateClient();
        var client = ProductTestData.Client(httpClient);
        var created = await ProductTestData.CreatePhoneAsync(client);
        var newName = ProductTestData.UniqueName();

        var response = await client.MobilePhones[created.Id!.Value]
            .PutAsync(ProductTestData.UpdateRequest(newName));

        response.ShouldNotBeNull();
        response.Id.ShouldBe(created.Id);
        response.CommonDescription.ShouldNotBeNull();
        response.CommonDescription.Name.ShouldBe(newName);
        response.Price.ShouldNotBeNull();
        response.Price.Amount.ShouldBe(1999);
        response.ElectronicDetails.ShouldNotBeNull();
        response.ElectronicDetails.Ram.ShouldBe("12 GB");
    }

    [Fact]
    public async Task UpdateMobilePhone_WithMissingName_ReturnsBadRequest()
    {
        using var client = factory.CreateClient();
        var api = ProductTestData.Client(client);
        var created = await ProductTestData.CreatePhoneAsync(api);
        var invalidRequest = ProductTestData.UpdateRequest(string.Empty);

        using var response = await client.PutAsJsonAsync($"/mobile-phones/{created.Id}", invalidRequest);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var rawJson = await response.Content.ReadAsStringAsync();
        rawJson.ShouldContain("Name", Case.Insensitive);
    }
}
