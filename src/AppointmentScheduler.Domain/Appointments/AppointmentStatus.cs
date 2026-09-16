namespace AppointmentScheduler.Domain.Appointments;

/// <summary>
/// The lifecycle of an appointment.
/// </summary>
public enum AppointmentStatus
{
    /// <summary>Booked and occupying its bay and technician.</summary>
    Confirmed = 1,

    /// <summary>Work has started. Still occupying its resources.</summary>
    InProgress = 2,

    /// <summary>Work finished. No longer occupies anything.</summary>
    Completed = 3,

    /// <summary>Called off. Frees its resources immediately (plan stage 5).</summary>
    Cancelled = 4,
}
