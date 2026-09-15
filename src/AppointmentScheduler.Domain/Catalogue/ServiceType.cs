using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.Domain.Catalogue;

/// <summary>
/// A bookable service.
/// </summary>
public sealed class ServiceType
{
    /// <summary>The longest service the catalogue permits.</summary>
    public const int MaximumDurationMinutes = 8 * 60;

    // Backed by EntityReference rather than Guid so the ServiceTypeRequiredSkills link table has a
    // type to map onto; RequiredSkillIds projects it away, so no caller sees the difference.
    private readonly List<EntityReference> _requiredSkillIds = [];

    private ServiceType(Guid id, string code, string name, int durationMinutes, bool isActive)
    {
        Id = id;
        Code = code;
        Name = name;
        DurationMinutes = durationMinutes;
        IsActive = isActive;
    }

    /// <summary>Rehydration constructor for EF Core.</summary>
    private ServiceType()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public int DurationMinutes { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>The skills a technician must all hold to perform this service.</summary>
    public IReadOnlyList<Guid> RequiredSkillIds => [.. _requiredSkillIds.Select(s => s.Id)];

    /// <summary>The duration as a <see cref="TimeSpan"/>, which is what <c>TimeSlot</c> consumes.</summary>
    public TimeSpan Duration => TimeSpan.FromMinutes(DurationMinutes);

    public static ServiceType Create(
        Guid id,
        string code,
        string name,
        int durationMinutes,
        bool isActive = true,
        IEnumerable<Guid>? requiredSkillIds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (durationMinutes is <= 0 or > MaximumDurationMinutes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(durationMinutes),
                durationMinutes,
                $"A service duration must be between 1 and {MaximumDurationMinutes} minutes.");
        }

        var serviceType = new ServiceType(id, code, name, durationMinutes, isActive);

        if (requiredSkillIds is not null)
        {
            serviceType._requiredSkillIds.AddRange(
                requiredSkillIds.Distinct().Select(id => new EntityReference(id)));
        }

        return serviceType;
    }

    /// <summary>
    /// True when <paramref name="technicianSkillIds"/> covers every skill this service requires.
    /// </summary>
    public bool IsSatisfiedBy(IEnumerable<Guid> technicianSkillIds)
    {
        ArgumentNullException.ThrowIfNull(technicianSkillIds);

        return _requiredSkillIds.Count == 0
            || !RequiredSkillIds.Except(technicianSkillIds).Any();
    }
}
