using ECommerceStoreBFF.Infrastructure.Generated.Products;
using ECommerceStoreBFF.Infrastructure.Generated.Products.Models;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Shouldly;

namespace ECommerceStoreBFF.IntegrationTests.Features.Products;

internal static class ProductTestData
{
    public static string UniqueName() => $"BFF phone {Guid.NewGuid():N}";

    public static ProductsApiClient Client(HttpClient httpClient)
    {
        var adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: httpClient)
        {
            BaseUrl = httpClient.BaseAddress!.ToString().TrimEnd('/')
        };
        return new ProductsApiClient(adapter);
    }

    public static async Task<MobilePhoneDetailsDto> CreatePhoneAsync(
        ProductsApiClient client, string? name = null, string brand = "Xiaomi", double price = 2499)
    {
        var response = await client.MobilePhones.PostAsync(CreateRequest(name ?? UniqueName(), brand, price));
        response.ShouldNotBeNull();
        response.Id.ShouldNotBeNull();
        return response;
    }

    public static CreateMobilePhoneExternalDto CreateRequest(string name, string brand = "Xiaomi", double price = 2499) =>
        new()
        {
            CommonDescription = Description(name, brand),
            ElectronicDetails = new CreateElectronicDetailsExternalDto
            {
                Cpu = "Octa-core", Gpu = "Adreno", Ram = "8 GB", Storage = "256 GB",
                DisplayType = "OLED", RefreshRateHz = 120, ScreenSizeInches = 6.4,
                Width = 72, Height = 152, BatteryType = "Li-Ion", BatteryCapacity = 4500
            },
            Connectivity = new CreateConnectivityExternalDto
            {
                Has5G = true, WiFi = true, Nfc = true, Bluetooth = true
            },
            SatelliteNavigationSystems = new CreateSatelliteNavigationSystemExternalDto
            {
                Gps = true, Agps = true, Galileo = true, Glonass = true, Qzss = true
            },
            Sensors = new CreateSensorsExternalDto
            {
                Accelerometer = true, Gyroscope = true, Proximity = true, Compass = true,
                Barometer = true, Halla = false, AmbientLight = true
            },
            Camera = "50 MP", FingerPrint = true, FaceId = true,
            Price = new CreateMoneyExternalDto { Amount = price, Currency = "PLN" },
            Description2 = "BFF product test", Description3 = "BFF product test details"
        };

    public static UpdateMobilePhoneExternalDto UpdateRequest(string name, double price = 1999) =>
        new()
        {
            CommonDescription = Description(name, "Xiaomi"),
            ElectronicDetails = new UpdateElectronicDetailsExternalDto
            {
                Cpu = "Octa-core", Gpu = "Adreno", Ram = "12 GB", Storage = "256 GB",
                DisplayType = "OLED", RefreshRateHz = 120, ScreenSizeInches = 6.4,
                Width = 72, Height = 152, BatteryType = "Li-Ion", BatteryCapacity = 4500
            },
            Connectivity = new UpdateConnectivityExternalDto
            {
                Has5G = true, WiFi = true, Nfc = true, Bluetooth = true
            },
            SatelliteNavigationSystems = new UpdateSatelliteNavigationSystemExternalDto
            {
                Gps = true, Agps = true, Galileo = true, Glonass = true, Qzss = true
            },
            Sensors = new UpdateSensorsExternalDto
            {
                Accelerometer = true, Gyroscope = true, Proximity = true, Compass = true,
                Barometer = true, Halla = false, AmbientLight = true
            },
            Camera = "50 MP", FingerPrint = true, FaceId = true,
            Price = new UpdateMoneyExternalDto { Amount = price, Currency = "PLN" },
            Description2 = "BFF product test", Description3 = "BFF product test details"
        };

    private static CommonDescriptionExtrernalDto Description(string name, string brand) =>
        new()
        {
            Name = name, Brand = brand, Description = "Phone for BFF integration test",
            MainPhoto = "main.jpg", OtherPhotos = ["detail.jpg"]
        };
}
