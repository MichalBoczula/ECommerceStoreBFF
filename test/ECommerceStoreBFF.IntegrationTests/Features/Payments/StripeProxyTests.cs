using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace ECommerceStoreBFF.IntegrationTests.Features.Payments;

public class StripeProxyTests
{
    [Fact]
    public async Task Proxy_PreservesSignedRawBody_AndHostedCheckoutResponse()
    {
        var payload = Encoding.UTF8.GetBytes("{\n  \"id\": \"evt_demo\", \"data\": {\"text\": \"Zażółć\"} \n}\n");
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("fixture-key"), payload));
        var checkoutResponse = "{\"checkout_url\":\"https://checkout.stripe.com/c/pay/cs_test_fixture\",\"checkout_status\":\"open\"}";
        var upstreamBuilder = WebApplication.CreateBuilder();
        upstreamBuilder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        await using var upstream = upstreamBuilder.Build();
        upstream.MapPost("/payments/webhooks/stripe", async context =>
        {
            using var buffer = new MemoryStream();
            await context.Request.Body.CopyToAsync(buffer);
            buffer.ToArray().ShouldBe(payload);
            context.Request.Headers["Stripe-Signature"].ToString().ShouldBe(signature);
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsync("{\"received\":true}");
        });
        upstream.MapPost("/payments/{orderId}/checkout", async context =>
        {
            context.Request.ContentLength.GetValueOrDefault().ShouldBe(0);
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(checkoutResponse);
        });
        await upstream.StartAsync();
        var address = upstream.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses.Single();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ReverseProxy:Clusters:payments-cluster:Destinations:destination1:Address"] = address
                })));
        using var bff = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/payments/webhooks/stripe");
        request.Content = new ByteArrayContent(payload);
        request.Headers.Add("Stripe-Signature", signature);
        using var response = await bff.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("{\"received\":true}");
        using var checkout = await bff.PostAsync($"/payments/{Guid.NewGuid()}/checkout", null);
        checkout.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await checkout.Content.ReadAsStringAsync()).ShouldBe(checkoutResponse);
        await upstream.StopAsync();
    }
}
