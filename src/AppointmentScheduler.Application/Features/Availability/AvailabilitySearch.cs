using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.Application.Features.Availability;

public static class AvailabilitySearch
{
    /// <summary>The step between candidate start times a day-grid search offers.</summary>
    public const int SlotGranularityMinutes = 30;

    public static SlotSearchResult Search(
        TimeSlot slot,
        TimeSlot? openingWindow,
        IReadOnlyList<ResourceOccupancy> bays,
        IReadOnlyList<ResourceOccupancy> technicians)
    {
        ArgumentNullException.ThrowIfNull(bays);
        ArgumentNullException.ThrowIfNull(technicians);

        if (openingWindow is not { } window)
        {
            return SlotSearchResult.Failure(BookingError.OutsideOpeningHours);
        }

        if (slot.Start < window.Start || slot.Start >= window.End)
        {
            return SlotSearchResult.Failure(BookingError.OutsideOpeningHours);
        }

        if (slot.End > window.End)
        {
            return SlotSearchResult.Failure(BookingError.CrossesClosingTime);
        }

        var bay = bays.FirstOrDefault(b => b.IsFreeDuring(slot));

        if (bay is null)
        {
            return SlotSearchResult.Failure(BookingError.NoServiceBayAvailable);
        }

        var technician = technicians.FirstOrDefault(t => t.IsFreeDuring(slot));

        if (technician is null)
        {
            return SlotSearchResult.Failure(BookingError.NoQualifiedTechnicianAvailable);
        }

        return SlotSearchResult.Success(new SlotAssignment(slot, bay.ResourceId, technician.ResourceId));
    }

    public static IReadOnlyList<TimeSlot> CandidateSlots(TimeSlot openingWindow, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "A slot must have a positive duration.");
        }

        var step = TimeSpan.FromMinutes(SlotGranularityMinutes);
        var slots = new List<TimeSlot>();

        for (var start = openingWindow.Start; start < openingWindow.End; start += step)
        {
            slots.Add(TimeSlot.FromDuration(start, duration));
        }

        return slots;
    }

    public static IReadOnlyList<SlotAssignment> SearchDay(
        TimeSlot? openingWindow,
        TimeSpan duration,
        IReadOnlyList<ResourceOccupancy> bays,
        IReadOnlyList<ResourceOccupancy> technicians)
    {
        ArgumentNullException.ThrowIfNull(bays);
        ArgumentNullException.ThrowIfNull(technicians);

        if (openingWindow is not { } window)
        {
            return [];
        }

        var assignments = new List<SlotAssignment>();

        foreach (var candidate in CandidateSlots(window, duration))
        {
            var result = Search(candidate, window, bays, technicians);

            if (result.IsSuccess)
            {
                assignments.Add(result.Assignment!);
            }
        }

        return assignments;
    }
}
