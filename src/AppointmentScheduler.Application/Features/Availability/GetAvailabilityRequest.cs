namespace AppointmentScheduler.Application.Features.Availability;

/// <param name="DealershipId">The dealership to search.</param>
/// <param name="ServiceTypeId">The service to search bookable start times for.</param>
/// <param name="Date">The local calendar date to search, in the dealership's own time zone.</param>
public sealed record GetAvailabilityRequest(Guid DealershipId, Guid ServiceTypeId, DateOnly Date);
