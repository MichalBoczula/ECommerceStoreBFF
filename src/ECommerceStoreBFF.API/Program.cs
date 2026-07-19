using Scalar.AspNetCore;
using ECommerceStoreBFF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructureServices();
builder.Services.AddReverseProxy()
       .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapScalarApiReference("scalar/products", options =>
{
    options.WithTitle("ECommerce BFF - Products API")
           .WithTheme(ScalarTheme.DeepSpace)
           .WithOpenApiRoutePattern("/api/products/swagger/v1/swagger.json");
});

app.MapScalarApiReference("scalar/users", options =>
{
    options.WithTitle("ECommerce BFF - Users API")
           .WithTheme(ScalarTheme.BluePlanet)
           .WithOpenApiRoutePattern("/api/users/swagger/v1/swagger.json");
});

app.MapScalarApiReference("scalar/orders", options =>
{
    options.WithTitle("ECommerce BFF - Orders API")
           .WithTheme(ScalarTheme.Mars)
           .WithOpenApiRoutePattern("/api/orders/swagger/v1/swagger.json");
});

app.Use(async (context, next) =>
{
    Console.WriteLine($"[BFF Request Received] Path: {context.Request.Path}{context.Request.QueryString}");

    await next();

    Console.WriteLine($"[BFF Request Finished] Status Code: {context.Response.StatusCode}");
});

app.MapReverseProxy();
app.Run();