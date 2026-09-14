namespace AppointmentScheduler.Domain.Appointments;

/// <summary>
/// Thrown when a status change is attempted that the lifecycle does not allow.
/// </summary>
public sealed class InvalidStatusTransitionException : InvalidOperationException
{
    public InvalidStatusTransitionException(AppointmentStatus from, AppointmentStatus to)
        : base($"An appointment cannot move from {from} to {to}.")
    {
        From = from;
        To = to;
    }

    public AppointmentStatus From { get; }

    public AppointmentStatus To { get; }
}
