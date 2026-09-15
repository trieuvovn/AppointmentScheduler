using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.Infrastructure.Persistence;

/// <summary>
/// The single definition of "this appointment occupies its resources", and of the overlap test that
/// every availability query applies.
/// </summary>
internal static class OccupyingExtensions
{
    /// <summary>
    /// Narrows to the appointments that still hold their bay and technician.
    /// </summary>
    internal static IQueryable<Appointment> Occupying(this IQueryable<Appointment> appointments)
    {
        ArgumentNullException.ThrowIfNull(appointments);
        return appointments.Where(a =>
            a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.InProgress);
    }

    /// <summary>
    /// Narrows to the appointments overlapping <paramref name="slot"/>, treating both intervals as
    /// half-open, exactly as <see cref="TimeSlot.Overlaps"/> does.
    /// </summary>
    internal static IQueryable<Appointment> OverlappingWith(
        this IQueryable<Appointment> appointments, TimeSlot slot)
    {
        ArgumentNullException.ThrowIfNull(appointments);

        return appointments.Where(a => a.StartsAtUtc < slot.End && a.EndsAtUtc > slot.Start);
    }
}
