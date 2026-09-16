using AppointmentScheduler.Domain.Appointments;

namespace AppointmentScheduler.Api.Common;

internal static class BookingProblem
{
    internal static IResult From(BookingError? error, string? detail = null) => error switch
    {
        BookingError.DealershipNotFound or
        BookingError.ServiceTypeNotFound or
        BookingError.VehicleNotFound or
        BookingError.AppointmentNotFound
            => Problem(StatusCodes.Status404NotFound, "The requested resource was not found", error, detail),

        BookingError.NoServiceBayAvailable or
        BookingError.NoQualifiedTechnicianAvailable or
        BookingError.VehicleAlreadyBooked
            => Problem(StatusCodes.Status409Conflict, "The request conflicts with the current schedule", error, detail),

        BookingError.OutsideOpeningHours or
        BookingError.CrossesClosingTime or
        BookingError.StartsInThePast or
        BookingError.BeyondBookingHorizon or
        BookingError.InvalidStatusTransition
            => Problem(StatusCodes.Status422UnprocessableEntity, "The request could not be processed", error, detail),

        _ => Problem(StatusCodes.Status422UnprocessableEntity, "The request could not be processed", error, detail),
    };

    private static IResult Problem(int statusCode, string title, BookingError? error, string? detail) =>
        Results.Problem(
            title: title,
            statusCode: statusCode,
            detail: detail,
            extensions: error is null
                ? null
                : new Dictionary<string, object?> { ["error"] = error.Value.ToString() });
}
