namespace AppointmentScheduler.Application.Features.Booking;

public sealed record BookAppointmentRequest(
    Guid DealershipId, Guid ServiceTypeId, Guid VehicleId, Guid CustomerId, DateTimeOffset StartsAtUtc)
{
    public string? IdempotencyKey { get; init; }
}
