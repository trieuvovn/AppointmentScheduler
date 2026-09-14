namespace AppointmentScheduler.Domain.Common;

/// <summary>
/// Carries the optimistic-concurrency token for entities the system updates after creation.
/// </summary>
public interface IVersioned
{
    int Version { get; set; }
}
