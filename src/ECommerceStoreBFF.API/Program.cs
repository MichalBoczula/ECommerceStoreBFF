using Scalar.AspNetCore;
using ECommerceStoreBFF.Infrastructure;
using ECommerceStoreBFF.API.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructureServices();
builder.Services.AddOpenApi();
builder.Services.AddReverseProxy()
       .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHttpClient();

var app = builder.Build();

app.MapOpenApi("swagger/v1/swagger.json");
app.MapScalarApiReference(options =>
{
    options.WithTitle("ECommerce BFF API")
            .WithTheme(ScalarTheme.DeepSpace)
            .WithOpenApiRoutePattern("/swagger/v1/swagger.json");
});

app.MapUsersEndpoints();
app.MapProductsEndpoints();

app.MapReverseProxy();

app.Run();