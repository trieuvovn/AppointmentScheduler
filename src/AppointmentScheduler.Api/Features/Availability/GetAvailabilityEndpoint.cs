using AppointmentScheduler.Application.Features.Availability;
using AppointmentScheduler.Domain.Appointments;

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
        CancellationToken ct)
    {
        var request = new GetAvailabilityRequest(dealershipId, serviceTypeId, date);

        var result = await handler.HandleAsync(request, ct);

        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return result.Error switch
        {
            BookingError.DealershipNotFound => Results.Problem(
                title: "Dealership not found", statusCode: StatusCodes.Status404NotFound),
            BookingError.ServiceTypeNotFound => Results.Problem(
                title: "Service type not found", statusCode: StatusCodes.Status404NotFound),
            _ => Results.Problem(
                title: "The request could not be processed", statusCode: StatusCodes.Status422UnprocessableEntity),
        };
    }
}
