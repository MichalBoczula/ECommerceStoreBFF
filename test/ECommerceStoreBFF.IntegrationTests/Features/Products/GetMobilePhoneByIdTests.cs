using ECommerceStoreBFF.AcceptanceTests;
using Shouldly;
using System.Net;

namespace ECommerceStoreBFF.IntegrationTests.Features.Products;

[Collection("Api Test Collection")]
public class GetMobilePhoneByIdTests(ApplicationFactory factory)
{
    [Fact]
    public async Task GetMobilePhoneById_WhenExists_ReturnsOwnPhone()
    {
        using var httpClient = factory.CreateClient();
        var client = ProductTestData.Client(httpClient);
        var name = ProductTestData.UniqueName();
        var created = await ProductTestData.CreatePhoneAsync(client, name);

        var response = await client.MobilePhones[created.Id!.Value].GetAsync();

        response.ShouldNotBeNull();
        response.Id.ShouldBe(created.Id);
        response.CommonDescription.ShouldNotBeNull();
        response.CommonDescription.Name.ShouldBe(name);
        response.ElectronicDetails.ShouldNotBeNull();
        response.Price.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetMobilePhoneById_WhenNotExists_ReturnsNotFound()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/mobile-phones/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMobilePhonesByIds_ReturnsOnlyRequestedExistingPhone()
    {
        using var httpClient = factory.CreateClient();
        var client = ProductTestData.Client(httpClient);
        var created = await ProductTestData.CreatePhoneAsync(client);

        var response = await client.MobilePhones.ByIds.PostAsync([created.Id, Guid.NewGuid()]);

        response.ShouldNotBeNull();
        response.Count.ShouldBe(1);
        response[0].Id.ShouldBe(created.Id);
    }
}
