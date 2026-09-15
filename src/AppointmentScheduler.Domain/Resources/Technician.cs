using AppointmentScheduler.Domain.Catalogue;
using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.Domain.Resources;

/// <summary>
/// A technician: the human constrained resource
/// </summary>
public sealed class Technician : IVersioned
{
    private readonly List<EntityReference> _skillIds = [];

    private Technician(Guid id, Guid dealershipId, string fullName, bool isActive)
    {
        Id = id;
        DealershipId = dealershipId;
        FullName = fullName;
        IsActive = isActive;
    }

    /// <summary>Rehydration constructor for EF Core.</summary>
    private Technician()
    {
        FullName = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid DealershipId { get; private set; }

    public string FullName { get; private set; }

    public bool IsActive { get; private set; }

    /// <inheritdoc />
    public int Version { get; set; }

    /// <summary>The skills this technician holds.</summary>
    public IReadOnlyList<Guid> SkillIds => [.. _skillIds.Select(s => s.Id)];

    public static Technician Create(
        Guid id,
        Guid dealershipId,
        string fullName,
        bool isActive = true,
        IEnumerable<Guid>? skillIds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);

        var technician = new Technician(id, dealershipId, fullName, isActive);

        if (skillIds is not null)
        {
            technician._skillIds.AddRange(skillIds.Distinct().Select(id => new EntityReference(id)));
        }

        return technician;
    }

    /// <summary>True when this technician holds every skill <paramref name="serviceType"/> requires.</summary>
    public bool IsQualifiedFor(ServiceType serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        return serviceType.IsSatisfiedBy(SkillIds);
    }
}
