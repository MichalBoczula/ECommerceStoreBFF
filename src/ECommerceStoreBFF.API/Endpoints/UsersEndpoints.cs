using ECommerceStoreBFF.Infrastructure.Generated.Users;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceStoreBFF.API.Endpoints;

public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsersEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/dashboard",
            async (
                UsersApiClient userClient,
                [FromServices] IHttpClientFactory httpClientFactory,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var requestInfo = userClient
                        .Documentation
                        .Validations
                        .ToGetRequestInformation();

                    var targetUri = requestInfo.URI;

                    if (targetUri is null)
                    {
                        return Results.Problem(
                            title: "Invalid users service request",
                            detail: "Kiota did not generate a target URI.",
                            statusCode: StatusCodes.Status500InternalServerError);
                    }

                    var httpClient = httpClientFactory.CreateClient();

                    var rawJson = await httpClient.GetStringAsync(
                        targetUri,
                        cancellationToken);

                    return Results.Content(
                        rawJson,
                        contentType: "application/json");
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    return Results.StatusCode(499);
                }
                catch (Exception exception)
                {
                    return Results.Problem(
                        title: "Users service orchestration failed",
                        detail: exception.Message,
                        statusCode: StatusCodes.Status502BadGateway);
                }
            })
            .WithName("GetDashboardData")
            .WithSummary("Fetches dashboard data for the web UI.")
            .WithDescription(
                "Uses Kiota for compile-time route generation and returns the polymorphic payload as raw JSON.")
            .Produces(
                StatusCodes.Status200OK,
                contentType: "application/json")
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return app;
    }
}