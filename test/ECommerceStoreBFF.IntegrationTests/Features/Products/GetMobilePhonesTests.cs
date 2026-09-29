using ECommerceStoreBFF.AcceptanceTests;
using Shouldly;

namespace ECommerceStoreBFF.IntegrationTests.Features.Products;

[Collection("Api Test Collection")]
public class GetMobilePhonesTests(ApplicationFactory factory)
{
    [Fact]
    public async Task GetMobilePhones_ContainsOwnPhone()
    {
        using var httpClient = factory.CreateClient();
        var client = ProductTestData.Client(httpClient);
        var created = await ProductTestData.CreatePhoneAsync(client);

        var response = await client.MobilePhones.GetAsync(options => options.QueryParameters.Amount = 1000);

        response.ShouldNotBeNull();
        response.ShouldContain(phone => phone.Id == created.Id);
    }

    [Fact]
    public async Task GetTopMobilePhones_ContainsRecentlyCreatedPhone()
    {
        using var httpClient = factory.CreateClient();
        var client = ProductTestData.Client(httpClient);
        var created = await ProductTestData.CreatePhoneAsync(client);

        var response = await client.MobilePhones.Top.GetAsync();

        response.ShouldNotBeNull();
        response.Count.ShouldBeLessThanOrEqualTo(3);
        response.ShouldContain(phone => phone.Id == created.Id);
    }
}
