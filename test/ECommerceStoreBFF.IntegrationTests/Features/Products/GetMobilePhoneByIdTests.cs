using ECommerceStoreBFF.AcceptanceTests;
using Shouldly;
using System.Net;
using System.Net.Http.Json;

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
    public async Task GetMobilePhonesByIds_ReturnsOnlyRequestedPhones()
    {
        using var httpClient = factory.CreateClient();
        var client = ProductTestData.Client(httpClient);
        var first = await ProductTestData.CreatePhoneAsync(client);
        var second = await ProductTestData.CreatePhoneAsync(client);

        var response = await client.MobilePhones.ByIds.PostAsync([second.Id, first.Id]);

        response.ShouldNotBeNull();
        response.Count.ShouldBe(2);
        response.ShouldContain(phone => phone.Id == first.Id);
        response.ShouldContain(phone => phone.Id == second.Id);
    }

    [Fact]
    public async Task GetMobilePhonesByIds_WhenOneIsMissing_ReturnsNotFound()
    {
        using var httpClient = factory.CreateClient();
        var client = ProductTestData.Client(httpClient);
        var created = await ProductTestData.CreatePhoneAsync(client);

        using var response = await httpClient.PostAsJsonAsync(
            "/mobile-phones/by-ids", new[] { created.Id!.Value, Guid.NewGuid() });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
