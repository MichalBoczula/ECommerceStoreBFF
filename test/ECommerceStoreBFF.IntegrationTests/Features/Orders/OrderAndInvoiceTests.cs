using ECommerceStoreBFF.AcceptanceTests;
using ECommerceStoreBFF.Infrastructure.Generated.Orders.Models;
using ECommerceStoreBFF.IntegrationTests.Features.Products;
using ECommerceStoreBFF.IntegrationTests.Features.Users;
using Shouldly;
using System.Net;
using System.Net.Http.Json;

namespace ECommerceStoreBFF.IntegrationTests.Features.Orders;

[Collection("Api Test Collection")]
public class OrderAndInvoiceTests(ApplicationFactory factory)
{
    [Fact]
    public async Task CustomerProductCartOrderAndInvoice_FlowThroughBff()
    {
        using var httpClient = factory.CreateClient();
        var users = UsersTestData.Client(httpClient);
        var products = ProductTestData.Client(httpClient);
        var orders = OrdersTestData.Client(httpClient);

        var customer = await UsersTestData.CreateCustomerAsync(users, UsersTestData.ExternalId());
        var clientId = customer.Id!.Value;
        var phone = await ProductTestData.CreatePhoneAsync(products);
        var productId = phone.Id!.Value;

        var version = await orders.ClientDataVersions[clientId].PostAsync(OrdersTestData.ClientDataRequest());
        version.ShouldNotBeNull();
        version.Id.ShouldNotBeNull();
        version.ClientId.ShouldBe(clientId);
        var savedVersion = await orders.ClientDataVersions.Client[clientId].GetAsync();
        savedVersion.ShouldNotBeNull();
        savedVersion.Id.ShouldBe(version.Id);

        var created = await OrdersTestData.CreateOrderAsync(orders, clientId, productId);
        created.ClientId.ShouldBe(clientId);
        created.Status.ShouldBe("Created");
        created.TotalAmount.ShouldBe(4998);
        created.TotalCurrency.ShouldBe("PLN");
        created.Lines.ShouldNotBeNull();
        created.Lines.Count.ShouldBe(1);
        created.Lines[0].Quantity.ShouldBe(2);
        created.Lines[0].ProductVersion.ShouldNotBeNull();
        created.Lines[0].ProductVersion.ProductId.ShouldBe(productId);

        var cartAfterCheckout = await orders.ShoppingCarts.Client[clientId].GetAsync();
        cartAfterCheckout.ShouldNotBeNull();
        cartAfterCheckout.Lines.ShouldNotBeNull();
        cartAfterCheckout.Lines.ShouldBeEmpty();

        var read = await orders.Orders[created.Id!.Value].GetAsync();
        read.ShouldNotBeNull();
        read.Id.ShouldBe(created.Id);
        var byClient = await orders.Orders.Client[clientId].GetAsync();
        byClient.ShouldNotBeNull();
        byClient.ShouldContain(x => x.Id == created.Id);

        var paid = await orders.Orders[created.Id.Value].Status.PatchAsync(new UpdateOrderStatusRequestDto { Status = "Paid" });
        paid.ShouldNotBeNull();
        paid.Status.ShouldBe("Paid");

        var invoice = await orders.Invoices[clientId][created.Id.Value].PostAsync();
        invoice.ShouldNotBeNull();
        invoice.Id.ShouldNotBeNull();
        invoice.OrderId.ShouldBe(created.Id);
        invoice.ClietDataVersionId.ShouldBe(version.Id);
        invoice.StorageUrl.ShouldNotBeNullOrWhiteSpace();
        var readInvoice = await orders.Invoices[invoice.Id!.Value].GetAsync();
        readInvoice.ShouldNotBeNull();
        readInvoice.Id.ShouldBe(invoice.Id);
        readInvoice.OrderId.ShouldBe(created.Id);

        using var duplicateInvoice = await httpClient.PostAsync($"/invoices/{clientId}/{created.Id}", null);
        await OrdersTestData.AssertProblemAsync(duplicateInvoice, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Cart_RejectsInvalidQuantityAndDuplicateCreation()
    {
        using var httpClient = factory.CreateClient();
        var orders = OrdersTestData.Client(httpClient);
        var clientId = Guid.NewGuid();
        var cart = await orders.ShoppingCarts[clientId].PostAsync();
        cart.ShouldNotBeNull();

        using var invalid = await httpClient.PutAsJsonAsync($"/shopping-carts/{clientId}",
            OrdersTestData.CartRequest(Guid.NewGuid(), quantity: 0));
        await OrdersTestData.AssertProblemAsync(invalid, HttpStatusCode.BadRequest);
        (await invalid.Content.ReadAsStringAsync()).ShouldContain("Quantity");

        using var duplicate = await httpClient.PostAsync($"/shopping-carts/{clientId}", null);
        await OrdersTestData.AssertProblemAsync(duplicate, HttpStatusCode.Conflict);
        var saved = await orders.ShoppingCarts.Client[clientId].GetAsync();
        saved.ShouldNotBeNull();
        saved.Lines.ShouldNotBeNull();
        saved.Lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task MissingCartOrderAndInvoice_ReturnNotFound()
    {
        using var httpClient = factory.CreateClient();
        var clientId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        using var missingCart = await httpClient.GetAsync($"/shopping-carts/client/{clientId}");
        await OrdersTestData.AssertProblemAsync(missingCart, HttpStatusCode.NotFound);
        using var createWithoutCart = await httpClient.PostAsync($"/orders/client/{clientId}", null);
        await OrdersTestData.AssertProblemAsync(createWithoutCart, HttpStatusCode.NotFound);
        using var missingOrder = await httpClient.GetAsync($"/orders/{orderId}");
        await OrdersTestData.AssertProblemAsync(missingOrder, HttpStatusCode.NotFound);
        using var missingInvoice = await httpClient.GetAsync($"/invoices/{Guid.NewGuid()}");
        await OrdersTestData.AssertProblemAsync(missingInvoice, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnpaidOrder_RejectsInvoiceAndInvalidStatus()
    {
        using var httpClient = factory.CreateClient();
        var orders = OrdersTestData.Client(httpClient);
        var products = ProductTestData.Client(httpClient);
        var clientId = Guid.NewGuid();
        var phone = await ProductTestData.CreatePhoneAsync(products);
        var created = await OrdersTestData.CreateOrderAsync(orders, clientId, phone.Id!.Value);

        using var unpaidInvoice = await httpClient.PostAsync($"/invoices/{clientId}/{created.Id}", null);
        await OrdersTestData.AssertProblemAsync(unpaidInvoice, HttpStatusCode.BadRequest);

        using var invalidStatus = await httpClient.PatchAsJsonAsync($"/orders/{created.Id}/status",
            new UpdateOrderStatusRequestDto { Status = "Unknown" });
        await OrdersTestData.AssertProblemAsync(invalidStatus, HttpStatusCode.BadRequest);
        var saved = await orders.Orders[created.Id!.Value].GetAsync();
        saved.ShouldNotBeNull();
        saved.Status.ShouldBe("Created");
    }
}
