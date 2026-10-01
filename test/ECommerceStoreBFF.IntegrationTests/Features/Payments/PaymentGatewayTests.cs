using ECommerceStoreBFF.AcceptanceTests;
using ECommerceStoreBFF.IntegrationTests.Features.Orders;
using ECommerceStoreBFF.IntegrationTests.Features.Products;
using Shouldly;
using System.Net;
using System.Text.Json;

namespace ECommerceStoreBFF.IntegrationTests.Features.Payments;

[Collection("Api Test Collection")]
public class PaymentGatewayTests(ApplicationFactory factory)
{
    [Fact]
    public async Task PayAndRead_ReusesOneCreatedPayment_WithoutPayingOrderOrIssuingInvoice()
    {
        using var bff = factory.CreateClient();
        var phone = await ProductTestData.CreatePhoneAsync(ProductTestData.Client(bff));
        var clientId = Guid.NewGuid();
        var order = await OrdersTestData.CreateOrderAsync(
            OrdersTestData.Client(bff), clientId, phone.Id!.Value);

        using var created = await bff.PostAsync($"/payments/{order.Id}/pay", null);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var payment = createdJson.RootElement;
        payment.GetProperty("order_id").GetGuid().ShouldBe(order.Id!.Value);
        payment.GetProperty("status").GetString().ShouldBe("created");
        var paymentId = payment.GetProperty("id").GetGuid();

        using var repeated = await bff.PostAsync($"/payments/{order.Id}/pay", null);
        repeated.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var repeatJson = JsonDocument.Parse(await repeated.Content.ReadAsStringAsync());
        repeatJson.RootElement.GetProperty("id").GetGuid().ShouldBe(paymentId);

        using var read = await bff.GetAsync($"/payments/order/{order.Id}");
        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var readJson = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
        readJson.RootElement.GetProperty("id").GetGuid().ShouldBe(paymentId);

        var savedOrder = await OrdersTestData.Client(bff).Orders[order.Id.Value].GetAsync();
        savedOrder.ShouldNotBeNull();
        savedOrder.Status.ShouldBe("Created");
        using var invoice = await bff.PostAsync($"/invoices/{clientId}/{order.Id}", null);
        invoice.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CheckoutDisabled_PreservesError_WithoutCreatingPayment()
    {
        using var bff = factory.CreateClient();
        var phone = await ProductTestData.CreatePhoneAsync(ProductTestData.Client(bff));
        var clientId = Guid.NewGuid();
        var order = await OrdersTestData.CreateOrderAsync(
            OrdersTestData.Client(bff), clientId, phone.Id!.Value);

        using var checkout = await bff.PostAsync($"/payments/{order.Id}/checkout", null);
        checkout.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        using var body = JsonDocument.Parse(await checkout.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().ShouldBe("checkout_disabled");
        using var payment = await bff.GetAsync($"/payments/order/{order.Id}");
        payment.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var savedOrder = await OrdersTestData.Client(bff).Orders[order.Id!.Value].GetAsync();
        savedOrder!.Status.ShouldBe("Created");
        using var invoice = await bff.GetAsync($"/invoices/by-order/{clientId}/{order.Id}");
        invoice.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnknownOrder_PreservesPaymentsNotFoundError()
    {
        using var bff = factory.CreateClient();
        using var response = await bff.PostAsync($"/payments/{Guid.NewGuid()}/pay", null);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().ShouldBe("order_not_found");
    }
}
