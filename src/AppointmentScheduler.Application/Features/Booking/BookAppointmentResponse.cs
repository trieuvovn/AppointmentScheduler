namespace AppointmentScheduler.Application.Features.Booking;

public sealed record BookAppointmentResponse(
    Guid AppointmentId, Guid DealershipId, Guid ServiceBayId, Guid TechnicianId,
    Guid ServiceTypeId, Guid VehicleId, Guid CustomerId,
    DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc, string Status);
