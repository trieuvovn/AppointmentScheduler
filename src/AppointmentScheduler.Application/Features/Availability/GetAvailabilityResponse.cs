namespace AppointmentScheduler.Application.Features.Availability;

/// <param name="StartsAtUtc">The start of one bookable slot.</param>
/// <param name="EndsAtUtc">The end of that slot, derived from the service's duration.</param>
public sealed record AvailableSlotResponse(DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc);

/// <param name="OpensAtUtc">
/// The start of the dealership's opening window that day, or null when closed that day. Distinguishes
/// "open but fully booked" (non-null, <see cref="Slots"/> empty) from "closed" (null) — requirement 5.1.
/// </param>
/// <param name="Slots">Every bookable start time for the requested day, in order. Empty when none are free.</param>
public sealed record GetAvailabilityResponse(
    DateTimeOffset? OpensAtUtc,
    IReadOnlyList<AvailableSlotResponse> Slots);
