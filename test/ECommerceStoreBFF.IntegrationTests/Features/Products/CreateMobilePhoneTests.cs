using ECommerceStoreBFF.AcceptanceTests;
using ECommerceStoreBFF.Infrastructure.Generated.Products;
using ECommerceStoreBFF.Infrastructure.Generated.Products.Models;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Shouldly;
using System.Net;
using System.Net.Http.Json;

namespace ECommerceStoreBFF.IntegrationTests.Features.Products
{
    [Collection("Api Test Collection")]
    public class CreateMobilePhoneTests
    {
        private readonly ApplicationFactory _factory;
        private static readonly Guid MobileCategoryId = Guid.Parse("587480bb-c126-4f9b-b531-b0244daa4ba4");

        public CreateMobilePhoneTests(ApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task CreateMobilePhone_WithValidXiaomiData_ShouldReturnSuccess_200()
        {
            // Arrange
            var httpClient = _factory.CreateClient();
            var adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: httpClient);
            var client = new ProductsApiClient(adapter);

            var validRequest = CreateBaseValidRequest();

            // Act
            var response = await client.MobilePhones.PostAsync(validRequest);

            // Assert
            response.ShouldNotBeNull();
        }

        [Fact]
        public async Task CreateMobilePhone_WithMissingName_ShouldReturnBadRequest_400()
        {
            // Arrange
            var client = _factory.CreateClient();

            var invalidRequest = CreateBaseValidRequest();
            invalidRequest.CommonDescription.Name = string.Empty;

            // Act
            var response = await client.PostAsJsonAsync("/mobile-phones", invalidRequest);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            var rawJson = await response.Content.ReadAsStringAsync();

            rawJson.ShouldNotBeNullOrEmpty();
            rawJson.ShouldContain("Name", Case.Insensitive);
        }

        private static CreateMobilePhoneExternalDto CreateBaseValidRequest()
        {
            return new CreateMobilePhoneExternalDto
            {
                CategoryId = MobileCategoryId,
                Camera = "50 MP (Sony LYT-600, OIS) + 8 MP ultrawide, 20 MP front",
                FingerPrint = true,
                FaceId = false,
                Description2 = "Xiaomi POCO F7 has a 50 MP main camera with a Sony LYT-600 sensor...",
                Description3 = "POCO F7 has an elegant, flat body that is 8.2 mm thick...",

                CommonDescription = new CommonDescriptionExtrernalDto
                {
                    Name = "Xiaomi POCO F7 12/512GB Black",
                    Brand = "Xiaomi",
                    Description = "The POCO F7 display has a 2772 x 1280 px resolution...",
                    MainPhoto = "xiaomi-poco-f7-black-main.jpg",
                    OtherPhotos = new List<string> { "xiaomi-poco-f7-black-1.jpg", "xiaomi-poco-f7-black-2.jpg" }
                },
                ElectronicDetails = new CreateElectronicDetailsExternalDto
                {
                    Cpu = "Qualcomm Snapdragon 8s Gen 4",
                    Gpu = "Adreno",
                    Ram = "12 GB",
                    Storage = "512 GB",
                    DisplayType = "AMOLED",
                    RefreshRateHz = 120,
                    ScreenSizeInches = 6.83d,
                    Width = 78,
                    Height = 163,
                    BatteryType = "Li-Ion",
                    BatteryCapacity = 6500
                },
                Connectivity = new CreateConnectivityExternalDto
                {
                    Has5G = true,
                    WiFi = true,
                    Nfc = true,
                    Bluetooth = true
                },
                SatelliteNavigationSystems = new CreateSatelliteNavigationSystemExternalDto
                {
                    Gps = true,
                    Agps = false,
                    Galileo = false,
                    Glonass = false,
                    Qzss = false
                },
                Sensors = new CreateSensorsExternalDto
                {
                    Accelerometer = true,
                    Gyroscope = true,
                    Proximity = true,
                    Compass = true,
                    Barometer = false,
                    Halla = false,
                    AmbientLight = true
                },
                Price = new CreateMoneyExternalDto
                {
                    Amount = 2499.00d,
                    Currency = "PLN"
                }
            };
        }
    }
}