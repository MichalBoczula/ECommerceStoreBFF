using Scalar.AspNetCore;
using ECommerceStoreBFF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructureServices();
builder.Services.AddOpenApi();
builder.Services.AddReverseProxy()
       .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapOpenApi("swagger/v1/swagger.json");
app.MapScalarApiReference(options =>
{
    options.WithTitle("ECommerce BFF API")
            .WithTheme(ScalarTheme.DeepSpace)
            .WithOpenApiRoutePattern("/swagger/v1/swagger.json");
});

app.MapGet("/api/dashboard", async (
    ECommerceStoreBFF.Infrastructure.Generated.Users.UsersApiClient userClient) =>
{
    try
    {
        var userTask = userClient.Documentation.Validations.GetAsync();

        await Task.WhenAll(userTask);

        var user = userTask.Result;

        return Results.Ok(user);
    }
    catch (Exception ex)
    {
        return Results.Problem($"BFF Orchestration Error: {ex.Message}");
    }
})
.WithName("GetDashboardData")
.AddOpenApiOperationTransformer((operation, context, ct) =>
{
    operation.Summary = "Fetches dashboard data for the web UI.";
    return Task.CompletedTask;
});

app.MapReverseProxy();

app.Run();