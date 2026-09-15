using AppointmentScheduler.Domain.Resources;

namespace AppointmentScheduler.Domain.Catalogue;

/// <summary>
/// A bookable service.
/// </summary>
public sealed class ServiceType
{
    /// <summary>The longest service the catalogue permits.</summary>
    public const int MaximumDurationMinutes = 8 * 60;

    private readonly List<Skill> _requiredSkills = [];

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

    public IReadOnlyCollection<Skill> RequiredSkills => _requiredSkills;

    /// <summary>The duration as a <see cref="TimeSpan"/>, which is what <c>TimeSlot</c> consumes.</summary>
    public TimeSpan Duration => TimeSpan.FromMinutes(DurationMinutes);

    public static ServiceType Create(
        Guid id,
        string code,
        string name,
        int durationMinutes,
        bool isActive = true,
        IEnumerable<Skill>? requiredSkills = null)
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

        if (requiredSkills is not null)
        {
            serviceType._requiredSkills.AddRange(requiredSkills.DistinctBy(skill => skill.Id));
        }

        return serviceType;
    }

    /// <summary>
    /// True when <paramref name="technicianSkills"/> covers every skill this service requires.
    /// </summary>
    public bool IsSatisfiedBy(IEnumerable<Skill> technicianSkills)
    {
        ArgumentNullException.ThrowIfNull(technicianSkills);

        if (_requiredSkills.Count == 0)
        {
            return true;
        }

        var held = technicianSkills.Select(skill => skill.Id).ToHashSet();

        return _requiredSkills.All(required => held.Contains(required.Id));
    }
}
