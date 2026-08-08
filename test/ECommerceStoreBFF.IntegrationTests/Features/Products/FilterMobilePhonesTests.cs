using ECommerceStoreBFF.AcceptanceTests;
using ECommerceStoreBFF.Infrastructure.Generated.Products;
using ECommerceStoreBFF.Infrastructure.Generated.Products.Models;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Shouldly;

namespace ECommerceStoreBFF.IntegrationTests.Features.Products
{
    public class FilterMobilePhonesTests : IClassFixture<ApplicationFactory>
    {
        private readonly ApplicationFactory _factory;

        public FilterMobilePhonesTests(ApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task FilterMobilePhones_ByBrand_ShouldReturnOnlyPhonesFromThatBrand_200()
        {
            // Arrange
            var httpClient = _factory.CreateClient();
            var adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: httpClient);
            var client = new ProductsApiClient(adapter);

            var filterRequest = new MobilePhoneFilterDto
            {
                Brand = MobilePhonesBrand.Apple,
                MinimalPrice = null,
                MaximalPrice = null
            };

            // Act
            var response = await client.MobilePhones.Filter.PostAsync(filterRequest);

            // Assert
            response.ShouldNotBeNull();
            response.ShouldNotBeEmpty();

            foreach (var phone in response)
            {
                phone.Brand.ShouldBe("Apple");
            }
        }

        [Fact]
        public async Task FilterMobilePhones_ByPriceRange_ShouldReturnExactlyTwoPhones_200()
        {
            // Arrange
            var httpClient = _factory.CreateClient();
            var adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: httpClient);
            var client = new ProductsApiClient(adapter);

            var filterRequest = new MobilePhoneFilterDto
            {
                Brand = null,
                MinimalPrice = 2000.00d,
                MaximalPrice = 3000.00d
            };

            // Act
            var response = await client.MobilePhones.Filter.PostAsync(filterRequest);

            // Assert
            response.ShouldNotBeNull();
            response.ShouldNotBeEmpty();

            response.Count.ShouldBe(2);

            foreach (var phone in response)
            {
                phone.Price.ShouldNotBeNull();
                phone.Price.Amount.ShouldNotBeNull();

                phone.Price.Amount.Value.ShouldBeGreaterThanOrEqualTo(2000.00d);
                phone.Price.Amount.Value.ShouldBeLessThanOrEqualTo(3000.00d);
            }
        }
    }
}