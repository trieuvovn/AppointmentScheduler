using AppointmentScheduler.Domain.Catalogue;
using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.Domain.Appointments;

/// <summary>
/// A booked service visit: a vehicle, in a bay, with a technician, over an interval.
/// </summary>
public sealed class Appointment : IVersioned
{
    private Appointment(
        Guid id,
        Guid dealershipId,
        Guid serviceBayId,
        Guid technicianId,
        Guid serviceTypeId,
        Guid vehicleId,
        Guid customerId,
        TimeSlot slot,
        AppointmentStatus status)
    {
        Id = id;
        DealershipId = dealershipId;
        ServiceBayId = serviceBayId;
        TechnicianId = technicianId;
        ServiceTypeId = serviceTypeId;
        VehicleId = vehicleId;
        CustomerId = customerId;
        StartsAtUtc = slot.Start;
        EndsAtUtc = slot.End;
        Status = status;
    }

    /// <summary>Rehydration constructor for EF Core. Bypasses the invariants, which the stored row already satisfies.</summary>
    private Appointment()
    {
    }

    public Guid Id { get; private set; }

    public Guid DealershipId { get; private set; }

    public Guid ServiceBayId { get; private set; }

    public Guid TechnicianId { get; private set; }

    public Guid ServiceTypeId { get; private set; }

    public Guid VehicleId { get; private set; }

    public Guid CustomerId { get; private set; }

    public DateTimeOffset StartsAtUtc { get; private set; }

    public DateTimeOffset EndsAtUtc { get; private set; }

    public AppointmentStatus Status { get; private set; }

    public string? IdempotencyKey { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <inheritdoc />
    public int Version { get; set; }

    /// <summary>The interval this appointment occupies, rebuilt from its stored bounds.</summary>
    public TimeSlot Slot => TimeSlot.Create(StartsAtUtc, EndsAtUtc);

    /// <summary>
    /// The statuses in which an appointment holds its bay and technician.
    /// </summary>
    public static IReadOnlyCollection<AppointmentStatus> OccupyingStatuses { get; } =
        [AppointmentStatus.Confirmed, AppointmentStatus.InProgress];

    /// <summary>True while this appointment still holds its resources.</summary>
    public bool Occupies => OccupyingStatuses.Contains(Status);

    /// <summary>True once this appointment can no longer change status.</summary>
    public bool IsTerminal => !Occupies;

    /// <summary>Books an appointment, deriving its end time from the service type's duration.</summary>
    public static Appointment Book(
        Guid id,
        Guid dealershipId,
        Guid serviceBayId,
        Guid technicianId,
        ServiceType serviceType,
        Guid vehicleId,
        Guid customerId,
        DateTimeOffset startsAtUtc,
        DateTimeOffset createdAtUtc,
        string? idempotencyKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        var slot = TimeSlot.FromDuration(startsAtUtc, serviceType.Duration);

        return new Appointment(
            id,
            dealershipId,
            serviceBayId,
            technicianId,
            serviceType.Id,
            vehicleId,
            customerId,
            slot,
            AppointmentStatus.Confirmed)
        {
            CreatedAtUtc = createdAtUtc,
            IdempotencyKey = idempotencyKey,
        };
    }

    /// <summary>Marks work as started. Legal only from <see cref="AppointmentStatus.Confirmed"/>.</summary>
    public void Start() => TransitionTo(AppointmentStatus.InProgress);

    /// <summary>Marks work as finished. Legal only from <see cref="AppointmentStatus.InProgress"/>.</summary>
    public void Complete() => TransitionTo(AppointmentStatus.Completed);

    /// <summary>Cancels the appointment, freeing its bay and technician.</summary>
    public void Cancel() => TransitionTo(AppointmentStatus.Cancelled);

    /// <summary>True when <paramref name="target"/> is reachable from the current status.</summary>
    public bool CanTransitionTo(AppointmentStatus target) => IsAllowed(Status, target);

    private void TransitionTo(AppointmentStatus target)
    {
        if (!IsAllowed(Status, target))
        {
            throw new InvalidStatusTransitionException(Status, target);
        }

        Status = target;
    }

    /// <summary>
    /// The transition table. Confirmed may start, complete early or cancel; InProgress may only finish
    /// or be cancelled; the two terminal statuses admit nothing.
    /// </summary>
    private static bool IsAllowed(AppointmentStatus from, AppointmentStatus to) => (from, to) switch
    {
        (AppointmentStatus.Confirmed, AppointmentStatus.InProgress) => true,
        (AppointmentStatus.Confirmed, AppointmentStatus.Completed) => true,
        (AppointmentStatus.Confirmed, AppointmentStatus.Cancelled) => true,

        (AppointmentStatus.InProgress, AppointmentStatus.Completed) => true,
        (AppointmentStatus.InProgress, AppointmentStatus.Cancelled) => true,

        _ => false,
    };
}
