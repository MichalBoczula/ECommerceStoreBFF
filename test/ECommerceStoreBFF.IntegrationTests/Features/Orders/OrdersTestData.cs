using ECommerceStoreBFF.Infrastructure.Generated.Orders;
using ECommerceStoreBFF.Infrastructure.Generated.Orders.Models;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Shouldly;
using System.Net;
using System.Text.Json;

namespace ECommerceStoreBFF.IntegrationTests.Features.Orders;

internal static class OrdersTestData
{
    public static OrdersApiClient Client(HttpClient httpClient)
    {
        var adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: httpClient)
        {
            BaseUrl = httpClient.BaseAddress!.ToString().TrimEnd('/')
        };
        return new OrdersApiClient(adapter);
    }

    public static CreateClientDataVersionRequestDto ClientDataRequest() => new()
    {
        ClientName = "Jan Kowalski",
        PostalCode = "00-001",
        City = "Warsaw",
        Street = "Main.St",
        BuildingNumber = "10",
        ApartmentNumber = "2",
        PhoneNumber = "123456789",
        PhonePrefix = "48",
        AddressEmail = $"{Guid.NewGuid():N}@example.com"
    };

    public static UpdateShoppingCartRequestDto CartRequest(Guid productId, int quantity = 2) => new()
    {
        Lines = [new ShoppingCartLineRequestDto { ProductId = productId, Quantity = quantity }]
    };

    public static async Task<OrderResponseDto> CreateOrderAsync(OrdersApiClient api, Guid clientId, Guid productId)
    {
        var cart = await api.ShoppingCarts[clientId].PostAsync();
        cart.ShouldNotBeNull();
        cart.ClientId.ShouldBe(clientId);

        var updated = await api.ShoppingCarts[clientId].PutAsync(CartRequest(productId));
        updated.ShouldNotBeNull();
        updated.Lines.ShouldNotBeNull();
        updated.Lines.Count.ShouldBe(1);
        updated.Lines[0].ProductId.ShouldBe(productId);

        var order = await api.Orders.Client[clientId].PostAsync();
        order.ShouldNotBeNull();
        order.Id.ShouldNotBeNull();
        return order;
    }

    public static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("status").GetInt32().ShouldBe((int)status);
        json.RootElement.GetProperty("title").GetString().ShouldNotBeNullOrWhiteSpace();
        json.RootElement.GetProperty("detail").GetString().ShouldNotBeNullOrWhiteSpace();
    }
}
