namespace AppointmentScheduler.Domain.Resources;

/// <summary>
/// A capability, shared as vocabulary between technicians and service types.
/// </summary>
public sealed class Skill
{
    private Skill(Guid id, string code, string name)
    {
        Id = id;
        Code = code;
        Name = name;
    }

    /// <summary>Rehydration constructor for EF Core.</summary>
    private Skill()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public static Skill Create(Guid id, string code, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Skill(id, code, name);
    }
}
