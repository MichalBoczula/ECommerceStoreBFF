using ECommerceStoreBFF.AcceptanceTests;
using ECommerceStoreBFF.Infrastructure.Generated.Products;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Shouldly;

namespace ECommerceStoreBFF.IntegrationTests.Features.Products;

public class GetMobilePhonesTests : IClassFixture<ApplicationFactory>
{
    private readonly ApplicationFactory _factory;

    public GetMobilePhonesTests(ApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetMobilePhones_ShouldReturnList_200()
    {
        // Arrange
        var httpClient = _factory.CreateClient();
        var adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: httpClient);
        var client = new ProductsApiClient(adapter);

        // Act
        var response = await client.MobilePhones.GetAsync(requestConfiguration =>
        {
            requestConfiguration.QueryParameters.Amount = 15;
        });

        // Assert
        response.ShouldNotBeNull();
        response.ShouldNotBeEmpty();
        response.Count.ShouldBe(15);
    }

    [Fact]
    public async Task GetTopMobilePhones_ShouldReturnList_200()
    {
        // Arrange
        var httpClient = _factory.CreateClient();
        var adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: httpClient);
        var client = new ProductsApiClient(adapter);

        // Act
        var response = await client.MobilePhones.Top.GetAsync();

        // Assert
        response.ShouldNotBeNull();
    }
}