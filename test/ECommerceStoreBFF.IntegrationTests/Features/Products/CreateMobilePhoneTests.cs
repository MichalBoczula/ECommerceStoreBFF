using ECommerceStoreBFF.AcceptanceTests;
using ECommerceStoreBFF.Infrastructure.Generated.Products;
using ECommerceStoreBFF.Infrastructure.Generated.Products.
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Abstractions;
using Shouldly;
using System.Net;
using Xunit;

namespace ECommerceStoreBFF.IntegrationTests.Features.Products;

public class CreateMobilePhoneTests : IClassFixture<ApplicationFactory>
{
    private readonly ApplicationFactory _factory;

    public CreateMobilePhoneTests(ApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateMobilePhone_WithValidData_ShouldReturnOk_200()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<ProductsApiClient>();

        // ⚠️ Use the Kiota-generated model for the request body
        var validRequest = new CreateMobilePhoneCommand
        {
            Name = "Samsung Galaxy S26",
            Description = "Latest flagship phone.",
            Price = 1199.99d,
            Brand = "Samsung",
            StockQuantity = 50
        };

        // Act
        // Kiota will automatically throw if the response is not 2xx.
        // If it successfully returns the object, we know it was a 200/201 Success!
        var response = await client.MobilePhones.PostAsync(validRequest);

        // Assert
        response.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateMobilePhone_WithMissingName_ShouldThrowBadRequest_400()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<ProductsApiClient>();

        var invalidRequest = new CreateMobilePhoneCommand
        {
            // Name is missing
            Description = "A phone with no name.",
            Price = 500.00d,
            Brand = "Samsung",
            StockQuantity = 10
        };

        // Act & Assert
        // 1. Catch the generic ApiException (or the specific one Kiota generated for your 400 response)
        var exception = await Should.ThrowAsync<ApiException>(async () =>
        {
            await client.MobilePhones.PostAsync(invalidRequest);
        });

        // 2. Verify it's a 400 Bad Request
        exception.ResponseStatusCode.ShouldBe((int)HttpStatusCode.BadRequest);

        // 3. Inspect the error message/content
        // Note: If your OpenAPI spec defines the 400 response schema, Kiota likely generated 
        // a specific Exception type for it (e.g., `ValidationException`). 
        // If so, change `ApiException` above to your specific type and assert on its properties!
        exception.Message.ShouldContain("Name", Case.Insensitive);
    }

    [Fact]
    public async Task CreateMobilePhone_WithNegativePrice_ShouldThrowBadRequest_400()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<ProductsApiClient>();

        var invalidRequest = new CreateMobilePhoneCommand
        {
            Name = "Google Pixel 10",
            Description = "Google smartphone",
            Price = -50.00d, // ❌ Invalid Price
            Brand = "Google",
            StockQuantity = 20
        };

        // Act & Assert
        var exception = await Should.ThrowAsync<ApiException>(async () =>
        {
            await client.MobilePhones.PostAsync(invalidRequest);
        });

        exception.ResponseStatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        exception.Message.ShouldContain("Price", Case.Insensitive);
    }
}