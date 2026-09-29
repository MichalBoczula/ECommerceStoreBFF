using ECommerceStoreBFF.AcceptanceTests;
using ECommerceStoreBFF.Infrastructure.Generated.Products.Models;
using Shouldly;

namespace ECommerceStoreBFF.IntegrationTests.Features.Products;

[Collection("Api Test Collection")]
public class FilterMobilePhonesTests(ApplicationFactory factory)
{
    [Fact]
    public async Task FilterMobilePhones_ByBrand_ContainsCreatedPhoneAndOnlyMatchingBrand()
    {
        using var httpClient = factory.CreateClient();
        var client = ProductTestData.Client(httpClient);
        var created = await ProductTestData.CreatePhoneAsync(client, brand: "Apple");

        var response = await client.MobilePhones.Filter.PostAsync(new MobilePhoneFilterDto
        {
            Brand = MobilePhonesBrand.Apple
        });

        response.ShouldNotBeNull();
        response.ShouldContain(phone => phone.Id == created.Id);
        response.ShouldAllBe(phone => phone.Brand == "Apple");
    }

    [Fact]
    public async Task FilterMobilePhones_ByPriceRange_ContainsCreatedPhoneWithinRange()
    {
        using var httpClient = factory.CreateClient();
        var client = ProductTestData.Client(httpClient);
        var price = Random.Shared.Next(20_000, 80_000);
        var created = await ProductTestData.CreatePhoneAsync(client, price: price);

        var response = await client.MobilePhones.Filter.PostAsync(new MobilePhoneFilterDto
        {
            MinimalPrice = price - 1,
            MaximalPrice = price + 1
        });

        response.ShouldNotBeNull();
        response.ShouldContain(phone => phone.Id == created.Id);
        response.ShouldAllBe(phone => phone.Price != null && phone.Price.Amount >= price - 1 && phone.Price.Amount <= price + 1);
    }
}
