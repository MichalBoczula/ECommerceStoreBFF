using ECommerceStoreBFF.Application.Registration;

internal static class RegistrationEndpoints
{
    public static IEndpointRouteBuilder MapRegistrationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/registrations/customers").WithTags("Registration");
        group.MapPost("/", async (RegisterCustomerRequest request, RegistrationService service,
            CancellationToken cancellationToken) =>
            Respond(await ExecuteAsync(() => service.RegisterAsync(request, cancellationToken), cancellationToken)));
        group.MapPost("/{externalId}/cart", async (string externalId, RegistrationService service,
            CancellationToken cancellationToken) =>
            Respond(await ExecuteAsync(() => service.RepairCartAsync(externalId, cancellationToken), cancellationToken)));
        return app;
    }

    private static async Task<RegistrationResult> ExecuteAsync(
        Func<Task<RegistrationResult>> execute, CancellationToken cancellationToken)
    {
        try { return await execute(); }
        catch (HttpRequestException) { return RegistrationResult.Failure(502); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return RegistrationResult.Failure(502); }
    }

    private static IResult Respond(RegistrationResult result)
    {
        if (result.Customer is not null) return Results.Ok(result.Customer);
        if (result.ErrorBody is not null)
            return Results.Content(result.ErrorBody, "application/problem+json", statusCode: result.Status);
        return Results.Problem(
            statusCode: result.Status,
            title: result.Status switch
            {
                400 => "External ID is required.",
                404 => "Customer was not found.",
                409 => "The existing customer does not match the registration request.",
                _ => "Registration could not be completed. Retry with the same external ID."
            });
    }
}
