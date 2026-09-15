namespace AppointmentScheduler.Domain.Common;

/// <summary>
/// A reference to another entity by id, as a row in a link table.
/// </summary>
public sealed class EntityReference
{
    public EntityReference(Guid id) => Id = id;

    /// <summary>Rehydration constructor for EF Core.</summary>
    private EntityReference()
    {
    }

    public Guid Id { get; private set; }
}
