using ECommerceStoreBFF.AcceptanceTests;
using ECommerceStoreBFF.Application.Registration;
using ECommerceStoreBFF.IntegrationTests.Features.Orders;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Shouldly;
using System.Net;
using System.Net.Http.Json;

namespace ECommerceStoreBFF.IntegrationTests.Features.Users;

[Collection("Api Test Collection")]
public class RegistrationTests(ApplicationFactory factory)
{
    [Fact]
    public async Task Register_CreatesOneEmptyCart_AndRetryReusesBothRecords()
    {
        using var http = factory.CreateClient();
        var externalId = UsersTestData.ExternalId();
        var request = Request(externalId);

        using var first = await http.PostAsJsonAsync("/registrations/customers", request);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var customer = await first.Content.ReadFromJsonAsync<CustomerResult>();
        customer.ShouldNotBeNull();
        customer.Id.ShouldNotBe(Guid.Empty);
        customer.ExternalId.ShouldBe(externalId);

        var orders = OrdersTestData.Client(http);
        var cart = await orders.ShoppingCarts.Client[customer.Id].GetAsync();
        cart.ShouldNotBeNull();
        cart.ClientId.ShouldBe(customer.Id);
        cart.Lines.ShouldNotBeNull();
        cart.Lines.ShouldBeEmpty();

        using var retry = await http.PostAsJsonAsync("/registrations/customers", request);
        retry.StatusCode.ShouldBe(HttpStatusCode.OK);
        var retried = await retry.Content.ReadFromJsonAsync<CustomerResult>();
        retried.ShouldNotBeNull();
        retried.Id.ShouldBe(customer.Id);
        var sameCart = await orders.ShoppingCarts.Client[customer.Id].GetAsync();
        sameCart.ShouldNotBeNull();
        sameCart.Id.ShouldBe(cart.Id);

        // The Users proxy still rejects duplicate profile creation.
        using var duplicate = await http.PostAsJsonAsync("/customers", request);
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_WhenInvoiceUnavailable_RecoversOnRetryWithoutDuplicatingCustomer()
    {
        var externalId = UsersTestData.ExternalId();
        var request = Request(externalId);
        using var broken = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(
            (_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Registration:InvoiceBaseUrl"] = "http://127.0.0.1:1"
            })));
        using var brokenHttp = broken.CreateClient();

        using var failed = await brokenHttp.PostAsJsonAsync("/registrations/customers", request);
        failed.StatusCode.ShouldBe(HttpStatusCode.BadGateway);

        using var http = factory.CreateClient();
        var users = UsersTestData.Client(http);
        var persisted = await users.Customers.External[externalId].GetAsync();
        persisted.ShouldNotBeNull();
        persisted.Id.ShouldNotBeNull();
        using var missingCart = await http.GetAsync($"/shopping-carts/client/{persisted.Id}");
        missingCart.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var retry = await http.PostAsJsonAsync("/registrations/customers", request);
        retry.StatusCode.ShouldBe(HttpStatusCode.OK);
        var customer = await retry.Content.ReadFromJsonAsync<CustomerResult>();
        customer.ShouldNotBeNull();
        customer.Id.ShouldBe(persisted.Id.Value);
        var cart = await OrdersTestData.Client(http).ShoppingCarts.Client[customer.Id].GetAsync();
        cart.ShouldNotBeNull();
        cart.ClientId.ShouldBe(customer.Id);
        cart.Lines.ShouldNotBeNull();
        cart.Lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task RepairCart_ForLegacyProfile_CreatesCartOnceWithoutChangingProfile()
    {
        using var http = factory.CreateClient();
        var externalId = UsersTestData.ExternalId();
        var existing = await UsersTestData.CreateCustomerAsync(UsersTestData.Client(http), externalId);
        using var repaired = await http.PostAsync($"/registrations/customers/{externalId}/cart", null);
        repaired.StatusCode.ShouldBe(HttpStatusCode.OK);
        var customer = await repaired.Content.ReadFromJsonAsync<CustomerResult>();
        customer.ShouldNotBeNull();
        customer.Id.ShouldBe(existing.Id!.Value);
        var firstCart = await OrdersTestData.Client(http).ShoppingCarts.Client[customer.Id].GetAsync();
        firstCart.ShouldNotBeNull();
        using var repeated = await http.PostAsync($"/registrations/customers/{externalId}/cart", null);
        repeated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sameCart = await OrdersTestData.Client(http).ShoppingCarts.Client[customer.Id].GetAsync();
        sameCart.ShouldNotBeNull();
        sameCart.Id.ShouldBe(firstCart.Id);
    }

    [Fact]
    public async Task Register_WithDifferentProfileForExistingExternalId_ReturnsConflict()
    {
        using var http = factory.CreateClient();
        var externalId = UsersTestData.ExternalId();
        var original = Request(externalId);
        using var created = await http.PostAsJsonAsync("/registrations/customers", original);
        created.StatusCode.ShouldBe(HttpStatusCode.OK);
        var different = Request(externalId);
        using var conflict = await http.PostAsJsonAsync("/registrations/customers", different);
        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    private sealed record CustomerResult(Guid Id, string ExternalId);

    private static RegisterCustomerRequest Request(string externalId)
    {
        var address = new AddressData("00-001", "Warsaw", "Main Street", "10", "2");
        return new RegisterCustomerRequest(externalId,
            new IndividualData("Jan", "Kowalski", $"{Guid.NewGuid():N}@example.com", "123456789", address, address), []);
    }
}
