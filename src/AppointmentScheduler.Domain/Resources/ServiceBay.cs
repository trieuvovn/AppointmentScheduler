using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.Domain.Resources;

/// <summary>
/// A physical bay: one of the two constrained resources a booking must secure.
/// </summary>
public sealed class ServiceBay : IVersioned
{
    private ServiceBay(Guid id, Guid dealershipId, string code, bool isActive)
    {
        Id = id;
        DealershipId = dealershipId;
        Code = code;
        IsActive = isActive;
    }

    /// <summary>Rehydration constructor for EF Core.</summary>
    private ServiceBay()
    {
        Code = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid DealershipId { get; private set; }

    public string Code { get; private set; }

    public bool IsActive { get; private set; }

    /// <inheritdoc />
    public int Version { get; set; }

    public static ServiceBay Create(Guid id, Guid dealershipId, string code, bool isActive = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return new ServiceBay(id, dealershipId, code, isActive);
    }
}
