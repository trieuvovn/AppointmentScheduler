using AppointmentScheduler.Api.Common;
using AppointmentScheduler.Application.Features.Booking;

namespace AppointmentScheduler.Api.Features.Booking;

public static class BookAppointmentEndpoint
{
    public static IEndpointRouteBuilder MapAppointmentEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/appointments").WithTags("Appointments");

        group.MapPost("/", HandleAsync)
             .WithName("BookAppointment")
             .WithSummary("Books a specific start time, returning the created appointment.")
             .WithValidation<BookAppointmentRequest>();

        return app;
    }

    private static async Task<IResult> HandleAsync(
        BookAppointmentRequest body,
        BookAppointmentHandler handler,
        HttpRequest httpRequest,
        CancellationToken ct)
    {
        var request = body with
        {
            IdempotencyKey = httpRequest.Headers["Idempotency-Key"].FirstOrDefault() ?? body.IdempotencyKey,
        };

        var result = await handler.HandleAsync(request, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : BookingProblem.From(result.Error, result.Detail, httpRequest.HttpContext);
    }
}
