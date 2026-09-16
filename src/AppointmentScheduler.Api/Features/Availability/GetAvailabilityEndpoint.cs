using AppointmentScheduler.Api.Common;
using AppointmentScheduler.Application.Features.Availability;

namespace AppointmentScheduler.Api.Features.Availability;

public static class GetAvailabilityEndpoint
{
    public static IEndpointRouteBuilder MapAvailabilityEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/v1/availability", HandleAsync)
           .WithName("GetAvailability")
           .WithSummary("Every bookable start time for a dealership, service and date.");

        return app;
    }

    private static async Task<IResult> HandleAsync(
        Guid dealershipId,
        Guid serviceTypeId,
        DateOnly date,
        GetAvailabilityHandler handler,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var request = new GetAvailabilityRequest(dealershipId, serviceTypeId, date);

        var result = await handler.HandleAsync(request, ct);

        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return BookingProblem.From(result.Error, result.Detail, httpContext);
    }
}
