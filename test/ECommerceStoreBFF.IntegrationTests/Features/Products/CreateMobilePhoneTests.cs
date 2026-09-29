using ECommerceStoreBFF.AcceptanceTests;
using Shouldly;
using System.Net;
using System.Net.Http.Json;

namespace ECommerceStoreBFF.IntegrationTests.Features.Products;

[Collection("Api Test Collection")]
public class CreateMobilePhoneTests(ApplicationFactory factory)
{
    [Fact]
    public async Task CreateMobilePhone_WithValidData_ReturnsCreatedPhone()
    {
        using var httpClient = factory.CreateClient();
        var client = ProductTestData.Client(httpClient);
        var name = ProductTestData.UniqueName();

        var created = await ProductTestData.CreatePhoneAsync(client, name);

        created.CommonDescription.ShouldNotBeNull();
        created.CommonDescription.Name.ShouldBe(name);
        created.CommonDescription.Brand.ShouldBe("Xiaomi");
        created.IsActive.ShouldBe(true);
    }

    [Fact]
    public async Task CreateMobilePhone_WithMissingName_ReturnsBadRequest()
    {
        using var client = factory.CreateClient();
        var invalidRequest = ProductTestData.CreateRequest(string.Empty);

        using var response = await client.PostAsJsonAsync("/mobile-phones", invalidRequest);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var rawJson = await response.Content.ReadAsStringAsync();
        rawJson.ShouldContain("Name", Case.Insensitive);
    }
}
