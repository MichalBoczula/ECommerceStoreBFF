using ECommerceStoreBFF.Infrastructure.Generated.Products;

namespace ECommerceStoreBFF.API.Endpoints;

public static class ProductsEndpoints
{
    public static IEndpointRouteBuilder MapProductsEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/categories",
            async (
                ProductsApiClient productsClient,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var resultModel = await productsClient
                        .Categories
                        .GetAsync(cancellationToken: cancellationToken);

                    if (resultModel is null)
                    {
                        return Results.NotFound(
                            new
                            {
                                Message = "No product categories found."
                            });
                    }

                    return Results.Ok(resultModel);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    return Results.StatusCode(499);
                }
                catch (Exception exception)
                {
                    return Results.Problem(
                        title: "Failed to fetch product categories",
                        detail: exception.Message,
                        statusCode: StatusCodes.Status502BadGateway);
                }
            })
            .WithName("GetCategories")
            .WithSummary(
                "Fetches product categories using the generated Kiota client.")
            .Produces(StatusCodes.Status200OK)
            .Produces(
                StatusCodes.Status404NotFound,
                contentType: "application/json")
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return app;
    }
}