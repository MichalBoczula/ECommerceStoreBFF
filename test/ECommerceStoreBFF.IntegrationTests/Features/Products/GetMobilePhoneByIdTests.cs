using ECommerceStoreBFF.AcceptanceTests;
using ECommerceStoreBFF.Infrastructure.Generated.Products;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Shouldly;
using System.Net;

namespace ECommerceStoreBFF.IntegrationTests.Features.Products;

public class GetMobilePhoneByIdTests : IClassFixture<ApplicationFactory>
{
    private readonly ApplicationFactory _factory;

    private static readonly Guid ExistingMobilePhoneId = Guid.Parse("0f62c3e1-8e3e-4b1f-9d74-3d6e2ff2c6d2");

    public GetMobilePhoneByIdTests(ApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetMobilePhoneById_WhenExists_ShouldReturnDetails_200()
    {
        // Arrange
        var httpClient = _factory.CreateClient();
        var adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: httpClient);
        var client = new ProductsApiClient(adapter);

        // Act
        var response = await client.MobilePhones[ExistingMobilePhoneId.ToString()].GetAsync();

        // Assert
        response.ShouldNotBeNull();

        // Assert
        response.Id.ShouldBe(ExistingMobilePhoneId);
        response.IsActive?.ShouldBeTrue();

        response.CommonDescription.ShouldNotBeNull();
        response.CommonDescription.Name.ShouldBe("Xiaomi POCO F7 12/512GB Black");
        response.CommonDescription.Brand.ShouldBe("Xiaomi");

        response.ElectronicDetails.ShouldNotBeNull();
        response.ElectronicDetails.Ram.ShouldBe("12 GB");

        response.Price.ShouldNotBeNull();
        response.Price.Amount.ShouldBe(2499.00d);
        response.Price.Currency.ShouldBe("PLN");
    }

    [Fact]
    public async Task GetMobilePhoneById_WhenNotExists_ShouldReturnNotFound_404()
    {
        // Arrange
        var client = _factory.CreateClient();
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/mobile-phones/{nonExistentId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}