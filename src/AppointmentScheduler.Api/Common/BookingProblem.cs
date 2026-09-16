using AppointmentScheduler.Domain.Appointments;

namespace AppointmentScheduler.Api.Common;

internal static class BookingProblem
{
    internal static IResult From(BookingError? error, string? detail, HttpContext httpContext) => error switch
    {
        BookingError.DealershipNotFound or
        BookingError.ServiceTypeNotFound or
        BookingError.VehicleNotFound or
        BookingError.AppointmentNotFound
            => Problem(StatusCodes.Status404NotFound, "The requested resource was not found", error, detail, httpContext),

        BookingError.NoServiceBayAvailable or
        BookingError.NoQualifiedTechnicianAvailable or
        BookingError.VehicleAlreadyBooked
            => Problem(StatusCodes.Status409Conflict, "The request conflicts with the current schedule", error, detail, httpContext),

        BookingError.OutsideOpeningHours or
        BookingError.CrossesClosingTime or
        BookingError.StartsInThePast or
        BookingError.BeyondBookingHorizon or
        BookingError.InvalidStatusTransition
            => Problem(StatusCodes.Status422UnprocessableEntity, "The request could not be processed", error, detail, httpContext),

        _ => Problem(StatusCodes.Status422UnprocessableEntity, "The request could not be processed", error, detail, httpContext),
    };

    private static IResult Problem(
        int statusCode, string title, BookingError? error, string? detail, HttpContext httpContext)
    {
        var correlationId = httpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemsKey, out var value)
            ? value as string
            : null;

        var extensions = new Dictionary<string, object?> { ["correlationId"] = correlationId };

        if (error is not null)
        {
            extensions["error"] = error.Value.ToString();
        }

        return Results.Problem(
            title: title,
            statusCode: statusCode,
            detail: detail,
            extensions: extensions);
    }
}
