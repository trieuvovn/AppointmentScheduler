using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.Application.Features.Availability;

/// <summary>The bay and technician <see cref="AvailabilitySearch"/> assigned to a free slot.</summary>
/// <param name="Slot">The candidate slot this assignment was found for.</param>
/// <param name="ServiceBayId">The first free bay, in the repository's own order.</param>
/// <param name="TechnicianId">The first free qualified technician, in the repository's own order.</param>
public sealed record SlotAssignment(TimeSlot Slot, Guid ServiceBayId, Guid TechnicianId);

/// <summary>The outcome of searching one slot: an assignment, or the reason none could be made.</summary>
public sealed class SlotSearchResult
{
    private SlotSearchResult(SlotAssignment? assignment, BookingError? error)
    {
        Assignment = assignment;
        Error = error;
    }

    /// <summary>Set when the search succeeded; null on failure.</summary>
    public SlotAssignment? Assignment { get; }

    /// <summary>Set when the search failed; null on success.</summary>
    public BookingError? Error { get; }

    public bool IsSuccess => Assignment is not null;

    public static SlotSearchResult Success(SlotAssignment assignment) => new(assignment, null);

    public static SlotSearchResult Failure(BookingError error) => new(null, error);
}
